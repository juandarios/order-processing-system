using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OrderProducer.Telemetry;
using Shared.Events;
using Shared.Serialization;

namespace OrderProducer.Kafka;

/// <summary>
/// Publishes order events to the Kafka order-placed topic.
/// Injects the current OpenTelemetry trace context into each Kafka message header
/// so that the distributed trace can be continued by the consumer (Order Intake).
/// </summary>
public class KafkaProducerService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaProducerService> _logger;
    private readonly TextMapPropagator _propagator;

    /// <summary>
    /// Initializes a new instance of <see cref="KafkaProducerService"/> using configuration options.
    /// Uses the SDK default propagator (<see cref="Propagators.DefaultTextMapPropagator"/>).
    /// </summary>
    /// <param name="options">Kafka producer configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public KafkaProducerService(IOptions<KafkaOptions> options, ILogger<KafkaProducerService> logger)
        : this(
            new ProducerBuilder<string, string>(
                new ProducerConfig { BootstrapServers = options.Value.BootstrapServers }).Build(),
            options.Value.OrderPlacedTopic,
            logger,
            Propagators.DefaultTextMapPropagator)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="KafkaProducerService"/> with explicit dependencies.
    /// Intended for use in unit tests where the producer and propagator can be substituted.
    /// </summary>
    /// <param name="producer">The Kafka producer instance.</param>
    /// <param name="topic">The target Kafka topic name.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="propagator">
    /// The trace context propagator. Defaults to <see cref="Propagators.DefaultTextMapPropagator"/>
    /// in production. Pass a <see cref="TraceContextPropagator"/> in unit tests.
    /// </param>
    internal KafkaProducerService(
        IProducer<string, string> producer,
        string topic,
        ILogger<KafkaProducerService> logger,
        TextMapPropagator propagator)
    {
        _producer = producer;
        _topic = topic;
        _logger = logger;
        _propagator = propagator;
    }

    /// <summary>
    /// Publishes an <see cref="OrderPlacedEvent"/> to the Kafka topic.
    /// The current OpenTelemetry trace context is injected into the message headers
    /// using W3C Trace Context propagation (<c>traceparent</c> / <c>tracestate</c>).
    /// If no activity is currently active, the message is published without trace headers.
    /// </summary>
    /// <param name="orderEvent">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ProduceException{TKey,TValue}">Thrown if publishing fails.</exception>
    public async Task PublishAsync(OrderPlacedEvent orderEvent, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(orderEvent, AppJsonContext.Default.OrderPlacedEvent);

        // Start a producer span to represent the Kafka publish operation.
        using var activity = KafkaActivitySource.Source.StartActivity(
            "order-placed publish", ActivityKind.Producer);

        var message = new Message<string, string>
        {
            Key = orderEvent.Payload.OrderId.ToString(),
            Value = json,
            Headers = new Headers()
        };

        // Inject trace context into Kafka headers (W3C Trace Context: traceparent / tracestate).
        // This propagates the distributed trace from S0 to S1 across the Kafka boundary.
        if (activity != null)
        {
            _propagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                message.Headers,
                static (headers, key, value) =>
                    headers.Add(key, Encoding.UTF8.GetBytes(value)));
        }

        var result = await _producer.ProduceAsync(_topic, message, ct);

        _logger.LogInformation(
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
