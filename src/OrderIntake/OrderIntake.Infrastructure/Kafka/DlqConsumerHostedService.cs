using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntake.Application.Logging;
using OrderIntake.Application.Models;

namespace OrderIntake.Infrastructure.Kafka;

/// <summary>
/// Background hosted service that consumes the Dead Letter Queue (DLQ) topic
/// and logs each failed message for monitoring and alerting purposes.
/// This service is for observability only — it does not retry or reprocess messages.
/// </summary>
public class DlqConsumerHostedService(
    IOptions<KafkaConsumerOptions> options,
    ILogger<DlqConsumerHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DLQ consumer service starting on topic: {Topic}", options.Value.DlqTopic);

        await Task.Run(() => ConsumeDlq(stoppingToken), stoppingToken);
    }

    private void ConsumeDlq(CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = $"{options.Value.GroupId}-dlq-monitor",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.Value.DlqTopic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                if (result?.Message?.Value is null) continue;

                var message = JsonSerializer.Deserialize(
                    result.Message.Value,
                    DlqJsonContext.Default.DlqMessage);

                if (message is not null)
                {
                    logger.DlqEntryReceived(
                        message.ErrorType,
                        message.ErrorDetail,
                        message.FailedAt,
                        message.RetryCount,
                        message.SourceService);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error consuming DLQ message");
            }
        }

        consumer.Close();
    }
}
