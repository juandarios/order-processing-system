using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Represents a payment in the system. Encapsulates the payment lifecycle rules.
/// </summary>
public class Payment
{
    /// <summary>Gets the unique identifier of the payment (UUID v7).</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the associated order identifier.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the current payment status.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Gets the payment amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the rejection reason (populated only when status is Rejected).</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Gets the raw gateway response for traceability.</summary>
    public string? GatewayResponse { get; private set; }

    /// <summary>Gets the UTC timestamp when the payment was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    private Payment() { }

    /// <summary>
    /// Creates a new payment in Pending status.
    /// </summary>
    /// <param name="id">Unique identifier (UUID v7).</param>
    /// <param name="orderId">Associated order identifier.</param>
    /// <param name="amount">Amount to charge.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <returns>A new <see cref="Payment"/> in Pending status.</returns>
    public static Payment Create(Guid id, Guid orderId, decimal amount, string currency)
    {
        return new Payment
        {
            Id = id,
            OrderId = orderId,
            Status = PaymentStatus.Pending,
            Amount = amount,
            Currency = currency,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Marks the payment as approved by the gateway.
    /// </summary>
    /// <param name="gatewayResponse">Raw gateway response.</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Pending status.</exception>
    public void Approve(string gatewayResponse)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot approve payment in status '{Status}'. Expected: Pending.");

        Status = PaymentStatus.Approved;
        GatewayResponse = gatewayResponse;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the payment as rejected by the gateway.
    /// </summary>
    /// <param name="reason">Rejection reason (e.g., "insufficient_funds").</param>
    /// <param name="gatewayResponse">Raw gateway response.</param>
    /// <exception cref="DomainException">Thrown when the payment is not in Pending status.</exception>
    public void Reject(string reason, string gatewayResponse)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot reject payment in status '{Status}'. Expected: Pending.");

        Status = PaymentStatus.Rejected;
        RejectionReason = reason;
        GatewayResponse = gatewayResponse;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the payment as expired (no gateway response received in time).
    /// Use this when the payment gateway was reached but did not respond within the allowed window.
    /// </summary>
    /// <exception cref="DomainException">Thrown when the payment is not in Pending status.</exception>
    public void Expire()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot expire payment in status '{Status}'. Expected: Pending.");

        Status = PaymentStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the payment as failed because the gateway was unreachable (never contacted).
    /// Use this when connectivity to the gateway could not be established, as opposed to
    /// <see cref="Expire"/> which applies when the gateway was reached but did not respond in time.
    /// </summary>
    /// <exception cref="DomainException">Thrown when the payment is not in Pending status.</exception>
    public void Fail()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot fail payment in status '{Status}'. Expected: Pending.");

        Status = PaymentStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
