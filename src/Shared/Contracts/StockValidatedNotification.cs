namespace Shared.Contracts;

/// <summary>
/// Notification sent from Order Intake (S1) to Order Orchestrator (S3)
/// after stock validation is completed.
/// </summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="StockValidated">True if all items are in stock; false otherwise.</param>
/// <param name="TotalAmount">Total order amount for orchestration context.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Items">Stock validation result per item.</param>
/// <param name="OccurredAt">UTC timestamp when validation occurred.</param>
public record StockValidatedNotification(
    Guid OrderId,
    bool StockValidated,
    decimal TotalAmount,
    string Currency,
    List<StockItemValidationResult> Items,
    DateTimeOffset OccurredAt);

/// <summary>
/// Stock validation result for a single order item.
/// </summary>
/// <param name="ProductId">Unique identifier of the product.</param>
/// <param name="QuantityRequested">Quantity requested in the order.</param>
/// <param name="StockAvailable">True if stock is available for the requested quantity.</param>
public record StockItemValidationResult(
    Guid ProductId,
    int QuantityRequested,
    bool StockAvailable);
