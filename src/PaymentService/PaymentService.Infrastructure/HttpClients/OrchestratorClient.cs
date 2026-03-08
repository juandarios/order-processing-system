using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using Shared.Contracts;
using Shared.Serialization;

namespace PaymentService.Infrastructure.HttpClients;

// TODO: Implement Outbox Pattern for prolonged unavailability

/// <summary>
/// HTTP client adapter for the Order Orchestrator (S3).
/// Implements <see cref="IOrchestratorClient"/>.
/// </summary>
public class OrchestratorClient(
    HttpClient httpClient,
    ILogger<OrchestratorClient> logger) : IOrchestratorClient
{
    /// <inheritdoc />
    public async Task NotifyPaymentProcessedAsync(PaymentProcessedNotification notification, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(notification, AppJsonContext.Default.PaymentProcessedNotification);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("orchestrator/orders/payment-processed", content, ct);

        logger.LogInformation(
            "Orchestrator notified for order {OrderId} payment {PaymentId} → {StatusCode}",
            notification.OrderId, notification.PaymentId, (int)response.StatusCode);

        response.EnsureSuccessStatusCode();
    }
}
