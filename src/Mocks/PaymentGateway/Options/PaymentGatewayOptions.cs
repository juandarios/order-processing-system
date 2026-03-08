namespace PaymentGateway.Options;

/// <summary>
/// Configuration options for the Payment Gateway Mock.
/// </summary>
public class PaymentGatewayOptions
{
    /// <summary>
    /// Gets or sets the URL of the Payment Service webhook endpoint.
    /// The gateway mock posts payment results to this URL after processing a charge.
    /// </summary>
    /// <remarks>
    /// In Docker Compose this is set via the <c>PaymentGateway__WebhookUrl</c>
    /// environment variable to <c>http://payment-service:8080/payments/webhook</c>.
    /// For local development it defaults to <c>http://localhost:5020/payments/webhook</c>.
    /// </remarks>
    public string WebhookUrl { get; set; } = "http://localhost:5020/payments/webhook";
}
