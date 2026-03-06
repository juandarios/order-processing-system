using OrderOrchestrator.Domain.Enums;

namespace OrderOrchestrator.Domain.Entities;

/// <summary>
/// Represents the orchestration state (saga) for a single order.
/// Persisted in the orchestrator-db and driven by the Stateless state machine.
/// </summary>
public class OrderSaga
{
    /// <summary>Gets the unique identifier of the saga.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets the associated order identifier.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Gets or sets the current state machine state.</summary>
    public OrderSagaStatus CurrentState { get; set; }

    /// <summary>Gets or sets the total order amount (orchestration context).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the payment identifier once payment is initiated.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Gets or sets when payment was initiated (used to calculate timeout).</summary>
    public DateTimeOffset? PaymentInitiatedAt { get; set; }

    /// <summary>Gets or sets the payment timeout (PaymentInitiatedAt + 5 minutes).</summary>
    public DateTimeOffset? TimeoutAt { get; set; }

    /// <summary>Gets or sets when the saga was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the saga was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
