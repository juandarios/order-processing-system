using Microsoft.AspNetCore.Mvc;
using OrderProducer.Kafka;
using Shared.Events;
using UUIDNext;

namespace OrderProducer.Controllers;

/// <summary>
/// REST endpoint for placing new orders. Publishes the order to the Kafka order-placed topic.
/// </summary>
[ApiController]
[Route("orders")]
public class OrdersController(
    KafkaProducerService producer,
    ILogger<OrdersController> logger) : ControllerBase
{
    /// <summary>
    /// Publishes a new order to the Kafka topic.
    /// </summary>
    /// <param name="request">The order details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>202 Accepted with the generated order ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var orderId = Uuid.NewSequential();
        var eventId = Uuid.NewSequential();

        var @event = new OrderPlacedEvent(
            EventId: eventId,
            EventType: "OrderPlaced",
            OccurredAt: DateTimeOffset.UtcNow,
            Version: 1,
            Payload: new OrderPlacedPayload(
                OrderId: orderId,
                CustomerId: request.CustomerId,
                CustomerEmail: request.CustomerEmail,
                ShippingAddress: new ShippingAddressDto(
                    request.ShippingAddress.Street,
                    request.ShippingAddress.City,
                    request.ShippingAddress.Country,
                    request.ShippingAddress.ZipCode),
                Items: request.Items.Select(i => new OrderItemDto(
                    i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Currency)).ToList(),
                TotalAmount: request.Items.Sum(i => i.UnitPrice * i.Quantity),
                Currency: request.Items.First().Currency));

        await producer.PublishAsync(@event, ct);

        logger.LogInformation("Order {OrderId} published to Kafka", orderId);

        return Accepted(new CreateOrderResponse(orderId, "Published"));
    }
}

/// <summary>
/// Request payload for creating a new order.
/// </summary>
public record CreateOrderRequest(
    Guid CustomerId,
    string CustomerEmail,
    CreateOrderAddressRequest ShippingAddress,
    List<CreateOrderItemRequest> Items);

/// <summary>
/// Shipping address for the order request.
/// </summary>
public record CreateOrderAddressRequest(
    string Street,
    string City,
    string Country,
    string ZipCode);

/// <summary>
/// Order item for the order request.
/// </summary>
public record CreateOrderItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Currency);

/// <summary>
/// Response returned after successfully publishing an order.
/// </summary>
public record CreateOrderResponse(Guid OrderId, string Status);
