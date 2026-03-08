using System.Net;
using System.Net.Http.Json;
using E2E.Infrastructure;
using FluentAssertions;
using Shared.Contracts;
using Xunit;

namespace E2E;

/// <summary>
/// End-to-end tests verifying that S3 does not propagate downstream (S2) failures
/// back to the caller (S1).
/// Prerequisite: all services running via "docker compose up -d",
/// except the Payment Service which is configured to be unavailable via the
/// Payment Gateway mock returning 503.
/// </summary>
[Collection("E2E")]
public class S3DownstreamFailureTests : IClassFixture<E2EFixture>
{
    private readonly E2EFixture _e2e;
    private readonly HttpClient _orchestratorClient =
        new() { BaseAddress = new Uri("http://localhost:5030") };

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public S3DownstreamFailureTests(E2EFixture e2e)
    {
        _e2e = e2e;
    }

    /// <summary>
    /// When S2 (Payment Service) is down, S3 must still return 200 OK to S1 once
    /// the stock-validated notification is persisted, and the saga must remain in
    /// StockValidated state (not fail or propagate an error).
    /// Verifies: no DLQ messages are generated for the order due to this failure.
    /// </summary>
    [Fact]
    public async Task NotifyStockValidated_WithS2Down_S1CompletesSuccessfullyAndSagaAwaitsRecovery()
    {
        // Arrange — configure the Payment Gateway mock to simulate S2 being down (503)
        await _e2e.PaymentGateway.PostAsJsonAsync("/config/payment-gateway", new
        {
            ImmediateResponse = 503,
            WebhookDelayMs = 0,
            WebhookResult = (string?)null,
            WebhookReason = (string?)null,
            WebhookUrl = (string?)null
        });

        var orderId = Guid.NewGuid();
        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 89.99m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        // Act — simulate S1 notifying S3 (S2 is unavailable)
        var response = await _orchestratorClient.PostAsJsonAsync(
            "/orchestrator/orders/stock-validated", notification);

        // Assert — S3 responds 200 OK to S1 even though S2 is down
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "S3 must acknowledge the notification once persisted, " +
                     "regardless of S2 availability");

        // Assert — no DLQ messages were produced for this orderId
        // (S3 failure must NOT cause S1 to retry and route to DLQ)
        var dlqContainsOrder = await E2EFixture.WaitForKafkaMessageAsync(
            topic: "order-placed-dlq",
            consumerGroup: $"e2e-dlq-s3-downstream-{orderId}",
            predicate: msg => msg.Contains(orderId.ToString()),
            timeout: TimeSpan.FromSeconds(5));

        dlqContainsOrder.Should().BeFalse(
            because: "S3 not propagating errors to S1 means no DLQ messages for this order");
    }
}
