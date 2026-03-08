using FluentAssertions;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Application.Commands.ProcessOrder;
using OrderIntake.IntegrationTests.Infrastructure;
using Shared.Events;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace OrderIntake.IntegrationTests;

public class GetOrderTests : IClassFixture<OrderIntakeWebAppFactory>
{
    private readonly OrderIntakeWebAppFactory _factory;

    public GetOrderTests(OrderIntakeWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrder_WithExistingOrder_ShouldReturn200()
    {
        // Arrange — seed an order by processing a command
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _factory.StockMock
            .Given(Request.Create().WithPath("/stock/availability").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        _factory.OrchestratorMock
            .Given(Request.Create().WithPath("/orchestrator/orders/stock-validated").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var evt = new OrderPlacedEvent(
            EventId: Guid.NewGuid(),
            EventType: "OrderPlaced",
            OccurredAt: DateTimeOffset.UtcNow,
            Version: 1,
            Payload: new OrderPlacedPayload(
                OrderId: orderId,
                CustomerId: Guid.NewGuid(),
                CustomerEmail: "get-test@example.com",
                ShippingAddress: new ShippingAddressDto("456 Elm St", "Shelbyville", "US", "67890"),
                Items: [new OrderItemDto(productId, "Gadget", 1, 29.99m, "USD")],
                TotalAmount: 29.99m,
                Currency: "USD"));

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new ProcessOrderCommand(evt));

        // Act
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(orderId.ToString());
    }

    [Fact]
    public async Task GetOrder_WithNonExistingOrder_ShouldReturn404()
    {
        // Act
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
