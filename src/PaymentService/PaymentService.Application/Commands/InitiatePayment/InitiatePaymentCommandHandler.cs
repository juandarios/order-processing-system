using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Exceptions;
using Shared.Contracts;
using UUIDNext;

namespace PaymentService.Application.Commands.InitiatePayment;

/// <summary>
/// Handles <see cref="InitiatePaymentCommand"/> in two phases:
/// <para>
/// <b>Phase 1 (synchronous — responsibility towards S3):</b>
/// Checks idempotency. If the payment already exists the existing ID is returned immediately.
/// Otherwise a new payment is persisted in <c>Pending</c> status and the payment ID is returned
/// as 202 Accepted. S3 is never blocked on downstream gateway availability.
/// </para>
/// <para>
/// <b>Phase 2 (background — S2's internal responsibility):</b>
/// The gateway call is issued asynchronously using a dedicated DI scope so that the HTTP request
/// scope is not held open. Only primitive values (Guid, decimal, string) are captured from the
/// outer scope — never services, DbContext, HttpContext, or any CancellationToken from the request.
/// If the gateway call fails after Polly exhausts all retries, the payment is marked as
/// <c>Expired</c> and S3 is notified of the failure — all without affecting the already-returned
/// 202 response.
/// </para>
/// </summary>
public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IServiceScopeFactory scopeFactory,
    ILogger<InitiatePaymentCommandHandler> logger)
    : IRequestHandler<InitiatePaymentCommand, Guid>
{
    /// <summary>
    /// Initiates the payment process.
    /// Idempotent: if a payment already exists for the given <c>orderId</c> in any state,
    /// the existing payment identifier is returned without creating a duplicate record or
    /// re-calling the gateway.
    /// </summary>
    /// <param name="request">The command with order payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The payment identifier — either from the newly created payment or from the existing one.
    /// </returns>
    public async ValueTask<Guid> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        // --- Phase 1: Idempotency check + persist (responsibility towards S3) ---

        // Application-level idempotency guard (first line of defence).
        var existing = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
        {
            logger.DuplicatePaymentDetected(request.OrderId);
            return existing.Id;
        }

        var paymentId = Uuid.NewSequential();
        var payment = Payment.Create(paymentId, request.OrderId, request.Amount, request.Currency);

        // ON CONFLICT DO NOTHING in AddAsync provides database-level idempotency
        // as a second line of defence against race conditions.
        await paymentRepository.AddAsync(payment, ct);

        // Always re-query after insert to ensure we have the persisted row
        // (covers the unlikely race where another concurrent request won the conflict).
        var persisted = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct);
        var resolvedId = persisted?.Id ?? paymentId;

        logger.PaymentPersistedAndAccepted(resolvedId, request.OrderId);

        // --- Phase 2: Fire-and-forget gateway call (S2's internal responsibility) ---
        // Capture ONLY primitive values from the request scope.
        // NEVER capture: services, DbContext, HttpContext, or any CancellationToken from the request.
        // IServiceScopeFactory is a singleton — safe to capture from the constructor field.
        //
        // TODO: Replace fire-and-forget with Outbox Pattern to guarantee delivery.
        var capturedPaymentId = resolvedId;
        var capturedOrderId = request.OrderId;
        var capturedAmount = request.Amount;
        var capturedCurrency = request.Currency;

        _ = Task.Run(async () =>
        {
            // Create a completely independent DI scope — never reuse the request scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            var gateway = scope.ServiceProvider.GetRequiredService<IPaymentGatewayClient>();
            var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestratorClient>();

            // Use CancellationToken.None — background work must not be tied to the HTTP request lifecycle.
            await ChargeGatewayAsync(
                capturedPaymentId, capturedOrderId, capturedAmount, capturedCurrency,
                gateway, repository, orchestrator, CancellationToken.None);
        }, CancellationToken.None);

        return resolvedId;
    }

    /// <summary>
    /// Calls the payment gateway and handles the result or failure entirely within its own
    /// DI scope. All dependencies are passed as parameters — this method has no access to any
    /// instance field other than the logger (which is a singleton-safe dependency).
    /// <para>
    /// On failure, distinguishes between two cases:
    /// <list type="bullet">
    /// <item><term>Gateway unreachable</term><description>
    /// Inner exception is <see cref="System.Net.Http.HttpRequestException"/> or
    /// <see cref="System.Net.Sockets.SocketException"/>. Payment transitions to
    /// <c>Failed</c>; orchestrator notified with <c>status="failed"</c>,
    /// <c>reason="gateway_unavailable"</c>.
    /// </description></item>
    /// <item><term>Gateway timeout</term><description>
    /// Inner exception is <see cref="TaskCanceledException"/> (Polly wraps
    /// <c>TimeoutRejectedException</c>). Payment transitions to <c>Expired</c>;
    /// orchestrator notified with <c>status="expired"</c>, <c>reason="gateway_timeout"</c>.
    /// </description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="paymentId">The payment identifier to charge.</param>
    /// <param name="orderId">The order identifier associated with this payment.</param>
    /// <param name="amount">The amount to charge.</param>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <param name="gateway">The gateway client resolved from the background scope.</param>
    /// <param name="repository">The payment repository resolved from the background scope.</param>
    /// <param name="orchestrator">The orchestrator client resolved from the background scope.</param>
    /// <param name="ct">Cancellation token (must be <see cref="CancellationToken.None"/> for background tasks).</param>
    private async Task ChargeGatewayAsync(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        string currency,
        IPaymentGatewayClient gateway,
        IPaymentRepository repository,
        IOrchestratorClient orchestrator,
        CancellationToken ct)
    {
        logger.GatewayCallStartedInBackground(paymentId, orderId);

        try
        {
            await gateway.ChargeAsync(paymentId, orderId, amount, currency, ct);
            logger.GatewayCallSucceeded(paymentId, orderId);
            // Gateway accepted the request. The result will arrive via POST /payments/webhook.
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            // Distinguish between two distinct failure modes:
            //
            // CASE 1 — Gateway unreachable (connectivity failure — gateway never contacted):
            //   Inner exception is HttpRequestException or SocketException.
            //   Status → "failed", reason → "gateway_unavailable".
            //   Payment transitions to Failed.
            //
            // CASE 2 — Gateway reachable but no response within the timeout window:
            //   Inner exception is TaskCanceledException (Polly TimeoutRejectedException wraps it).
            //   Status → "expired", reason → "gateway_timeout".
            //   Payment transitions to Expired.
            bool isTimeout = ex.InnerException is TaskCanceledException
                             || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);

            string notificationStatus;
            string notificationReason;

            if (isTimeout)
            {
                logger.GatewayTimeout(paymentId, orderId);
                notificationStatus = "expired";
                notificationReason = "gateway_timeout";
            }
            else
            {
                logger.GatewayUnavailable(paymentId, orderId, ex.Message);
                notificationStatus = "failed";
                notificationReason = "gateway_unavailable";
            }

            // Update payment status and notify S3 so the saga can transition to Failed.
            try
            {
                var failedPayment = await repository.GetByIdAsync(paymentId, ct);
                if (failedPayment is not null && failedPayment.Status == Domain.Enums.PaymentStatus.Pending)
                {
                    if (isTimeout)
                        failedPayment.Expire();
                    else
                        failedPayment.Fail();

                    await repository.UpdateAsync(failedPayment, ct);

                    var notification = new PaymentProcessedNotification(
                        OrderId: orderId,
                        PaymentId: paymentId,
                        Status: notificationStatus,
                        Reason: notificationReason,
                        Amount: amount,
                        Currency: currency,
                        OccurredAt: DateTimeOffset.UtcNow);

                    await orchestrator.NotifyPaymentProcessedAsync(notification, ct);
                }
            }
            catch (Exception innerEx)
            {
                // The orchestrator notification also failed. Log and give up.
                // TODO: Replace fire-and-forget with Outbox Pattern to guarantee delivery.
                logger.LogError(innerEx,
                    "Failed to update payment or notify orchestrator after gateway failure for payment {PaymentId} order {OrderId}",
                    paymentId, orderId);
            }
        }
    }
}
