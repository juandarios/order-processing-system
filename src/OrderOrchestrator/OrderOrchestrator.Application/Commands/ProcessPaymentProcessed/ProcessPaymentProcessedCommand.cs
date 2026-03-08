using Mediator;
using Shared.Contracts;

namespace OrderOrchestrator.Application.Commands.ProcessPaymentProcessed;

/// <summary>
/// Command to process a payment-processed notification from the Payment Service (S2).
/// Drives the saga to its terminal state (PaymentConfirmed or Failed).
/// </summary>
/// <param name="Notification">The payment result notification from S2.</param>
public record ProcessPaymentProcessedCommand(PaymentProcessedNotification Notification) : ICommand;
