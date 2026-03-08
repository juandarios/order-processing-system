using System.Net.Http.Json;
using System.Text.Json;
using E2E.Infrastructure;
using FluentAssertions;

namespace E2E;

/// <summary>
/// End-to-end tests for the Dead Letter Queue (DLQ) and validation error topic scenarios.
/// Prerequisite: all services running via "docker compose up -d".
/// Tests run sequentially (xUnit's default for the same collection).
/// </summary>
[Collection("E2E")]
public class DlqAndValidationErrorTests : IClassFixture<E2EFixture>
{
    private readonly E2EFixture _e2e;

    private static readonly TimeSpan KafkaProcessingTimeout = TimeSpan.FromSeconds(30);
    private const string KafkaBootstrapServers = "localhost:9092";
    private const string DlqTopic = "order-placed-dlq";
    private const string ValidationErrorTopic = "order-validation-errors";

    public DlqAndValidationErrorTests(E2EFixture e2e)
    {
        _e2e = e2e;
    }

    /// <summary>
    /// Publishing a structurally invalid payload (malformed JSON) to the order-placed topic
    /// should cause S1 to route the message to the DLQ with error type "DeserializationError".
    /// </summary>
    [Fact]
    public async Task PublishMalformedMessage_DeserializationFails_AppearsInDlqTopic()
    {
        // Arrange — produce a malformed JSON message directly to the order-placed topic
        var malformedPayload = "{ this is not valid json :::";
        var consumerGroup = $"e2e-dlq-deser-{Guid.NewGuid():N}";

        await ProduceRawMessageAsync("order-placed", key: "malformed", value: malformedPayload);

        // Act & Assert — wait for DLQ message with DeserializationError
        var found = await E2EFixture.WaitForKafkaMessageAsync(
            topic: DlqTopic,
            consumerGroup: consumerGroup,
            predicate: msg => msg.Contains("DeserializationError", StringComparison.OrdinalIgnoreCase),
            timeout: KafkaProcessingTimeout);

        found.Should().BeTrue(
            because: "a malformed message should be routed to the DLQ as DeserializationError");
    }

    /// <summary>
    /// Publishing an invalid order (stock service returns 500 immediately causing domain exception)
    /// should route the validation failure to the order-validation-errors topic.
    /// We simulate a DomainException by configuring the Stock Service to return 500 and
    /// verifying that transient errors eventually exhaust and reach DLQ.
    ///
    /// The actual validation error scenario: an order message that passes deserialization
    /// but fails because of a business rule (here we publish a well-formed event and
    /// mock the stock service to return 400 which maps to bad params — but the real
    /// DomainValidation path is exercised when the handler throws DomainException).
    ///
    /// Since in the current implementation, DomainException is raised inside the handler
    /// when order processing logic itself fails (not via stock mock), we validate the
    /// ValidationErrors topic receives events by triggering a domain error via a
    /// duplicate orderId (same orderId published twice — second attempt will violate
    /// a DB unique constraint which surfaces as a DomainException if the repository
    /// wraps it, or as a TransientError to DLQ otherwise).
    ///
    /// For a deterministic test, we configure stock to succeed and publish the same orderId
    /// twice; the second message should be routed to the appropriate error topic.
    /// </summary>
    [Fact]
    public async Task PublishInvalidOrder_ValidationFails_AppearsInValidationErrorsTopic()
    {
        // Arrange — configure stock to succeed
        await _e2e.SetStockAvailableAsync();

        // Place a valid order first so its ID exists in the database
        var orderId = await PlaceOrderAsync();
        orderId.Should().NotBeEmpty();

        // Wait for the first order to be processed (StockValidated)
        var processed = await E2EFixture.WaitUntilAsync(
            () => OrderHasStatusAsync(orderId, "StockValidated"),
            timeout: KafkaProcessingTimeout);

        processed.Should().BeTrue(because: "first order should be processed before sending duplicate");

        // Now publish a raw Kafka event with the same orderId — this causes a duplicate
        // DB insert which the repository surfaces as a DomainException (primary key violation)
        var duplicateEvent = BuildOrderPlacedEvent(orderId);
        var consumerGroup = $"e2e-validation-{Guid.NewGuid():N}";

        await ProduceRawMessageAsync("order-placed", key: orderId.ToString(), value: duplicateEvent);

        // Act & Assert — wait for a message on the validation-errors topic
        var found = await E2EFixture.WaitForKafkaMessageAsync(
            topic: ValidationErrorTopic,
            consumerGroup: consumerGroup,
            predicate: msg => msg.Contains("DomainValidation", StringComparison.OrdinalIgnoreCase)
                           || msg.Contains(orderId.ToString(), StringComparison.OrdinalIgnoreCase),
            timeout: KafkaProcessingTimeout);

        found.Should().BeTrue(
            because: "a duplicate order should trigger a domain validation error routed to the validation-errors topic");
    }

