using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntake.Application.Interfaces;
using OrderIntake.Application.Logging;
using OrderIntake.Application.Models;

namespace OrderIntake.Infrastructure.Kafka;

/// <summary>
/// Publishes failed messages to the Dead Letter Queue (DLQ) Kafka topic.
/// Implements <see cref="IDlqPublisher"/>.
/// </summary>
public class KafkaDlqPublisher(
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaDlqPublisher> logger) : IDlqPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(DlqMessage message, CancellationToken ct = default)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        var json = JsonSerializer.Serialize(message, DlqJsonContext.Default.DlqMessage);

        var kafkaMessage = new Message<string, string>
        {
            Key = message.ErrorType,
            Value = json
        };

        await producer.ProduceAsync(options.Value.DlqTopic, kafkaMessage, ct);
        logger.DlqMessagePublished(message.ErrorType, message.SourceService);
    }
}

/// <summary>
/// Source-generated JSON serialization context for DLQ messages.
/// </summary>
[JsonSerializable(typeof(DlqMessage))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class DlqJsonContext : JsonSerializerContext;
