using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderIntake.Application.Queries.GetOrder;
using OrderIntake.Domain.Exceptions;

namespace OrderIntake.Api.Controllers;

/// <summary>
/// Controller for querying order state in the Order Intake service.
/// </summary>
[ApiController]
[Route("orders")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves the current state of an order by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order details if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderQuery(id), ct);

        return result is null
            ? NotFound(new ProblemDetails { Status = 404, Title = "Order not found" })
            : Ok(result);
    }
}
