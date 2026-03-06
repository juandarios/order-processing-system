using MediatR;

namespace PaymentService.Application.Commands.InitiatePayment;

/// <summary>
/// Command to initiate payment processing for an order.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Amount">The amount to charge.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public record InitiatePaymentCommand(Guid OrderId, decimal Amount, string Currency) : IRequest<Guid>;
