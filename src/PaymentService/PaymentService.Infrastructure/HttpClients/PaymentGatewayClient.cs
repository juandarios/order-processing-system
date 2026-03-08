using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;

namespace PaymentService.Infrastructure.HttpClients;

// TODO: Implement Outbox Pattern for prolonged unavailability

/// <summary>
/// HTTP client adapter for the Payment Gateway Mock (Mock 2).
/// Implements <see cref="IPaymentGatewayClient"/>.
/// </summary>
public class PaymentGatewayClient(
    HttpClient httpClient,
    ILogger<PaymentGatewayClient> logger) : IPaymentGatewayClient
{
    /// <inheritdoc />
    public async Task<bool> ChargeAsync(Guid orderId, decimal amount, string currency, CancellationToken ct = default)
    {
        var payload = new { orderId, amount, currency };
        var response = await httpClient.PostAsJsonAsync("charge", payload, ct);

        logger.LogInformation(
            "Gateway charge for order {OrderId} → {StatusCode}",
            orderId, (int)response.StatusCode);

        return response.StatusCode == System.Net.HttpStatusCode.Accepted;
    }
}
