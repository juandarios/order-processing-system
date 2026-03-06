namespace Shared.Contracts;

/// <summary>
/// Notification sent from Payment Service (S2) to Order Orchestrator (S3)
/// after a payment is processed by the gateway.
/// </summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="PaymentId">Unique identifier of the payment.</param>
/// <param name="Status">Payment status: approved, rejected, or expired.</param>
/// <param name="Reason">Rejection reason, populated only when status is rejected.</param>
/// <param name="Amount">Payment amount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="OccurredAt">UTC timestamp when the payment was processed.</param>
public record PaymentProcessedNotification(
    Guid OrderId,
    Guid PaymentId,
    string Status,
    string? Reason,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt);
