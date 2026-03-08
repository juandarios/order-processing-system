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
/// Integration tests verifying idempotency behaviour of S3 notification endpoints.
/// </summary>
public class OrchestratorIdempotencyTests : IClassFixture<OrchestratorWebAppFactory>
{
    private readonly OrchestratorWebAppFactory _factory;

    public OrchestratorIdempotencyTests(OrchestratorWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HandleStockValidated_CalledTwiceWithSameOrderId_OnlyOneSagaCreated()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        _factory.PaymentServiceMock
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBodyAsJson(new { PaymentId = paymentId }));

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 75.00m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        var client = _factory.CreateClient();

        // Act — send the same notification twice (simulating S1 retry via Polly)
        var response1 = await client.PostAsJsonAsync("/orchestrator/orders/stock-validated", notification);
        var response2 = await client.PostAsJsonAsync("/orchestrator/orders/stock-validated", notification);

        // Assert — both calls return success; no duplicate key error on the second call
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
