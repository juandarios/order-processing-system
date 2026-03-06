using OrderIntake.Domain.Exceptions;

namespace OrderIntake.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value with an amount and currency. Immutable value object.
/// </summary>
/// <param name="Amount">The monetary amount (must be non-negative).</param>
/// <param name="Currency">ISO 4217 currency code (e.g., "USD").</param>
public record Money(decimal Amount, string Currency)
{
    /// <summary>
    /// Gets the monetary amount.
    /// </summary>
    public decimal Amount { get; } = Amount >= 0
        ? Amount
        : throw new DomainException($"Amount cannot be negative. Received: {Amount}");

    /// <summary>
    /// Gets the ISO 4217 currency code.
    /// </summary>
    public string Currency { get; } = !string.IsNullOrWhiteSpace(Currency)
        ? Currency.ToUpperInvariant()
        : throw new DomainException("Currency cannot be empty.");
}
