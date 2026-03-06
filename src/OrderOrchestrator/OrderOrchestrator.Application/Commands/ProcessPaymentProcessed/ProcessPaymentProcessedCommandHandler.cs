using MediatR;
using Microsoft.Extensions.Logging;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Application.Logging;
using OrderOrchestrator.Domain.Exceptions;
using OrderOrchestrator.Application.StateMachine;

namespace OrderOrchestrator.Application.Commands.ProcessPaymentProcessed;

/// <summary>
/// Handles <see cref="ProcessPaymentProcessedCommand"/>:
/// Retrieves the saga and fires the appropriate payment result trigger.
/// </summary>
public class ProcessPaymentProcessedCommandHandler(
    IOrderSagaRepository sagaRepository,
    ILogger<ProcessPaymentProcessedCommandHandler> logger)
    : IRequestHandler<ProcessPaymentProcessedCommand>
{
    /// <summary>
    /// Processes the payment result and drives the saga to terminal state.
    /// </summary>
    public async Task Handle(ProcessPaymentProcessedCommand request, CancellationToken ct)
    {
        var notification = request.Notification;

        var saga = await sagaRepository.GetByOrderIdAsync(notification.OrderId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.OrderSaga), notification.OrderId);

        var machine = new OrderStateMachine(saga);

        switch (notification.Status.ToLowerInvariant())
        {
            case "approved":
                machine.FirePaymentApproved();
                logger.PaymentApproved(saga.OrderId);
                break;
            default:
                machine.FirePaymentFailed();
                logger.PaymentFailed(saga.OrderId, notification.Status);
                break;
        }

        await sagaRepository.UpdateAsync(saga, ct);
        logger.SagaTransitioned(saga.OrderId, saga.CurrentState.ToString());
    }
}
