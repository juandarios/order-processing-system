namespace Shared.Events;

/// <summary>
/// Kafka event envelope for an order placed in the system.
/// </summary>
/// <param name="EventId">Unique identifier for this event (UUID v7).</param>
/// <param name="EventType">The type of the event.</param>
/// <param name="OccurredAt">UTC timestamp when the event occurred.</param>
/// <param name="Version">Schema version of the event.</param>
/// <param name="Payload">The order placed payload.</param>
public record OrderPlacedEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    int Version,
    OrderPlacedPayload Payload);

/// <summary>
/// Payload for the OrderPlaced event.
/// </summary>
/// <param name="OrderId">Unique identifier of the order (UUID v7).</param>
/// <param name="CustomerId">Unique identifier of the customer.</param>
/// <param name="CustomerEmail">Email address of the customer.</param>
/// <param name="ShippingAddress">Shipping address for the order.</param>
/// <param name="Items">List of ordered items.</param>
/// <param name="TotalAmount">Total order amount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public record OrderPlacedPayload(
    Guid OrderId,
    Guid CustomerId,
    string CustomerEmail,
    ShippingAddressDto ShippingAddress,
    List<OrderItemDto> Items,
    decimal TotalAmount,
    string Currency);

/// <summary>
/// Shipping address data transfer object.
/// </summary>
public record ShippingAddressDto(
    string Street,
    string City,
    string Country,
    string ZipCode);

/// <summary>
/// Order item data transfer object.
/// </summary>
public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Currency);
