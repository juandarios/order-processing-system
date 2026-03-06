namespace Shared.Contracts;

/// <summary>
/// Request sent from Order Orchestrator (S3) to Payment Service (S2)
/// to initiate payment processing for an order.
/// </summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public record InitiatePaymentRequest(
    Guid OrderId,
    decimal Amount,
    string Currency);
