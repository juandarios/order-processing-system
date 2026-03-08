using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OrderOrchestrator.Application.Interfaces;

namespace OrderOrchestrator.Infrastructure.HttpClients;

// TODO: Implement Outbox Pattern for prolonged unavailability

/// <summary>
/// HTTP client adapter for the Payment Service (S2).
/// Implements <see cref="IPaymentServiceClient"/>.
/// </summary>
public class PaymentServiceClient(
    HttpClient httpClient,
    ILogger<PaymentServiceClient> logger) : IPaymentServiceClient
{
    /// <inheritdoc />
    public async Task<Guid> InitiatePaymentAsync(
        Guid orderId, decimal amount, string currency, CancellationToken ct = default)
    {
        var payload = new { orderId, amount, currency };
        var response = await httpClient.PostAsJsonAsync("payments", payload, ct);

        logger.LogInformation(
            "Payment initiation for order {OrderId} → {StatusCode}",
            orderId, (int)response.StatusCode);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaymentResponse>(ct);
        return result!.PaymentId;
    }

    private record PaymentResponse([property: JsonPropertyName("paymentId")] Guid PaymentId);
}
