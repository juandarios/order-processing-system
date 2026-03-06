using OrderIntake.Domain.Enums;
using OrderIntake.Domain.Events;
using OrderIntake.Domain.Exceptions;
using OrderIntake.Domain.ValueObjects;

namespace OrderIntake.Domain.Entities;

/// <summary>
/// Aggregate root representing a customer order in the system.
/// Encapsulates all business rules related to order lifecycle transitions.
/// </summary>
public class Order
{
    private readonly List<OrderLine> _lines = new();
    private readonly List<object> _domainEvents = new();

    /// <summary>Gets the unique identifier of the order (UUID v7).</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the current status of the order.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Gets the unique identifier of the customer.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Gets the customer email address.</summary>
    public string CustomerEmail { get; private set; } = string.Empty;

    /// <summary>Gets the total order amount.</summary>
    public Money Total { get; private set; } = null!;

    /// <summary>Gets the shipping address.</summary>
    public Address ShippingAddress { get; private set; } = null!;

    /// <summary>Gets the order lines (read-only view).</summary>
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>Gets the UTC timestamp when the order was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Gets domain events raised during this operation (cleared after retrieval).</summary>
    public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Clears all pending domain events.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // EF Core requires parameterless constructor
    private Order() { }

    /// <summary>
    /// Creates a new order in Pending status.
    /// </summary>
    /// <param name="id">Unique identifier (UUID v7).</param>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="customerEmail">Customer email.</param>
    /// <param name="total">Total order amount.</param>
    /// <param name="shippingAddress">Shipping address.</param>
    /// <param name="lines">Order line items.</param>
    /// <returns>A new <see cref="Order"/> in Pending status.</returns>
    /// <exception cref="DomainException">Thrown if lines are empty.</exception>
    public static Order Create(
        Guid id,
        Guid customerId,
        string customerEmail,
        Money total,
        Address shippingAddress,
        IEnumerable<OrderLine> lines)
    {
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            Total = total,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var lineList = lines.ToList();
        if (lineList.Count == 0)
            throw new DomainException("An order must have at least one line.");

        order._lines.AddRange(lineList);
        return order;
    }

    /// <summary>
    /// Transitions the order to StockValidated status after successful stock check.
    /// </summary>
    /// <exception cref="DomainException">Thrown when the order is not in Pending status.</exception>
    public void MarkStockValidated()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException($"Cannot validate stock for order in status '{Status}'. Expected: Pending.");

        Status = OrderStatus.StockValidated;
        UpdatedAt = DateTimeOffset.UtcNow;
        _domainEvents.Add(new OrderValidatedEvent(Id, UpdatedAt));
    }

    /// <summary>
    /// Transitions the order to Cancelled status due to insufficient stock.
    /// </summary>
    /// <exception cref="DomainException">Thrown when the order is not in Pending status.</exception>
    public void Cancel()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException($"Cannot cancel order in status '{Status}'. Expected: Pending.");

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
