using Microsoft.Extensions.Logging;
using OrderIntake.Application.Interfaces;
using OrderIntake.Domain.Exceptions;

namespace OrderIntake.Infrastructure.HttpClients;

// TODO: Implement Outbox Pattern for prolonged unavailability

/// <summary>
/// HTTP client adapter for the Stock Service Mock (Mock 1).
/// Implements <see cref="IStockServiceClient"/>.
/// Polly resilience is applied at the HttpClient level (see Program.cs).
/// When all retries are exhausted, <see cref="StockServiceUnavailableException"/> is thrown.
/// </summary>
public class StockServiceClient(
    HttpClient httpClient,
    ILogger<StockServiceClient> logger) : IStockServiceClient
{
    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(Guid productId, int quantity, CancellationToken ct = default)
    {
        var url = $"stock/availability?productId={productId}&quantity={quantity}";

        try
        {
            var response = await httpClient.GetAsync(url, ct);

            logger.LogInformation(
                "Stock check for product {ProductId} qty {Quantity} → {StatusCode}",
                productId, quantity, (int)response.StatusCode);

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.OK => true,
                System.Net.HttpStatusCode.Conflict => false,
                _ => throw new HttpRequestException(
                    $"Stock service returned unexpected status {(int)response.StatusCode} for product {productId}")
            };
        }
        catch (StockServiceUnavailableException)
        {
            throw; // already wrapped, propagate as-is
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Polly exhausted all retries — wrap so the consumer can identify the failure type
            throw new StockServiceUnavailableException(
                $"Stock service unavailable for product {productId} after all retries: {ex.Message}", ex);
        }
    }
}
