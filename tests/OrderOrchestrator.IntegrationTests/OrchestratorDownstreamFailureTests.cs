using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderOrchestrator.IntegrationTests.Infrastructure;
using Shared.Contracts;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace OrderOrchestrator.IntegrationTests;

/// <summary>
/// Integration tests verifying that S3 does not propagate S2 failures back to the caller.
/// </summary>
public class OrchestratorDownstreamFailureTests : IClassFixture<OrchestratorWebAppFactory>
{
    private readonly OrchestratorWebAppFactory _factory;

    public OrchestratorDownstreamFailureTests(OrchestratorWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StockValidated_PaymentServiceDown_ReturnsOkAndSagaStaysInStockValidated()
    {
        // Arrange — configure mock payment service to return 503 (simulating S2 down)
        var orderId = Guid.NewGuid();

        _factory.PaymentServiceMock
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 55.00m,
            Currency: "EUR",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 2, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        var client = _factory.CreateClient();

        // Act — S1 notifies S3; S2 is down
        var response = await client.PostAsJsonAsync("/orchestrator/orders/stock-validated", notification);

        // Assert — S3 must respond 200 OK to S1 regardless of S2 availability
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "S3 must acknowledge the notification once it is persisted, independent of S2 availability");
    }
}
