using Microsoft.AspNetCore.Mvc;

namespace StockService.Controllers;

/// <summary>
/// Allows test runners to configure the stock mock behavior at runtime.
/// </summary>
[ApiController]
[Route("config")]
public class ConfigController(ILogger<ConfigController> logger) : ControllerBase
{
    /// <summary>
    /// Configures the stock mock response for subsequent requests.
    /// </summary>
    /// <param name="config">Configuration specifying the response code to return.</param>
    /// <returns>200 OK when configuration is applied.</returns>
    [HttpPost("stock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Configure([FromBody] StockMockConfig config)
    {
        if (config.Response is not (200 or 409 or 400 or 500))
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid response code",
                Detail = "Allowed values: 200, 409, 400, 500"
            });

        StockController.SetResponse(config.Response);
        logger.LogInformation("Stock mock configured to return {Response}", config.Response);
        return Ok(new { configured = config.Response });
    }
}

/// <summary>
/// Configuration payload for the stock mock.
/// </summary>
/// <param name="Response">HTTP status code to return: 200 (available), 409 (out of stock), 400 (invalid), 500 (error).</param>
public record StockMockConfig(int Response);
