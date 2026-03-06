using Shared.Contracts;

namespace OrderIntake.Application.Interfaces;

/// <summary>
/// HTTP client contract for sending notifications to the Order Orchestrator (S3).
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IOrchestratorClient
{
    /// <summary>
    /// Sends a stock validation result notification to the Order Orchestrator.
    /// </summary>
    /// <param name="notification">The stock validation notification payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="HttpRequestException">Thrown when the notification fails.</exception>
    Task NotifyStockValidatedAsync(StockValidatedNotification notification, CancellationToken ct = default);
}
