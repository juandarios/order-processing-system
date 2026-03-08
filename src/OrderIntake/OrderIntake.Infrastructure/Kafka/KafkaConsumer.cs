using System.Text.Json;
using Confluent.Kafka;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntake.Application.Commands.ProcessOrder;
using OrderIntake.Application.Interfaces;
using OrderIntake.Application.Logging;
using Shared.Events;
using Shared.Serialization;

namespace OrderIntake.Infrastructure.Kafka;

/// <summary>
/// Kafka consumer that listens to the order-placed topic and dispatches
/// <see cref="ProcessOrderCommand"/> for each received event.
/// </summary>
public class KafkaConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaConsumer> logger) : IEventConsumer
{
    /// <inheritdoc />
    public async Task ConsumeAsync(CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = options.Value.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.Value.OrderPlacedTopic);

        logger.LogInformation("Kafka consumer started, topic: {Topic}", options.Value.OrderPlacedTopic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                if (result?.Message?.Value is null) continue;

                var @event = JsonSerializer.Deserialize(
                    result.Message.Value,
                    AppJsonContext.Default.OrderPlacedEvent);

                if (@event is null)
                {
                    logger.LogWarning("Received null event from Kafka, skipping");
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new ProcessOrderCommand(@event), ct);

                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.KafkaConsumeError(ex);
                await Task.Delay(1000, ct);
            }
        }

        consumer.Close();
    }
}

/// <summary>
/// Configuration options for the Kafka consumer.
/// </summary>
public class KafkaConsumerOptions
{
    /// <summary>Gets or sets the Kafka bootstrap servers.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Gets or sets the consumer group ID.</summary>
    public string GroupId { get; set; } = "order-intake-group";

    /// <summary>Gets or sets the order-placed topic name.</summary>
    public string OrderPlacedTopic { get; set; } = "order-placed";
}
