using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntake.Application.Interfaces;
using OrderIntake.Application.Logging;
using Shared.Events;
using Shared.Serialization;

namespace OrderIntake.Infrastructure.Kafka;

/// <summary>
/// Publishes order validation error events to the dedicated validation error Kafka topic.
/// Implements <see cref="IValidationErrorPublisher"/>.
/// </summary>
public class KafkaValidationErrorPublisher(
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaValidationErrorPublisher> logger) : IValidationErrorPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(OrderValidationErrorEvent errorEvent, CancellationToken ct = default)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        var json = JsonSerializer.Serialize(errorEvent, AppJsonContext.Default.OrderValidationErrorEvent);

        var kafkaMessage = new Message<string, string>
        {
            Key = errorEvent.ErrorType,
            Value = json
        };

        await producer.ProduceAsync(options.Value.ValidationErrorTopic, kafkaMessage, ct);
        logger.ValidationErrorPublished(errorEvent.ErrorType, errorEvent.SourceService);
    }
}
