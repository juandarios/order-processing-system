namespace PaymentService.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a payment.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Payment request received and being processed.</summary>
    Pending,

    /// <summary>Payment was approved by the gateway.</summary>
    Approved,

    /// <summary>Payment was rejected by the gateway (e.g., insufficient funds).</summary>
    Rejected,

    /// <summary>Payment expired without a gateway response within the allowed time window.</summary>
    Expired,

    /// <summary>
    /// Payment could not be initiated because the gateway was unreachable.
    /// Distinct from <see cref="Expired"/>: the gateway was never contacted at all.
    /// </summary>
    Failed
}
