using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Controllers;

namespace PaymentGateway.Controllers;

/// <summary>
/// Allows test runners to configure the payment gateway mock behavior at runtime.
/// </summary>
[ApiController]
[Route("config")]
public class ConfigController(ILogger<ConfigController> logger) : ControllerBase
{
    /// <summary>
    /// Configures the payment gateway mock behavior for subsequent charge requests.
    /// </summary>
    /// <param name="config">Configuration for immediate response, webhook delay, and result.</param>
    /// <returns>200 OK when configuration is applied.</returns>
    [HttpPost("payment-gateway")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Configure([FromBody] GatewayMockConfigRequest config)
    {
        if (config.ImmediateResponse is not (202 or 400 or 422 or 500))
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid immediateResponse",
                Detail = "Allowed values: 202, 400, 422, 500"
            });

        if (config.WebhookResult is not ("approved" or "rejected" or "expired"))
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid webhookResult",
                Detail = "Allowed values: approved, rejected, expired"
            });

        var gatewayConfig = new PaymentGatewayConfig(
            config.ImmediateResponse,
            config.WebhookDelayMs,
            config.WebhookResult,
            config.WebhookReason);

        ChargeController.SetConfig(gatewayConfig);

        if (!string.IsNullOrWhiteSpace(config.WebhookUrl))
            ChargeController.SetWebhookUrl(config.WebhookUrl);

        logger.LogInformation(
            "Gateway mock configured: immediateResponse={Response}, webhookResult={Result}, delay={Delay}ms",
            config.ImmediateResponse, config.WebhookResult, config.WebhookDelayMs);

        return Ok(new { configured = true });
    }
}

/// <summary>
/// Configuration request for the payment gateway mock.
/// </summary>
public record GatewayMockConfigRequest(
    int ImmediateResponse,
    int WebhookDelayMs,
    string WebhookResult,
    string? WebhookReason,
    string? WebhookUrl);
