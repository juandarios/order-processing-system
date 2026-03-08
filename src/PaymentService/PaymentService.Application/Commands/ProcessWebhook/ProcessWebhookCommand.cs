using Mediator;

namespace PaymentService.Application.Commands.ProcessWebhook;

/// <summary>
/// Command to process a webhook callback from the payment gateway.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="PaymentId">The payment identifier from the gateway.</param>
/// <param name="Status">Payment result: approved, rejected, or expired.</param>
/// <param name="Reason">Rejection reason (only when status is rejected).</param>
/// <param name="Amount">The payment amount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public record ProcessWebhookCommand(
    Guid OrderId,
    Guid PaymentId,
    string Status,
    string? Reason,
    decimal Amount,
    string Currency) : ICommand;
