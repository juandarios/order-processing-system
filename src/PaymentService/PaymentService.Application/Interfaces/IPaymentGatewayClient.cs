namespace PaymentService.Application.Interfaces;

/// <summary>
/// HTTP client contract for the Payment Gateway Mock (Mock 2).
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IPaymentGatewayClient
{
    /// <summary>
    /// Submits a charge request to the payment gateway.
    /// </summary>
    /// <param name="orderId">The order identifier for this payment.</param>
    /// <param name="amount">The amount to charge.</param>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the gateway accepted the request (202); false otherwise.</returns>
    /// <exception cref="HttpRequestException">Thrown when the gateway returns an unexpected error.</exception>
    Task<bool> ChargeAsync(Guid orderId, decimal amount, string currency, CancellationToken ct = default);
}
