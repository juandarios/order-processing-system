using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderIntake.Application.Interfaces;

namespace OrderIntake.Infrastructure.Kafka;

/// <summary>
/// Background hosted service that runs the Kafka consumer loop.
/// Resolves the keyed <see cref="IEventConsumer"/> to support swapping in tests.
/// </summary>
public class KafkaConsumerHostedService(
    [FromKeyedServices("kafka")] IEventConsumer consumer,
    ILogger<KafkaConsumerHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("KafkaConsumerHostedService starting");
        try
        {
            await consumer.ConsumeAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KafkaConsumerHostedService terminated with error");
        }
    }
}
