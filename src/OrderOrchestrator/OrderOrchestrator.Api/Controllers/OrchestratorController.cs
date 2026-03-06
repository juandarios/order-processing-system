using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderOrchestrator.Application.Commands.ProcessPaymentProcessed;
using OrderOrchestrator.Application.Commands.ProcessStockValidated;
using Shared.Contracts;

namespace OrderOrchestrator.Api.Controllers;

/// <summary>
/// Controller for receiving orchestration notifications from S1 (Order Intake) and S2 (Payment Service).
/// </summary>
[ApiController]
[Route("orchestrator/orders")]
public class OrchestratorController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Receives a stock validation notification from Order Intake (S1).
    /// Creates the order saga and initiates payment if stock is available.
    /// </summary>
    /// <param name="notification">The stock validation result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>202 Accepted when the notification is processed.</returns>
    [HttpPost("stock-validated")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StockValidated(
        [FromBody] StockValidatedNotification notification,
        CancellationToken ct)
    {
        await mediator.Send(new ProcessStockValidatedCommand(notification), ct);
        return Accepted();
    }

    /// <summary>
    /// Receives a payment processed notification from Payment Service (S2).
    /// Drives the saga to its terminal state.
    /// </summary>
    /// <param name="notification">The payment result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>202 Accepted when the notification is processed.</returns>
    [HttpPost("payment-processed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PaymentProcessed(
        [FromBody] PaymentProcessedNotification notification,
        CancellationToken ct)
    {
        await mediator.Send(new ProcessPaymentProcessedCommand(notification), ct);
        return Accepted();
    }
}
