using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Shared.Events;
using Shared.Serialization;

namespace OrderProducer.Kafka;

/// <summary>
/// Publishes order events to the Kafka order-placed topic.
/// </summary>
public class KafkaProducerService(
    IOptions<KafkaOptions> options,
    ILogger<KafkaProducerService> logger) : IDisposable
{
    private readonly IProducer<string, string> _producer = new ProducerBuilder<string, string>(
        new ProducerConfig { BootstrapServers = options.Value.BootstrapServers })
        .Build();

    private readonly string _topic = options.Value.OrderPlacedTopic;

    /// <summary>
    /// Publishes an <see cref="OrderPlacedEvent"/> to the Kafka topic.
    /// </summary>
    /// <param name="orderEvent">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ProduceException{TKey,TValue}">Thrown if publishing fails.</exception>
    public async Task PublishAsync(OrderPlacedEvent orderEvent, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(orderEvent, AppJsonContext.Default.OrderPlacedEvent);
        var message = new Message<string, string>
        {
            Key = orderEvent.Payload.OrderId.ToString(),
            Value = json
        };

        var result = await _producer.ProduceAsync(_topic, message, ct);

        logger.LogInformation(
            "Published OrderPlaced event {EventId} for order {OrderId} to partition {Partition} offset {Offset}",
            orderEvent.EventId, orderEvent.Payload.OrderId, result.Partition.Value, result.Offset.Value);
    }

    /// <inheritdoc />
    public void Dispose() => _producer.Dispose();
}

/// <summary>
/// Configuration options for the Kafka producer.
/// </summary>
public class KafkaOptions
{
    /// <summary>Gets or sets the Kafka bootstrap servers connection string.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Gets or sets the topic name for order-placed events.</summary>
    public string OrderPlacedTopic { get; set; } = "order-placed";
}
