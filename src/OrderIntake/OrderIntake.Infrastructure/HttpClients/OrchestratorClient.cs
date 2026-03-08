using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderIntake.Application.Interfaces;
using OrderIntake.Domain.Exceptions;
using Shared.Contracts;
using Shared.Serialization;

namespace OrderIntake.Infrastructure.HttpClients;

// TODO: Implement Outbox Pattern for prolonged unavailability

/// <summary>
/// HTTP client adapter for the Order Orchestrator (S3).
/// Implements <see cref="IOrchestratorClient"/>.
/// Polly resilience is applied at the HttpClient level (see Program.cs).
/// When all retries are exhausted, <see cref="OrchestratorUnavailableException"/> is thrown.
/// </summary>
public class OrchestratorClient(
    HttpClient httpClient,
    ILogger<OrchestratorClient> logger) : IOrchestratorClient
{
    /// <inheritdoc />
    public async Task NotifyStockValidatedAsync(StockValidatedNotification notification, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(notification, AppJsonContext.Default.StockValidatedNotification);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("orchestrator/orders/stock-validated", content, ct);

            logger.LogInformation(
                "Orchestrator notified for order {OrderId} → {StatusCode}",
                notification.OrderId, (int)response.StatusCode);

            response.EnsureSuccessStatusCode();
        }
        catch (OrchestratorUnavailableException)
        {
            throw; // already wrapped, propagate as-is
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Polly exhausted all retries — wrap so the consumer can identify the failure type
            throw new OrchestratorUnavailableException(
                $"Orchestrator unavailable for order {notification.OrderId} after all retries: {ex.Message}", ex);
        }
    }
}
