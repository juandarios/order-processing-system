using OrderIntake.Domain.Exceptions;

namespace OrderIntake.Domain.ValueObjects;

/// <summary>
/// Represents a single line item in an order. Immutable value object.
/// </summary>
/// <param name="ProductId">Unique identifier of the product.</param>
/// <param name="ProductName">Display name of the product.</param>
/// <param name="Quantity">Number of units ordered (must be at least 1).</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public record OrderLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, string Currency)
{
    /// <summary>Gets the quantity ordered.</summary>
    public int Quantity { get; } = Quantity > 0
        ? Quantity : throw new DomainException($"Quantity must be positive. Received: {Quantity}");

    /// <summary>Gets the unit price.</summary>
    public decimal UnitPrice { get; } = UnitPrice >= 0
        ? UnitPrice : throw new DomainException($"UnitPrice cannot be negative. Received: {UnitPrice}");

    /// <summary>Gets the line total (unit price × quantity).</summary>
    public decimal LineTotal => UnitPrice * Quantity;
}
