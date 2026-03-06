namespace OrderIntake.Application.Interfaces;

/// <summary>
/// HTTP client contract for stock validation requests to Mock 1 (Stock Service).
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IStockServiceClient
{
    /// <summary>
    /// Checks whether sufficient stock is available for a given product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// True if stock is available; false if out of stock.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails with a non-stock-result status (e.g., 400, 500).</exception>
    Task<bool> IsAvailableAsync(Guid productId, int quantity, CancellationToken ct = default);
}
