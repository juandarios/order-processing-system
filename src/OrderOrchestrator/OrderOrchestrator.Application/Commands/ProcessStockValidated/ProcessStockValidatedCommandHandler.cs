using MediatR;
using Microsoft.Extensions.Logging;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Application.Logging;
using OrderOrchestrator.Domain.Entities;
using OrderOrchestrator.Domain.Enums;
using OrderOrchestrator.Application.StateMachine;
using UUIDNext;

namespace OrderOrchestrator.Application.Commands.ProcessStockValidated;

/// <summary>
/// Handles <see cref="ProcessStockValidatedCommand"/>:
/// 1. Creates the order saga in Pending state.
/// 2. Fires the appropriate state machine trigger (stock ok or no stock).
/// 3. If stock is validated, calls S2 to initiate payment.
/// </summary>
public class ProcessStockValidatedCommandHandler(
    IOrderSagaRepository sagaRepository,
    IPaymentServiceClient paymentClient,
    ILogger<ProcessStockValidatedCommandHandler> logger)
    : IRequestHandler<ProcessStockValidatedCommand>
{
    /// <summary>
    /// Processes the stock validation result and drives the state machine.
    /// </summary>
    public async Task Handle(ProcessStockValidatedCommand request, CancellationToken ct)
    {
        var notification = request.Notification;

        var saga = new OrderSaga
        {
            Id = Uuid.NewSequential(),
            OrderId = notification.OrderId,
            CurrentState = OrderSagaStatus.Pending,
            TotalAmount = notification.TotalAmount,
            Currency = notification.Currency,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await sagaRepository.AddAsync(saga, ct);
        logger.SagaCreated(saga.Id, saga.OrderId);

        var machine = new OrderStateMachine(saga);

        if (notification.StockValidated)
        {
            machine.FireStockOk();
            await sagaRepository.UpdateAsync(saga, ct);
            logger.SagaTransitioned(saga.OrderId, saga.CurrentState.ToString());

            // Initiate payment immediately after stock confirmation
            var paymentId = await paymentClient.InitiatePaymentAsync(
                notification.OrderId, notification.TotalAmount, notification.Currency, ct);

            machine.FirePaymentInitiated(paymentId);
            await sagaRepository.UpdateAsync(saga, ct);
            logger.SagaTransitioned(saga.OrderId, saga.CurrentState.ToString());
        }
        else
        {
            machine.FireNoStock();
            await sagaRepository.UpdateAsync(saga, ct);
            logger.SagaCancelled(saga.OrderId);
        }
    }
}
