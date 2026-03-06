using Microsoft.AspNetCore.Mvc;

namespace StockService.Controllers;

/// <summary>
/// Simulates an external stock validation service.
/// Returns configurable responses for testing different scenarios.
/// </summary>
[ApiController]
[Route("stock")]
public class StockController(ILogger<StockController> logger) : ControllerBase
{
    private static int _configuredResponse = 200;

    /// <summary>
    /// Sets the configured response code for subsequent availability checks.
    /// </summary>
    /// <param name="response">HTTP status code to return (200, 409, 400, 500).</param>
    internal static void SetResponse(int response) => _configuredResponse = response;

    /// <summary>
    /// Checks stock availability for a given product and quantity.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The requested quantity.</param>
    /// <returns>200 if available, 409 if out of stock, 400 if invalid params, 500 on error.</returns>
    [HttpGet("availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult CheckAvailability([FromQuery] Guid productId, [FromQuery] int quantity)
    {
        logger.LogInformation(
            "Stock check for product {ProductId} quantity {Quantity} → configured response {Response}",
            productId, quantity, _configuredResponse);

        return _configuredResponse switch
        {
            200 => Ok(new { available = true }),
            409 => Conflict(new { message = "Out of stock" }),
            400 => BadRequest(new { message = "Invalid parameters" }),
            500 => StatusCode(500, new { message = "Internal server error" }),
            _ => Ok(new { available = true })
        };
    }
}
