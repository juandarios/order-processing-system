using OrderIntake.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace OrderIntake.Infrastructure.HttpClients;

/// <summary>
/// HTTP client adapter for the Stock Service Mock (Mock 1).
/// Implements <see cref="IStockServiceClient"/>.
/// </summary>
public class StockServiceClient(
    HttpClient httpClient,
    ILogger<StockServiceClient> logger) : IStockServiceClient
{
    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(Guid productId, int quantity, CancellationToken ct = default)
    {
        var url = $"stock/availability?productId={productId}&quantity={quantity}";
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
}
