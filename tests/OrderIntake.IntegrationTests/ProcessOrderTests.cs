using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Application.Commands.ProcessOrder;
using OrderIntake.Domain.Enums;
using OrderIntake.IntegrationTests.Infrastructure;
using Shared.Events;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace OrderIntake.IntegrationTests;

public class ProcessOrderTests : IClassFixture<OrderIntakeWebAppFactory>
{
    private readonly OrderIntakeWebAppFactory _factory;

    public ProcessOrderTests(OrderIntakeWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessOrder_WithStockAvailable_ShouldPersistOrderAndNotifyOrchestrator()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _factory.StockMock
            .Given(Request.Create().WithPath("/stock/availability").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        _factory.OrchestratorMock
            .Given(Request.Create().WithPath("/orchestrator/orders/stock-validated").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var command = BuildProcessOrderCommand(orderId, productId, allInStock: true);

        // Act
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command);

        // Assert — retrieve order via HTTP to confirm persistence
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/orders/{orderId}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.OrderId.Should().Be(orderId);
        body.Status.Should().Be(OrderStatus.StockValidated.ToString());

        // Confirm orchestrator was called at least once for this saga type
        _factory.OrchestratorMock.LogEntries.Should().Contain(e =>
            e.RequestMessage.Path == "/orchestrator/orders/stock-validated");
    }

    [Fact]
    public async Task ProcessOrder_WithStockUnavailable_ShouldCancelOrder()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _factory.StockMock
            .Given(Request.Create().WithPath("/stock/availability").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(409));

        _factory.OrchestratorMock
            .Given(Request.Create().WithPath("/orchestrator/orders/stock-validated").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var command = BuildProcessOrderCommand(orderId, productId, allInStock: false);

        // Act
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command);

        // Assert — order should be Cancelled
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/orders/{orderId}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.Status.Should().Be(OrderStatus.Cancelled.ToString());
    }

    private static ProcessOrderCommand BuildProcessOrderCommand(
        Guid orderId, Guid productId, bool allInStock)
    {
        var evt = new OrderPlacedEvent(
            EventId: Guid.NewGuid(),
            EventType: "OrderPlaced",
            OccurredAt: DateTimeOffset.UtcNow,
            Version: 1,
            Payload: new OrderPlacedPayload(
                OrderId: orderId,
                CustomerId: Guid.NewGuid(),
                CustomerEmail: "test@example.com",
                ShippingAddress: new ShippingAddressDto("123 Main St", "Springfield", "US", "12345"),
                Items:
                [
                    new OrderItemDto(productId, "Widget", 2, 49.99m, "USD")
                ],
                TotalAmount: 99.98m,
                Currency: "USD"));

        return new ProcessOrderCommand(evt);
    }

    private record OrderResponse(Guid OrderId, string Status, decimal TotalAmount, string Currency);
}
