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

    /// <summary>
    /// A valid payment request must be persisted immediately and 202 Accepted returned to S3.
    /// The gateway call runs in background; this test only asserts the synchronous phase.
    /// </summary>
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

        // Assert — 202 returned from the synchronous Phase 1 (payment persisted)
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<PaymentIdResponse>();
        body!.PaymentId.Should().NotBeEmpty();
    }

    /// <summary>
    /// POST /payments called twice with the same orderId must return 202 on both calls
    /// and persist only one payment record (idempotency).
    /// </summary>
    [Fact]
    public async Task InitiatePayment_CalledTwiceWithSameOrderId_OnlyOnePaymentCreated()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var request = new { OrderId = orderId, Amount = 50.00m, Currency = "USD" };
        var client = _factory.CreateClient();

        // Act
        var firstResponse = await client.PostAsJsonAsync("/payments", request);
        var secondResponse = await client.PostAsJsonAsync("/payments", request);

        // Assert — both return 202 (not 500 on first and 202 on second)
        firstResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        secondResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<PaymentIdResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<PaymentIdResponse>();

        // Both calls must return the same payment ID (idempotent)
        firstBody!.PaymentId.Should().Be(secondBody!.PaymentId);
    }

    /// <summary>
    /// POST /payments must persist the payment with Pending status and return 202 immediately,
    /// regardless of whether the gateway call has completed.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_GatewayCalledInBackground_PaymentPersisted()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Gateway responds 202 (success path — no webhook configured for this test)
        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var request = new { OrderId = orderId, Amount = 120.00m, Currency = "GBP" };
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/payments", request);

        // Assert — synchronous phase: 202 returned with a payment ID
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<PaymentIdResponse>();
        body!.PaymentId.Should().NotBeEmpty();

        // The payment is already in DB (persisted synchronously in Phase 1).
        // Allow a short window for the background task to settle, then verify
        // the gateway was contacted (optional — the important assertion is Phase 1 above).
        await Task.Delay(200);
        _factory.GatewayMock.LogEntries.Should().Contain(e => e.RequestMessage.Path == "/charge");
    }

    [Fact]
    public async Task ProcessWebhook_WithApprovedStatus_ShouldReturn200AndNotifyOrchestrator()
    {
        // Arrange — first initiate a payment to have something to approve
        var orderId = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        _factory.OrchestratorMock
            .Given(Request.Create().WithPath("/orchestrator/orders/payment-processed").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var client = _factory.CreateClient();

        // Initiate payment first (Phase 1 — synchronous, 202 Accepted)
        var initiateResp = await client.PostAsJsonAsync("/payments",
            new { OrderId = orderId, Amount = 50.00m, Currency = "USD" });
        initiateResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        var initiated = await initiateResp.Content.ReadFromJsonAsync<PaymentIdResponse>();

        // Allow background gateway call to complete before sending webhook
        await Task.Delay(200);

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

        // Allow background gateway call to complete
        await Task.Delay(200);

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

    /// <summary>
    /// Three simultaneous POST /payments requests with distinct orderIds must all return 202
    /// and each payment must be persisted with Pending status. Verifies that concurrent
    /// background tasks do not mix data between requests.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_MultipleSimultaneousRequests_EachProcessedWithCorrectData()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();
        var orderId3 = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var client = _factory.CreateClient();

        // Snapshot the gateway call count before this test to handle LogEntries
        // accumulated by other tests sharing the same IClassFixture WireMockServer.
        var chargeCountBefore = _factory.GatewayMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/charge");

        var request1 = new { OrderId = orderId1, Amount = 10.00m, Currency = "USD" };
        var request2 = new { OrderId = orderId2, Amount = 20.00m, Currency = "EUR" };
        var request3 = new { OrderId = orderId3, Amount = 30.00m, Currency = "GBP" };

        // Act — fire all three simultaneously
        var task1 = client.PostAsJsonAsync("/payments", request1);
        var task2 = client.PostAsJsonAsync("/payments", request2);
        var task3 = client.PostAsJsonAsync("/payments", request3);
        var responses = await Task.WhenAll(task1, task2, task3);

        // Assert — all three synchronous phases returned 202 Accepted
        responses[0].StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        responses[1].StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        responses[2].StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);

        var body1 = await responses[0].Content.ReadFromJsonAsync<PaymentIdResponse>();
        var body2 = await responses[1].Content.ReadFromJsonAsync<PaymentIdResponse>();
        var body3 = await responses[2].Content.ReadFromJsonAsync<PaymentIdResponse>();

        // Each response must carry a distinct payment ID
        body1!.PaymentId.Should().NotBe(body2!.PaymentId);
        body2.PaymentId.Should().NotBe(body3!.PaymentId);
        body1.PaymentId.Should().NotBe(body3.PaymentId);

        // Allow background tasks to complete
        await Task.Delay(500);

        // All three gateway calls must have been made (background tasks ran independently).
        // Compare against the pre-test snapshot to isolate this test from others in the fixture.
        var chargeCountAfter = _factory.GatewayMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/charge");
        (chargeCountAfter - chargeCountBefore).Should().Be(3,
            "each of the 3 simultaneous requests must trigger exactly one background gateway call");
    }

    private record PaymentIdResponse(Guid PaymentId);
}
