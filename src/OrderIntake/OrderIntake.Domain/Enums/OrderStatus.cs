namespace OrderIntake.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an order in the intake service.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order has been received and is awaiting stock validation.</summary>
    Pending,

    /// <summary>All items in the order have been confirmed as in stock.</summary>
    StockValidated,

    /// <summary>Order was cancelled due to insufficient stock.</summary>
    Cancelled,

    /// <summary>Payment has been initiated and is pending confirmation.</summary>
    PaymentPending,

    /// <summary>Payment has been confirmed and the order is complete.</summary>
    PaymentConfirmed,

    /// <summary>Order processing failed (payment rejected, expired, or timeout).</summary>
    Failed
}
