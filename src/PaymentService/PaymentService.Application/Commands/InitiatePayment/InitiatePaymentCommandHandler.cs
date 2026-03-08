using Mediator;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Logging;
using PaymentService.Domain.Entities;
using UUIDNext;

namespace PaymentService.Application.Commands.InitiatePayment;

/// <summary>
/// Handles <see cref="InitiatePaymentCommand"/>:
/// 1. Checks if a payment already exists for the given order (idempotency guard).
/// 2. If it does, returns the existing payment identifier without creating a duplicate.
/// 3. Otherwise, creates a pending payment record and calls the payment gateway asynchronously.
/// </summary>
public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGatewayClient gatewayClient,
    ILogger<InitiatePaymentCommandHandler> logger)
    : IRequestHandler<InitiatePaymentCommand, Guid>
{
    /// <summary>
    /// Initiates the payment process.
    /// Idempotent: if a payment already exists for the given <c>orderId</c> in any state,
    /// the existing payment identifier is returned without creating a duplicate record or
    /// re-calling the gateway.
    /// </summary>
    /// <param name="request">The command with order payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The payment identifier — either from the newly created payment or from the existing one.
    /// </returns>
    public async ValueTask<Guid> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        // --- Idempotency check (application-level, first line of defence) ---
        var existing = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct);
        if (existing is not null)
        {
            logger.DuplicatePaymentDetected(request.OrderId);
            return existing.Id;
        }

        var paymentId = Uuid.NewSequential();
        var payment = Payment.Create(paymentId, request.OrderId, request.Amount, request.Currency);

        // ON CONFLICT DO NOTHING in AddAsync provides database-level idempotency
        // as a second line of defence against race conditions.
        await paymentRepository.AddAsync(payment, ct);

        // Always re-query after insert to ensure we have the persisted row
        // (covers the unlikely race where another request won the conflict).
        var persisted = await paymentRepository.GetByOrderIdAsync(request.OrderId, ct);
        var resolvedId = persisted?.Id ?? paymentId;

        logger.PaymentCreated(resolvedId, request.OrderId);

        await gatewayClient.ChargeAsync(request.OrderId, request.Amount, request.Currency, ct);
        logger.GatewayChargeSent(resolvedId);

        return resolvedId;
    }
}
