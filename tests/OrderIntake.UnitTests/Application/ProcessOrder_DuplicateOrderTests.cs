using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderIntake.Application.Commands.ProcessOrder;
using OrderIntake.Application.Interfaces;
using OrderIntake.Domain.Entities;
using OrderIntake.Domain.Enums;
using OrderIntake.Domain.Exceptions;
using OrderIntake.Domain.ValueObjects;
using Shared.Events;
using Xunit;

namespace OrderIntake.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="ProcessOrderCommandHandler"/> focusing on duplicate-order
/// idempotency behaviour.
/// </summary>
public class ProcessOrder_DuplicateOrderTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IStockServiceClient _stockClient = Substitute.For<IStockServiceClient>();
    private readonly IOrchestratorClient _orchestratorClient = Substitute.For<IOrchestratorClient>();
    private readonly ILogger<ProcessOrderCommandHandler> _logger =
        Substitute.For<ILogger<ProcessOrderCommandHandler>>();

    private readonly ProcessOrderCommandHandler _handler;

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessOrder_DuplicateOrderTests"/>
    /// wiring up the handler with substituted dependencies.
    /// </summary>
    public ProcessOrder_DuplicateOrderTests()
    {
        _handler = new ProcessOrderCommandHandler(
            _orderRepository, _stockClient, _orchestratorClient, _logger);
    }

    /// <summary>
    /// When an order with the same orderId already exists in the repository, the handler must
    /// throw <see cref="DuplicateOrderException"/> so the Kafka consumer can route the message
    /// to the DLQ with error type <c>DuplicateOrder</c> and commit the offset.
    /// </summary>
    [Fact]
    public async Task ProcessOrder_WithDuplicateOrderId_ThrowsDuplicateOrderException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var existingOrder = Order.Create(
            id: orderId,
            customerId: Guid.NewGuid(),
            customerEmail: "existing@example.com",
            total: new Money(100m, "USD"),
            shippingAddress: new Address("123 Main St", "Springfield", "US", "12345"),
            lines: [new OrderLine(Guid.NewGuid(), "Widget", 1, 100m, "USD")]);

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(existingOrder);

        var @event = new OrderPlacedEvent(
            EventId: Guid.NewGuid(),
            EventType: "OrderPlaced",
            OccurredAt: DateTimeOffset.UtcNow,
            Version: 1,
            Payload: new OrderPlacedPayload(
                OrderId: orderId,
                CustomerId: Guid.NewGuid(),
                CustomerEmail: "duplicate@example.com",
                ShippingAddress: new ShippingAddressDto("123 Main St", "Springfield", "US", "12345"),
                Items: [new OrderItemDto(Guid.NewGuid(), "Widget", 1, 100m, "USD")],
                TotalAmount: 100m,
                Currency: "USD"));

        var command = new ProcessOrderCommand(@event);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert — DuplicateOrderException is thrown; no insert or update attempted
        await act.Should().ThrowAsync<DuplicateOrderException>();
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _stockClient.DidNotReceive()
            .IsAvailableAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _orchestratorClient.DidNotReceive()
            .NotifyStockValidatedAsync(Arg.Any<Shared.Contracts.StockValidatedNotification>(), Arg.Any<CancellationToken>());
    }
}
