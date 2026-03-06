using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderIntake.Application.Interfaces;
using Shared.Contracts;
using Shared.Serialization;

namespace OrderIntake.Infrastructure.HttpClients;

/// <summary>
/// HTTP client adapter for the Order Orchestrator (S3).
/// Implements <see cref="IOrchestratorClient"/>.
/// </summary>
public class OrchestratorClient(
    HttpClient httpClient,
    ILogger<OrchestratorClient> logger) : IOrchestratorClient
{
    /// <inheritdoc />
    public async Task NotifyStockValidatedAsync(StockValidatedNotification notification, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(notification, AppJsonContext.Default.StockValidatedNotification);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("orchestrator/orders/stock-validated", content, ct);

        logger.LogInformation(
            "Orchestrator notified for order {OrderId} → {StatusCode}",
            notification.OrderId, (int)response.StatusCode);

        response.EnsureSuccessStatusCode();
    }
}
