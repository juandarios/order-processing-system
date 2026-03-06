using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Logging;
using PaymentService.Domain.Exceptions;
using Shared.Contracts;

namespace PaymentService.Application.Commands.ProcessWebhook;

/// <summary>
/// Handles <see cref="ProcessWebhookCommand"/>:
/// 1. Retrieves and updates the payment based on gateway result.
/// 2. Notifies the orchestrator with the payment outcome.
/// </summary>
public class ProcessWebhookCommandHandler(
    IPaymentRepository paymentRepository,
    IOrchestratorClient orchestratorClient,
    ILogger<ProcessWebhookCommandHandler> logger)
    : IRequestHandler<ProcessWebhookCommand>
{
    /// <summary>
    /// Processes the gateway webhook result.
    /// </summary>
    /// <param name="request">The command with payment result details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="NotFoundException">Thrown when the payment is not found.</exception>
    public async Task Handle(ProcessWebhookCommand request, CancellationToken ct)
    {
        var payment = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Payment), request.OrderId);

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
                throw new DomainException($"Unknown payment status: {request.Status}");
        }

        await paymentRepository.UpdateAsync(payment, ct);

        var notification = new PaymentProcessedNotification(
            OrderId: request.OrderId,
            PaymentId: payment.Id,
            Status: request.Status,
            Reason: request.Reason,
            Amount: request.Amount,
            Currency: request.Currency,
            OccurredAt: DateTimeOffset.UtcNow);

        await orchestratorClient.NotifyPaymentProcessedAsync(notification, ct);
        logger.OrchestratorNotified(payment.Id, request.OrderId, request.Status);
    }
}
