using Mediator;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Logging;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;
using Shared.Contracts;

namespace PaymentService.Application.Commands.ProcessWebhook;

/// <summary>
/// Handles <see cref="ProcessWebhookCommand"/>:
/// 1. Retrieves and updates the payment based on gateway result.
/// 2. Notifies the orchestrator with the payment outcome.
/// Duplicate webhook notifications for a payment already in a terminal state are ignored gracefully.
/// </summary>
public class ProcessWebhookCommandHandler(
    IPaymentRepository paymentRepository,
    IOrchestratorClient orchestratorClient,
    ILogger<ProcessWebhookCommandHandler> logger)
    : ICommandHandler<ProcessWebhookCommand>
{
    /// <summary>
    /// Processes the gateway webhook result.
    /// Idempotent: if the payment is already in a terminal state (<c>Approved</c>, <c>Rejected</c>,
    /// or <c>Expired</c>), the webhook is ignored gracefully and success is returned to the caller.
    /// </summary>
    /// <param name="request">The command with payment result details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit value indicating successful (or already-processed) handling.</returns>
    /// <exception cref="NotFoundException">Thrown when the payment is not found.</exception>
    public async ValueTask<Unit> Handle(ProcessWebhookCommand request, CancellationToken ct)
    {
        var payment = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Payment), request.OrderId);

        // Idempotency guard: if the payment is already in a terminal state, the webhook is a
        // duplicate (e.g. the gateway retried the callback). Return success without re-processing.
        if (payment.Status != PaymentStatus.Pending)
        {
            logger.DuplicateWebhookIgnored(payment.Id);
            return Unit.Value;
        }

        switch (request.Status.ToLowerInvariant())
        {
            case "approved":
                payment.Approve(request.Status);
                logger.PaymentApproved(payment.Id, request.OrderId);
                break;
            case "rejected":
                payment.Reject(request.Reason ?? "unknown", request.Status);
                logger.PaymentRejected(payment.Id, request.OrderId, request.Reason);
                break;
            case "expired":
                payment.Expire();
                logger.PaymentExpired(payment.Id, request.OrderId);
                break;
            default:
                throw new Domain.Exceptions.DomainException($"Unknown payment status: {request.Status}");
        }

        // Persist the updated payment status BEFORE notifying S3.
        // S2 must respond 200 OK to the gateway regardless of downstream orchestrator availability.
        await paymentRepository.UpdateAsync(payment, ct);

        var notification = new PaymentProcessedNotification(
            OrderId: request.OrderId,
            PaymentId: payment.Id,
            Status: request.Status,
            Reason: request.Reason,
            Amount: request.Amount,
            Currency: request.Currency,
            OccurredAt: DateTimeOffset.UtcNow);

        // Polly resilience on orchestratorClient handles retries internally.
        // If all retries are exhausted the exception is caught here so that the
        // gateway always receives 200 OK — the webhook callback must never fail
        // due to orchestrator unavailability.
        // TODO: Replace fire-and-forget with Outbox Pattern to guarantee delivery.
        try
        {
            await orchestratorClient.NotifyPaymentProcessedAsync(notification, ct);
            logger.OrchestratorNotified(payment.Id, request.OrderId, request.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to notify orchestrator for payment {PaymentId} order {OrderId}. " +
                "Payment status was updated successfully. Orchestrator notification will be retried on next webhook delivery.",
                payment.Id, request.OrderId);
            // Do not re-throw: the gateway already received its result; S2's job is done.
        }

        return Unit.Value;
    }
}
