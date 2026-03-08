using Mediator;
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
    /// Phase 1 (synchronous): persists the saga state and responds 200 OK to the caller.
    /// Phase 2 (internal): initiates payment via S2. Failures in Phase 2 are handled internally
    /// and are never propagated back to S1. The saga remains in StockValidated state if S2 is
    /// unavailable, awaiting external recovery or Outbox Pattern implementation.
    /// </summary>
    /// <param name="notification">The stock validation result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK once the notification is persisted, regardless of S2 availability.</returns>
    [HttpPost("stock-validated")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StockValidated(
        [FromBody] StockValidatedNotification notification,
        CancellationToken ct)
    {
        // The handler persists the saga and attempts to call S2 internally.
        // Any S2 failure is swallowed in the handler; this call always succeeds
        // once the notification is persisted.
        await mediator.Send(new ProcessStockValidatedCommand(notification), ct);
        return Ok();
    }

    /// <summary>
    /// Receives a payment processed notification from Payment Service (S2).
    /// Persists the saga transition and responds 200 OK to the caller.
    /// Internal processing failures do not propagate to S2.
    /// </summary>
    /// <param name="notification">The payment result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK once the notification is persisted.</returns>
    [HttpPost("payment-processed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PaymentProcessed(
        [FromBody] PaymentProcessedNotification notification,
        CancellationToken ct)
    {
        await mediator.Send(new ProcessPaymentProcessedCommand(notification), ct);
        return Ok();
    }
}
