using Mediator;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Logging;
using PaymentService.Domain.Entities;
using UUIDNext;

namespace PaymentService.Application.Commands.InitiatePayment;

/// <summary>
/// Handles <see cref="InitiatePaymentCommand"/>:
/// 1. Creates a pending payment record.
/// 2. Calls the payment gateway asynchronously.
/// </summary>
public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGatewayClient gatewayClient,
    ILogger<InitiatePaymentCommandHandler> logger)
    : IRequestHandler<InitiatePaymentCommand, Guid>
{
    /// <summary>
    /// Initiates the payment process.
    /// </summary>
    /// <param name="request">The command with order payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated payment identifier.</returns>
    public async ValueTask<Guid> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        var paymentId = Uuid.NewSequential();
        var payment = Payment.Create(paymentId, request.OrderId, request.Amount, request.Currency);

        await paymentRepository.AddAsync(payment, ct);
        logger.PaymentCreated(paymentId, request.OrderId);

        await gatewayClient.ChargeAsync(request.OrderId, request.Amount, request.Currency, ct);
        logger.GatewayChargeSent(paymentId);

        return paymentId;
    }
}
