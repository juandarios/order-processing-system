using Mediator;
using Microsoft.Extensions.Logging;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Application.Logging;
using OrderOrchestrator.Domain.Enums;
using OrderOrchestrator.Domain.Exceptions;
using OrderOrchestrator.Application.StateMachine;

namespace OrderOrchestrator.Application.Commands.ProcessPaymentProcessed;

/// <summary>
/// Handles <see cref="ProcessPaymentProcessedCommand"/>:
/// Retrieves the saga and fires the appropriate payment result trigger.
/// Duplicate notifications are detected and ignored gracefully.
/// </summary>
public class ProcessPaymentProcessedCommandHandler(
    IOrderSagaRepository sagaRepository,
    ILogger<ProcessPaymentProcessedCommandHandler> logger)
    : ICommandHandler<ProcessPaymentProcessedCommand>
{
    /// <summary>
    /// Processes the payment result and drives the saga to terminal state.
    /// Idempotent: if the saga is already in a terminal state, the notification is ignored.
    /// </summary>
    /// <param name="request">The command containing the payment processed notification.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask<Unit> Handle(ProcessPaymentProcessedCommand request, CancellationToken ct)
    {
        var notification = request.Notification;

        var saga = await sagaRepository.GetByOrderIdAsync(notification.OrderId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.OrderSaga), notification.OrderId);

        // Idempotency guard: if the saga is already in a terminal state, the notification is a
        // duplicate (e.g. S2 retried the callback). Return success without re-processing.
        if (saga.CurrentState != OrderSagaStatus.PaymentPending)
        {
            logger.DuplicateNotificationIgnored(notification.OrderId);
            return Unit.Value;
        }

        var machine = new OrderStateMachine(saga);

        switch (notification.Status.ToLowerInvariant())
        {
            case "approved":
                machine.FirePaymentApproved();
                logger.PaymentApproved(saga.OrderId);
                break;
            default:
                machine.FirePaymentFailed();
                // Log with reason when available so gateway-level failure causes
                // (gateway_unavailable, gateway_timeout, etc.) are visible in the orchestrator.
                if (!string.IsNullOrWhiteSpace(notification.Reason))
                    logger.PaymentFailedWithReason(saga.OrderId, notification.Status, notification.Reason);
                else
                    logger.PaymentFailed(saga.OrderId, notification.Status);
                break;
        }

        await sagaRepository.UpdateAsync(saga, ct);
        logger.SagaTransitioned(saga.OrderId, saga.CurrentState.ToString());

        return Unit.Value;
    }
}
