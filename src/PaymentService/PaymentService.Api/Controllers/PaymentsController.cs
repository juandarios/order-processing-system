using Mediator;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Commands.InitiatePayment;
using PaymentService.Application.Commands.ProcessWebhook;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Controller for payment operations: initiating payments and processing gateway webhooks.
/// </summary>
[ApiController]
[Route("payments")]
public class PaymentsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Initiates payment processing for an order. Called by the Order Orchestrator (S3).
    /// </summary>
    /// <param name="request">The payment initiation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>202 Accepted with the generated payment ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken ct)
    {
        var paymentId = await mediator.Send(
            new InitiatePaymentCommand(request.OrderId, request.Amount, request.Currency), ct);

        return Accepted(new InitiatePaymentResponse(paymentId));
    }

    /// <summary>
    /// Receives payment result webhook callback from the Payment Gateway Mock (Mock 2).
    /// </summary>
    /// <param name="webhook">The webhook payload from the gateway.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK when the webhook is processed successfully.</returns>
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessWebhook(
        [FromBody] PaymentWebhookRequest webhook,
        CancellationToken ct)
    {
        await mediator.Send(new ProcessWebhookCommand(
            webhook.OrderId,
            webhook.PaymentId,
            webhook.Status,
            webhook.Reason,
            webhook.Amount,
            webhook.Currency), ct);

        return Ok();
    }
}

/// <summary>Request to initiate payment. Sent by the Order Orchestrator.</summary>
public record InitiatePaymentRequest(Guid OrderId, decimal Amount, string Currency);

/// <summary>Response returned after initiating a payment.</summary>
public record InitiatePaymentResponse(Guid PaymentId);

/// <summary>Webhook payload from the Payment Gateway Mock.</summary>
public record PaymentWebhookRequest(
    Guid OrderId,
    Guid PaymentId,
    string Status,
    string? Reason,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt);
