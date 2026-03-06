using System.Net.Http.Json;
using System.Text.Json;
using E2E.Infrastructure;
using FluentAssertions;

namespace E2E;

/// <summary>
/// End-to-end tests that run against the full Docker Compose environment.
/// Prerequisite: run "docker compose up -d" and wait for all services to be healthy.
///
/// Scope: OrderIntake (S1) perspective — the order status reflects stock processing.
/// Payment confirmation is tracked in the Orchestrator saga (S3), not reflected back to S1.
/// </summary>
[Collection("E2E")]
public class FullFlowTests : IClassFixture<E2EFixture>
{
    private readonly E2EFixture _e2e;

    // First Kafka message after startup can take longer due to consumer group rebalancing.
    private static readonly TimeSpan KafkaProcessingTimeout = TimeSpan.FromSeconds(30);

    public FullFlowTests(E2EFixture e2e)
    {
        _e2e = e2e;
    }

    [Fact]
    public async Task FullFlow_StockAvailable_PaymentApproved_ShouldReachStockValidated()
    {
        // Arrange — configure mocks
        await _e2e.SetStockAvailableAsync();
        await _e2e.SetPaymentApprovedAsync();

        // Act — place order
        var orderId = await PlaceOrderAsync();
        orderId.Should().NotBeEmpty();

        // Poll OrderIntake until StockValidated
        // (payment confirmation flows through S3 Orchestrator, not reflected back to S1)
        var stockValidated = await E2EFixture.WaitUntilAsync(
            () => OrderHasStatusAsync(orderId, "StockValidated"),
            timeout: KafkaProcessingTimeout);

        // Assert — OrderIntake processed the stock check successfully
        stockValidated.Should().BeTrue(
            because: "order should reach StockValidated in OrderIntake within 30 seconds");

        // Extra wait to allow the payment webhook to fire (1s delay in gateway)
        // and the Orchestrator saga to transition — we just verify no errors bubble up
        await Task.Delay(TimeSpan.FromSeconds(3));

        var finalStatus = await GetOrderStatusAsync(orderId);
        finalStatus.Should().Be("StockValidated",
            because: "OrderIntake status stays StockValidated after stock check; " +
                     "payment outcome is tracked in the Orchestrator saga");
    }

    [Fact]
    public async Task FullFlow_StockUnavailable_ShouldCancelOrder()
    {
        // Arrange
        await _e2e.SetStockUnavailableAsync();

        // Act
        var orderId = await PlaceOrderAsync();
        orderId.Should().NotBeEmpty();

        // Poll until Cancelled
        var cancelled = await E2EFixture.WaitUntilAsync(
            () => OrderHasStatusAsync(orderId, "Cancelled"),
            timeout: KafkaProcessingTimeout);

        // Assert
        cancelled.Should().BeTrue(
            because: "order should be Cancelled when stock is unavailable");
    }

    [Fact]
    public async Task FullFlow_PaymentRejected_ShouldReachStockValidated()
    {
        // Arrange
        await _e2e.SetStockAvailableAsync();
        await _e2e.SetPaymentRejectedAsync();

        // Act
        var orderId = await PlaceOrderAsync();
        orderId.Should().NotBeEmpty();

        // Stock is available so OrderIntake should reach StockValidated.
        // The Orchestrator saga will then transition to Failed after the rejected webhook.
        var stockValidated = await E2EFixture.WaitUntilAsync(
            () => OrderHasStatusAsync(orderId, "StockValidated"),
            timeout: KafkaProcessingTimeout);

        // Assert
        stockValidated.Should().BeTrue(
            because: "stock should be validated regardless of payment outcome");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> PlaceOrderAsync()
    {
        var request = new
        {
            CustomerId = Guid.NewGuid(),
            CustomerEmail = "e2e@example.com",
            ShippingAddress = new
            {
                Street = "1 E2E Street",
                City = "Testcity",
                Country = "US",
                ZipCode = "10001"
            },
            Items = new[]
            {
                new
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "E2E Product",
                    Quantity = 1,
                    UnitPrice = 49.99m,
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

    private async Task<string?> GetOrderStatusAsync(Guid orderId)
    {
        try
        {
            var resp = await _e2e.OrderIntake.GetAsync($"/orders/{orderId}");
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("status").GetString();
        }
        catch
        {
            return null;
        }
    }
}
