using System.Net.Http.Json;

namespace E2E.Infrastructure;

/// <summary>
/// Shared HTTP clients and helpers for E2E tests.
/// Assumes all services are running via docker compose (docker compose up -d).
/// </summary>
public class E2EFixture : IDisposable
{
    // Host-side URLs — match docker-compose.yml port mappings
    public static readonly string OrderProducerUrl  = "http://localhost:5000";
    public static readonly string OrderIntakeUrl    = "http://localhost:5001";
    public static readonly string StockServiceUrl   = "http://localhost:5010";
    public static readonly string PaymentGatewayUrl = "http://localhost:5040";

    // Internal Docker network URL the Payment Gateway uses to reach the Payment Service webhook
    private const string PaymentServiceWebhookUrl = "http://payment-service:8080/payments/webhook";

    public HttpClient OrderProducer  { get; } = new() { BaseAddress = new Uri(OrderProducerUrl) };
    public HttpClient OrderIntake    { get; } = new() { BaseAddress = new Uri(OrderIntakeUrl) };
    public HttpClient StockService   { get; } = new() { BaseAddress = new Uri(StockServiceUrl) };
    public HttpClient PaymentGateway { get; } = new() { BaseAddress = new Uri(PaymentGatewayUrl) };

    /// <summary>
    /// Polls <paramref name="check"/> until it returns true or <paramref name="timeout"/> elapses.
    /// </summary>
    public static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> check,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await check()) return true;
            await Task.Delay(interval);
        }

        return false;
    }

    /// <summary>
    /// Configures the Stock Service mock to return stock available (HTTP 200).
    /// </summary>
    public Task SetStockAvailableAsync() =>
        StockService.PostAsJsonAsync("/config/stock", new { Response = 200 });

    /// <summary>
    /// Configures the Stock Service mock to return out of stock (HTTP 409).
    /// </summary>
    public Task SetStockUnavailableAsync() =>
        StockService.PostAsJsonAsync("/config/stock", new { Response = 409 });

    /// <summary>
    /// Configures the Payment Gateway mock to approve all payments after 1 second.
    /// </summary>
    public Task SetPaymentApprovedAsync() =>
        PaymentGateway.PostAsJsonAsync("/config/payment-gateway", new
        {
            ImmediateResponse = 202,
            WebhookDelayMs = 1000,
            WebhookResult = "approved",
            WebhookReason = (string?)null,
            WebhookUrl = PaymentServiceWebhookUrl
        });

    /// <summary>
    /// Configures the Payment Gateway mock to reject all payments after 1 second.
    /// </summary>
    public Task SetPaymentRejectedAsync() =>
        PaymentGateway.PostAsJsonAsync("/config/payment-gateway", new
        {
            ImmediateResponse = 202,
            WebhookDelayMs = 1000,
            WebhookResult = "rejected",
            WebhookReason = "card_declined",
            WebhookUrl = PaymentServiceWebhookUrl
        });

    public void Dispose()
    {
        OrderProducer.Dispose();
        OrderIntake.Dispose();
        StockService.Dispose();
        PaymentGateway.Dispose();
    }
}
