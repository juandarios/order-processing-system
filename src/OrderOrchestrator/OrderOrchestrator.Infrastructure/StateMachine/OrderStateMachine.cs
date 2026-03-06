using OrderOrchestrator.Domain.Entities;
using OrderOrchestrator.Domain.Enums;
using Stateless;

namespace OrderOrchestrator.Infrastructure.StateMachine;

/// <summary>
/// Stateless state machine for the order saga lifecycle.
/// Encapsulates all valid state transitions for an <see cref="OrderSaga"/>.
/// </summary>
public class OrderStateMachine
{
    private readonly StateMachine<OrderSagaStatus, SagaTrigger> _machine;
    private readonly OrderSaga _saga;

    private static readonly TimeSpan PaymentTimeout = TimeSpan.FromMinutes(5);

    private readonly StateMachine<OrderSagaStatus, SagaTrigger>.TriggerWithParameters<Guid>
        _paymentInitiatedTrigger;

    /// <summary>
    /// Initializes the state machine bound to the given saga.
    /// </summary>
    /// <param name="saga">The saga whose state the machine manages.</param>
    public OrderStateMachine(OrderSaga saga)
    {
        _saga = saga;
        _machine = new StateMachine<OrderSagaStatus, SagaTrigger>(
            () => _saga.CurrentState,
            state =>
            {
                _saga.CurrentState = state;
                _saga.UpdatedAt = DateTimeOffset.UtcNow;
            });

        _paymentInitiatedTrigger = _machine.SetTriggerParameters<Guid>(SagaTrigger.PaymentInitiated);

        _machine.Configure(OrderSagaStatus.Pending)
            .Permit(SagaTrigger.StockOk, OrderSagaStatus.StockValidated)
            .Permit(SagaTrigger.NoStock, OrderSagaStatus.Cancelled);

        _machine.Configure(OrderSagaStatus.StockValidated)
            .Permit(SagaTrigger.PaymentInitiated, OrderSagaStatus.PaymentPending);

        _machine.Configure(OrderSagaStatus.PaymentPending)
            .Permit(SagaTrigger.PaymentApproved, OrderSagaStatus.PaymentConfirmed)
            .Permit(SagaTrigger.PaymentFailed, OrderSagaStatus.Failed)
            .Permit(SagaTrigger.Timeout, OrderSagaStatus.Failed);

        // Terminal states: Cancelled, PaymentConfirmed, Failed — no outgoing transitions
        _machine.Configure(OrderSagaStatus.Cancelled);
        _machine.Configure(OrderSagaStatus.PaymentConfirmed);
        _machine.Configure(OrderSagaStatus.Failed);
    }

    /// <summary>Fires the stock ok trigger, transitioning from Pending to StockValidated.</summary>
    public void FireStockOk() => _machine.Fire(SagaTrigger.StockOk);

    /// <summary>Fires the no-stock trigger, transitioning from Pending to Cancelled.</summary>
    public void FireNoStock() => _machine.Fire(SagaTrigger.NoStock);

    /// <summary>
    /// Fires the payment initiated trigger, transitioning from StockValidated to PaymentPending.
    /// Sets payment-related saga fields.
    /// </summary>
    /// <param name="paymentId">The payment identifier returned by S2.</param>
    public void FirePaymentInitiated(Guid paymentId)
    {
        _machine.Fire(_paymentInitiatedTrigger, paymentId);
        _saga.PaymentId = paymentId;
        _saga.PaymentInitiatedAt = DateTimeOffset.UtcNow;
        _saga.TimeoutAt = DateTimeOffset.UtcNow + PaymentTimeout;
    }

    /// <summary>Fires the payment approved trigger, transitioning to PaymentConfirmed.</summary>
    public void FirePaymentApproved() => _machine.Fire(SagaTrigger.PaymentApproved);

    /// <summary>Fires the payment failed trigger, transitioning to Failed.</summary>
    public void FirePaymentFailed() => _machine.Fire(SagaTrigger.PaymentFailed);

    /// <summary>Fires the timeout trigger, transitioning from PaymentPending to Failed.</summary>
    public void FireTimeout() => _machine.Fire(SagaTrigger.Timeout);
}

/// <summary>
/// State machine triggers for the order saga.
/// </summary>
public enum SagaTrigger
{
    /// <summary>Stock is available for all items.</summary>
    StockOk,

    /// <summary>Stock is insufficient for one or more items.</summary>
    NoStock,

    /// <summary>Payment request was successfully sent to S2.</summary>
    PaymentInitiated,

    /// <summary>Payment was approved by the gateway.</summary>
    PaymentApproved,

    /// <summary>Payment was rejected or expired by the gateway.</summary>
    PaymentFailed,

    /// <summary>Orchestrator timeout expired while waiting for payment.</summary>
    Timeout
}
