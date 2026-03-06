using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PaymentService.IntegrationTests.Infrastructure;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace PaymentService.IntegrationTests;

public class PaymentsTests : IClassFixture<PaymentServiceWebAppFactory>
{
    private readonly PaymentServiceWebAppFactory _factory;

    public PaymentsTests(PaymentServiceWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InitiatePayment_ValidRequest_ShouldReturn202WithPaymentId()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var request = new { OrderId = orderId, Amount = 99.99m, Currency = "USD" };

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/payments", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<PaymentIdResponse>();
        body!.PaymentId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessWebhook_WithApprovedStatus_ShouldReturn200AndNotifyOrchestrator()
    {
        // Arrange — first initiate a payment to have something to approve
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        _factory.OrchestratorMock
            .Given(Request.Create().WithPath("/orchestrator/orders/payment-processed").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var client = _factory.CreateClient();

        // Initiate payment first
        var initiateResp = await client.PostAsJsonAsync("/payments",
            new { OrderId = orderId, Amount = 50.00m, Currency = "USD" });
        initiateResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var initiated = await initiateResp.Content.ReadFromJsonAsync<PaymentIdResponse>();

        // Act — send webhook
        var webhook = new
        {
            OrderId = orderId,
            PaymentId = initiated!.PaymentId,
            Status = "approved",
            Reason = (string?)null,
            Amount = 50.00m,
            Currency = "USD",
            OccurredAt = DateTimeOffset.UtcNow
        };
        var webhookResp = await client.PostAsJsonAsync("/payments/webhook", webhook);

        // Assert
        var body = await webhookResp.Content.ReadAsStringAsync();
        webhookResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, body);
        _factory.OrchestratorMock.LogEntries.Should().Contain(e =>
            e.RequestMessage.Path == "/orchestrator/orders/payment-processed");
    }

    [Fact]
    public async Task ProcessWebhook_WithRejectedStatus_ShouldReturn200AndNotifyOrchestrator()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        _factory.OrchestratorMock
            .Given(Request.Create().WithPath("/orchestrator/orders/payment-processed").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var client = _factory.CreateClient();

        // Initiate
        var initiateResp = await client.PostAsJsonAsync("/payments",
            new { OrderId = orderId, Amount = 75.00m, Currency = "USD" });
        var initiated = await initiateResp.Content.ReadFromJsonAsync<PaymentIdResponse>();

        // Act — rejected webhook
        var webhook = new
        {
            OrderId = orderId,
            PaymentId = initiated!.PaymentId,
            Status = "rejected",
            Reason = "insufficient_funds",
            Amount = 75.00m,
            Currency = "USD",
            OccurredAt = DateTimeOffset.UtcNow
        };
        var webhookResp = await client.PostAsJsonAsync("/payments/webhook", webhook);

        // Assert
        var rejBody = await webhookResp.Content.ReadAsStringAsync();
        webhookResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, rejBody);
    }

    private record PaymentIdResponse(Guid PaymentId);
}
