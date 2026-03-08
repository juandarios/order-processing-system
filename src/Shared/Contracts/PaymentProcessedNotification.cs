namespace Shared.Contracts;

/// <summary>
/// Notification sent from Payment Service (S2) to Order Orchestrator (S3)
/// after a payment is processed by the gateway.
/// </summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="PaymentId">Unique identifier of the payment.</param>
/// <param name="Status">
/// Payment outcome status. Valid values:
/// <list type="bullet">
/// <item><term>approved</term><description>Gateway approved the payment.</description></item>
/// <item><term>rejected</term><description>Gateway rejected the payment (e.g. insufficient funds).</description></item>
/// <item><term>expired</term><description>Gateway was reachable but did not respond within the timeout window.</description></item>
/// <item><term>failed</term><description>Gateway was unreachable — connectivity failure, no response ever received.</description></item>
/// </list>
/// </param>
/// <param name="Reason">
/// Contextual reason code. Populated for non-approved statuses:
/// <list type="bullet">
/// <item><term>insufficient_funds</term><description>Rejection: card had insufficient funds.</description></item>
/// <item><term>card_expired</term><description>Rejection: card has expired.</description></item>
/// <item><term>gateway_timeout</term><description>Gateway was reached but timed out.</description></item>
/// <item><term>gateway_unavailable</term><description>Gateway was not reachable at all.</description></item>
/// </list>
/// </param>
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
