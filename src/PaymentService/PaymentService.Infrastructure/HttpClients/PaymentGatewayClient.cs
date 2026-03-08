using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Exceptions;

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
    public async Task<bool> ChargeAsync(Guid paymentId, Guid orderId, decimal amount, string currency, CancellationToken ct = default)
    {
        try
        {
            var payload = new { orderId, amount, currency };
            var response = await httpClient.PostAsJsonAsync("charge", payload, ct);

            logger.LogInformation(
                "Gateway charge for payment {PaymentId} order {OrderId} → {StatusCode}",
                paymentId, orderId, (int)response.StatusCode);

            return response.StatusCode == System.Net.HttpStatusCode.Accepted;
        }
        catch (Exception ex) when (ex is not PaymentGatewayUnavailableException)
        {
            // Polly resilience has already exhausted all retries at this point.
            // Wrap any remaining failure (HttpRequestException, circuit breaker open, timeout)
            // in a typed exception so the caller can handle it without catching Exception broadly.
            throw new PaymentGatewayUnavailableException(
                $"Payment gateway unavailable for payment {paymentId}: {ex.Message}", ex);
        }
    }
}
