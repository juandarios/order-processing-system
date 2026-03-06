namespace OrderOrchestrator.Domain.Enums;

/// <summary>
/// Represents the state machine states for an order saga in the orchestrator.
/// </summary>
public enum OrderSagaStatus
{
    /// <summary>Order received, waiting for stock validation from S1.</summary>
    Pending,

    /// <summary>Stock confirmed, S3 is about to call S2 for payment.</summary>
    StockValidated,

    /// <summary>Order cancelled due to insufficient stock (terminal state).</summary>
    Cancelled,

    /// <summary>Payment initiated, waiting for gateway response via S2 webhook.</summary>
    PaymentPending,

    /// <summary>Payment approved and order complete (terminal state).</summary>
    PaymentConfirmed,

    /// <summary>Payment rejected, expired, or timed out (terminal state).</summary>
    Failed
}
