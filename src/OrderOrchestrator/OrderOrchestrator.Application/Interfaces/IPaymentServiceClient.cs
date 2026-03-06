namespace OrderOrchestrator.Application.Interfaces;

/// <summary>
/// HTTP client contract for calling the Payment Service (S2).
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IPaymentServiceClient
{
    /// <summary>
    /// Sends a payment initiation request to the Payment Service.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="amount">The amount to charge.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated payment identifier.</returns>
    /// <exception cref="HttpRequestException">Thrown if the request fails.</exception>
    Task<Guid> InitiatePaymentAsync(Guid orderId, decimal amount, string currency, CancellationToken ct = default);
}
