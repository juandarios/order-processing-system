using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PaymentGateway.Options;
using UUIDNext;

namespace PaymentGateway.Controllers;

/// <summary>
/// Simulates an external async payment gateway.
/// Accepts charges and fires a webhook callback after a configurable delay.
/// The webhook target URL is read from <see cref="PaymentGatewayOptions"/> and can be
/// overridden at runtime via <see cref="ConfigController"/> for test scenarios.
/// </summary>
[ApiController]
[Route("")]
public class ChargeController(
    ILogger<ChargeController> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<PaymentGatewayOptions> options) : ControllerBase
{
    private static PaymentGatewayConfig _config = new(202, 1000, "approved", null);

    /// <summary>
    /// Override URL set at runtime by the test-configuration endpoint.
    /// When <c>null</c> the URL from <see cref="PaymentGatewayOptions"/> is used.
    /// </summary>
    private static string? _runtimeWebhookUrl;

    /// <summary>Sets the current gateway configuration.</summary>
    internal static void SetConfig(PaymentGatewayConfig config) => _config = config;

    /// <summary>
    /// Overrides the webhook callback URL at runtime (used by integration tests via
    /// <see cref="ConfigController"/>). Pass <c>null</c> to revert to the configured default.
    /// </summary>
    /// <param name="url">The override URL, or <c>null</c> to clear the override.</param>
    internal static void SetWebhookUrl(string? url) => _runtimeWebhookUrl = url;

    /// <summary>
    /// Accepts a payment charge request and schedules an asynchronous webhook callback.
    /// </summary>
    /// <param name="request">The charge request with order and amount details.</param>
    /// <returns>202 Accepted, or configured error response.</returns>
    [HttpPost("charge")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult Charge([FromBody] ChargeRequest request)
    {
        logger.LogInformation(
            "Gateway charge received for order {OrderId}. Configured response: {Response}",
            request.OrderId, _config.ImmediateResponse);

        if (_config.ImmediateResponse != 202)
        {
            return _config.ImmediateResponse switch
            {
                400 => BadRequest(new ProblemDetails { Title = "Invalid payment data" }),
                422 => UnprocessableEntity(new ProblemDetails { Title = "Insufficient funds" }),
                500 => StatusCode(500, new ProblemDetails { Title = "Gateway internal error" }),
                _ => StatusCode(_config.ImmediateResponse)
            };
        }

        var paymentId = Uuid.NewSequential();

        // Resolve webhook URL: runtime override (set by tests) takes precedence over the
        // value from configuration (PaymentGatewayOptions.WebhookUrl).
        var webhookUrl = _runtimeWebhookUrl ?? options.Value.WebhookUrl;

        _ = Task.Run(async () =>
        {
            await Task.Delay(_config.WebhookDelayMs);
            await SendWebhookAsync(request.OrderId, paymentId, webhookUrl);
        });

        return Accepted(new { paymentId, message = "Payment accepted, processing asynchronously" });
    }

    private async Task SendWebhookAsync(Guid orderId, Guid paymentId, string webhookUrl)
    {
        try
        {
            var payload = new
            {
                orderId,
                paymentId,
                status = _config.WebhookResult,
                reason = _config.WebhookReason,
                amount = 0m,
                currency = "USD",
                occurredAt = DateTimeOffset.UtcNow
            };

            using var client = httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(webhookUrl, payload);

            logger.LogInformation(
                "Webhook sent for order {OrderId} → {StatusCode}",
                orderId, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send webhook for order {OrderId}", orderId);
        }
    }
}

/// <summary>
/// Charge request payload from the payment service.
/// </summary>
public record ChargeRequest(Guid OrderId, decimal Amount, string Currency);

/// <summary>
/// Payment gateway mock configuration.
/// </summary>
/// <param name="ImmediateResponse">HTTP status code returned immediately (202, 400, 422, 500).</param>
/// <param name="WebhookDelayMs">Milliseconds to wait before sending the webhook.</param>
/// <param name="WebhookResult">Webhook payment result: approved, rejected, expired.</param>
/// <param name="WebhookReason">Optional rejection reason.</param>
public record PaymentGatewayConfig(
    int ImmediateResponse,
    int WebhookDelayMs,
    string WebhookResult,
    string? WebhookReason);