    /// <summary>
    /// When the stock service is unavailable and returns 500, Polly retries 3 times with
    /// exponential backoff, then routes the failure to the DLQ as "TransientError".
    /// After the stock service recovers, new orders should be processed successfully.
    /// </summary>
    [Fact]
    public async Task StockServiceFailsThenRecovers_SubsequentOrderProcessedSuccessfully()
    {
        // Arrange — configure stock service to return 500 (server error → Polly retries)
        await _e2e.StockService.PostAsJsonAsync("/config/stock", new { Response = 500 });

        var failingOrderId = await PlaceOrderAsync();
        failingOrderId.Should().NotBeEmpty();

        // The order should NOT reach StockValidated while stock is returning 500
        // (Polly will retry but eventually the error surfaces as TransientError to DLQ)
        var dlqGroup = $"e2e-transient-{Guid.NewGuid():N}";

        var reachedDlq = await E2EFixture.WaitForKafkaMessageAsync(
            topic: DlqTopic,
            consumerGroup: dlqGroup,
            predicate: msg => msg.Contains("TransientError", StringComparison.OrdinalIgnoreCase),
            timeout: TimeSpan.FromSeconds(60)); // longer timeout due to Polly retry delays

        reachedDlq.Should().BeTrue(
            because: "transient stock service failures should eventually route to DLQ as TransientError");

        // Act — stock service recovers
        await _e2e.SetStockAvailableAsync();

        // Place a new order — should now be processed successfully
        var recoveredOrderId = await PlaceOrderAsync();
        recoveredOrderId.Should().NotBeEmpty();

        var stockValidated = await E2EFixture.WaitUntilAsync(
            () => OrderHasStatusAsync(recoveredOrderId, "StockValidated"),
            timeout: KafkaProcessingTimeout);

        // Assert
        stockValidated.Should().BeTrue(
            because: "after stock service recovers, new orders should be processed successfully");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> PlaceOrderAsync()
    {
        var request = new
        {
            CustomerId = Guid.NewGuid(),
            CustomerEmail = "e2e-dlq@example.com",
            ShippingAddress = new
            {
                Street = "1 DLQ Street",
                City = "Testcity",
                Country = "US",
                ZipCode = "10001"
            },
            Items = new[]
            {
                new
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "DLQ Test Product",
                    Quantity = 1,
                    UnitPrice = 29.99m,
                    Currency = "USD"
                }
            }
        };

        var response = await _e2e.OrderProducer.PostAsJsonAsync("/orders", request);
        response.IsSuccessStatusCode.Should().BeTrue(
            because: $"placing order should succeed, got {response.StatusCode}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("orderId").GetGuid();
    }

    private async Task<bool> OrderHasStatusAsync(Guid orderId, string status)
    {
        try
        {
            var resp = await _e2e.OrderIntake.GetAsync($"/orders/{orderId}");
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadAsStringAsync();
            return json.Contains(status, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Produces a raw message directly to a Kafka topic without going through the Order Producer API.
    /// Used to simulate malformed payloads or edge-case events that the REST API would reject.
    /// </summary>
    private static async Task ProduceRawMessageAsync(string topic, string key, string value)
    {
        var config = new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = KafkaBootstrapServers
        };

        using var producer = new Confluent.Kafka.ProducerBuilder<string, string>(config).Build();
        await producer.ProduceAsync(topic, new Confluent.Kafka.Message<string, string>
        {
            Key = key,
            Value = value
        });
    }

    /// <summary>
    /// Builds a well-formed OrderPlaced Kafka event JSON with a specific orderId.
    /// Used to simulate duplicate order submissions.
    /// </summary>
    private static string BuildOrderPlacedEvent(Guid orderId)
    {
        var payload = new
        {
            orderId,
            customerId = Guid.NewGuid(),
            customerEmail = "dup@example.com",
            shippingAddress = new
            {
                street = "1 Dup Street",
                city = "DupCity",
                country = "US",
                zipCode = "99999"
            },
            items = new[]
            {
                new
                {
                    productId = Guid.NewGuid(),
                    productName = "Dup Product",
                    quantity = 1,
                    unitPrice = 9.99m,
                    currency = "USD"
                }
            },
            totalAmount = 9.99m,
            currency = "USD"
        };

        var envelope = new
        {
            eventId = Guid.NewGuid(),
            eventType = "OrderPlaced",
            occurredAt = DateTimeOffset.UtcNow,
            version = 1,
            payload
        };

        return JsonSerializer.Serialize(envelope);
    }
}
