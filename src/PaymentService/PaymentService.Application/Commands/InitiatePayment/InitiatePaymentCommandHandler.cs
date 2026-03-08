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
/// scope is not held open. If the gateway call fails after Polly exhausts all retries, the payment
/// is marked as <c>Expired</c> and S3 is notified of the failure — all without affecting the
/// already-returned 202 response.
/// </para>
/// </summary>
public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGatewayClient gatewayClient,
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
        // The gateway call runs in a background task with its own DI scope so the
        // current HTTP request scope is not held open.
        //
        // TODO: Replace fire-and-forget with Outbox Pattern to guarantee delivery.
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var backgroundGateway = scope.ServiceProvider.GetRequiredService<IPaymentGatewayClient>();
            var backgroundRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
            var backgroundOrchestrator = scope.ServiceProvider.GetRequiredService<IOrchestratorClient>();

            logger.GatewayCallStartedInBackground(resolvedId);

            try
            {
                await backgroundGateway.ChargeAsync(
                    resolvedId, request.OrderId, request.Amount, request.Currency,
                    CancellationToken.None);

                logger.GatewayCallSucceeded(resolvedId);
                // Gateway accepted the request. The result will arrive via POST /payments/webhook.
            }
            catch (PaymentGatewayUnavailableException ex)
            {
                logger.GatewayCallFailed(resolvedId, ex.Message);

                // Update payment to Expired and notify S3 so the saga can transition to Failed.
                try
                {
                    var failedPayment = await backgroundRepository.GetByIdAsync(resolvedId, CancellationToken.None);
                    if (failedPayment is not null && failedPayment.Status == Domain.Enums.PaymentStatus.Pending)
                    {
                        failedPayment.Expire();
                        await backgroundRepository.UpdateAsync(failedPayment, CancellationToken.None);

                        var notification = new PaymentProcessedNotification(
                            OrderId: request.OrderId,
                            PaymentId: resolvedId,
                            Status: "expired",
                            Reason: "gateway_unavailable",
                            Amount: request.Amount,
                            Currency: request.Currency,
                            OccurredAt: DateTimeOffset.UtcNow);

                        await backgroundOrchestrator.NotifyPaymentProcessedAsync(
                            notification, CancellationToken.None);
                    }
                }
                catch (Exception innerEx)
                {
                    // The orchestrator notification also failed. Log and give up.
                    // TODO: Replace fire-and-forget with Outbox Pattern to guarantee delivery.
                    logger.LogError(innerEx,
                        "Failed to update payment or notify orchestrator after gateway failure for payment {PaymentId}",
                        resolvedId);
                }
            }
        }, CancellationToken.None); // CancellationToken.None: background work must not be tied to the HTTP request lifecycle.

        return resolvedId;
    }
}
