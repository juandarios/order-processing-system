using Mediator;
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
/// 1. Checks whether a saga already exists for the order (idempotency guard).
/// 2. Creates the order saga in Pending state if it does not exist yet.
/// 3. Fires the appropriate state machine trigger (stock ok or no stock).
/// 4. If stock is validated, calls S2 to initiate payment.
/// Duplicate notifications for the same order are handled gracefully.
/// </summary>
public class ProcessStockValidatedCommandHandler(
    IOrderSagaRepository sagaRepository,
    IPaymentServiceClient paymentClient,
    ILogger<ProcessStockValidatedCommandHandler> logger)
    : ICommandHandler<ProcessStockValidatedCommand>
{
    /// <summary>
    /// Processes the stock validation result and drives the state machine.
    /// Idempotent: if a saga already exists for the given order, it is reused.
    /// </summary>
    /// <param name="request">The command containing the stock validation notification.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask<Unit> Handle(ProcessStockValidatedCommand request, CancellationToken ct)
    {
        var notification = request.Notification;

        // --- Idempotency check (application-level, first line of defence) ---
        var existing = await sagaRepository.GetByOrderIdAsync(notification.OrderId, ct);
        OrderSaga saga;

        if (existing is not null)
        {
            logger.DuplicateSagaDetected(notification.OrderId);
            saga = existing;
        }
        else
        {
            saga = new OrderSaga
            {
                Id = Uuid.NewSequential(),
                OrderId = notification.OrderId,
                CurrentState = OrderSagaStatus.Pending,
                TotalAmount = notification.TotalAmount,
                Currency = notification.Currency,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // ON CONFLICT DO NOTHING in AddAsync provides database-level idempotency
            // as a second line of defence against race conditions.
            await sagaRepository.AddAsync(saga, ct);

            // Always re-query after insert to ensure we have the persisted saga
            // (covers the unlikely race where another request won the conflict).
            saga = await sagaRepository.GetByOrderIdAsync(notification.OrderId, ct) ?? saga;

            logger.SagaCreated(saga.Id, saga.OrderId);
        }

        // If the saga is already past Pending (duplicate notification that was fully processed),
        // return success without re-driving the state machine.
        if (saga.CurrentState != OrderSagaStatus.Pending)
        {
            logger.DuplicateNotificationIgnored(notification.OrderId);
            return Unit.Value;
        }

        var machine = new OrderStateMachine(saga);

        if (notification.StockValidated)
        {
            machine.FireStockOk();
            await sagaRepository.UpdateAsync(saga, ct);
            logger.SagaTransitioned(saga.OrderId, saga.CurrentState.ToString());

            // Initiate payment immediately after stock confirmation.
            // Failures are swallowed here; the caller (controller) will still receive a success
            // response once the saga state is persisted. See Fix 3 for the full rationale.
            try
            {
                var paymentId = await paymentClient.InitiatePaymentAsync(
                    notification.OrderId, notification.TotalAmount, notification.Currency, ct);

                machine.FirePaymentInitiated(paymentId);
                await sagaRepository.UpdateAsync(saga, ct);
                logger.SagaTransitioned(saga.OrderId, saga.CurrentState.ToString());
            }
            catch (Exception ex)
            {
                // TODO: Implement Outbox Pattern to handle prolonged S2 unavailability.
                // Saga intentionally stays in StockValidated state for external recovery.
                logger.PaymentInitiationFailed(notification.OrderId, ex.Message);
            }
        }
        else
        {
            machine.FireNoStock();
            await sagaRepository.UpdateAsync(saga, ct);
            logger.SagaCancelled(saga.OrderId);
        }

        return Unit.Value;
    }
}
