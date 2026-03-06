using Shared.Contracts;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// HTTP client contract for sending notifications to the Order Orchestrator (S3).
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IOrchestratorClient
{
    /// <summary>
    /// Sends a payment processed notification to the Order Orchestrator.
    /// </summary>
    /// <param name="notification">The payment result notification payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="HttpRequestException">Thrown when the notification fails.</exception>
    Task NotifyPaymentProcessedAsync(PaymentProcessedNotification notification, CancellationToken ct = default);
}
