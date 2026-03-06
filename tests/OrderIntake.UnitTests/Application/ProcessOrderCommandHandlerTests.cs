using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderIntake.Application.Commands.ProcessOrder;
using OrderIntake.Application.Interfaces;
using OrderIntake.Domain.Entities;
using OrderIntake.Domain.Enums;
using Shared.Contracts;
using Shared.Events;
using Xunit;

namespace OrderIntake.UnitTests.Application;

public class ProcessOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IStockServiceClient _stockClient = Substitute.For<IStockServiceClient>();
    private readonly IOrchestratorClient _orchestratorClient = Substitute.For<IOrchestratorClient>();
    private readonly ILogger<ProcessOrderCommandHandler> _logger =
        Substitute.For<ILogger<ProcessOrderCommandHandler>>();

    private readonly ProcessOrderCommandHandler _handler;

    public ProcessOrderCommandHandlerTests()
    {
        _handler = new ProcessOrderCommandHandler(
            _orderRepository, _stockClient, _orchestratorClient, _logger);
    }

    private static OrderPlacedEvent CreateOrderPlacedEvent() => new(
        EventId: Guid.NewGuid(),
        EventType: "OrderPlaced",
        OccurredAt: DateTimeOffset.UtcNow,
        Version: 1,
        Payload: new OrderPlacedPayload(
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "test@example.com",
            ShippingAddress: new ShippingAddressDto("123 Main St", "Springfield", "US", "12345"),
            Items: [new OrderItemDto(Guid.NewGuid(), "Widget", 2, 50m, "USD")],
            TotalAmount: 100m,
            Currency: "USD"));

    [Fact]
    public async Task Handle_WithStockAvailable_ShouldValidateOrderAndNotifyOrchestrator()
    {
        // Arrange
        var @event = CreateOrderPlacedEvent();
        _stockClient.IsAvailableAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);

        Order? capturedOrder = null;
        await _orderRepository.AddAsync(Arg.Do<Order>(o => capturedOrder = o), Arg.Any<CancellationToken>());
        await _orderRepository.UpdateAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());

        StockValidatedNotification? capturedNotification = null;
        await _orchestratorClient.NotifyStockValidatedAsync(
            Arg.Do<StockValidatedNotification>(n => capturedNotification = n),
            Arg.Any<CancellationToken>());

        // Act
        await _handler.Handle(new ProcessOrderCommand(@event), CancellationToken.None);

        // Assert
        await _orderRepository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).UpdateAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _orchestratorClient.Received(1).NotifyStockValidatedAsync(
            Arg.Any<StockValidatedNotification>(), Arg.Any<CancellationToken>());

        capturedNotification!.StockValidated.Should().BeTrue();
        capturedNotification.OrderId.Should().Be(@event.Payload.OrderId);
    }

    [Fact]
    public async Task Handle_WithStockUnavailable_ShouldCancelOrderAndNotifyOrchestrator()
    {
        // Arrange
        var @event = CreateOrderPlacedEvent();
        _stockClient.IsAvailableAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        StockValidatedNotification? capturedNotification = null;
        await _orchestratorClient.NotifyStockValidatedAsync(
            Arg.Do<StockValidatedNotification>(n => capturedNotification = n),
            Arg.Any<CancellationToken>());

        // Act
        await _handler.Handle(new ProcessOrderCommand(@event), CancellationToken.None);

        // Assert
        capturedNotification!.StockValidated.Should().BeFalse();
        capturedNotification.Items.Should().AllSatisfy(i => i.StockAvailable.Should().BeFalse());
    }
}
