using System.Text.Json;
using Confluent.Kafka;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderIntake.Application.Commands.ProcessOrder;
using OrderIntake.Application.Interfaces;
using OrderIntake.Application.Logging;
using OrderIntake.Application.Models;
using OrderIntake.Domain.Exceptions;
using Shared.Events;
using Shared.Serialization;

namespace OrderIntake.Infrastructure.Kafka;

/// <summary>
/// Kafka consumer that listens to the order-placed topic and dispatches
/// <see cref="ProcessOrderCommand"/> for each received event.
/// Failed messages are routed to the Dead Letter Queue (DLQ) via <see cref="IDlqPublisher"/>.
/// </summary>
public class KafkaConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaConsumer> logger) : IEventConsumer
{
    private const string SourceService = "order-intake";

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
            ConsumeResult<string, string>? result = null;

            try
            {
                result = consumer.Consume(ct);
                if (result?.Message?.Value is null) continue;

                OrderPlacedEvent? @event = null;

                try
                {
                    @event = JsonSerializer.Deserialize(
                        result.Message.Value,
                        AppJsonContext.Default.OrderPlacedEvent);
                }
                catch (JsonException jsonEx)
                {
                    // Deserialization error → DLQ immediately, no retry
                    logger.MessageRoutedToDlq("DeserializationError", jsonEx.Message);
                    await SendToDlqAsync(result.Message.Value, "DeserializationError", jsonEx.Message, 0, ct);
                    consumer.Commit(result);
                    continue;
                }

                if (@event is null)
                {
                    logger.LogWarning("Received null event from Kafka, skipping");
                    consumer.Commit(result);
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                try
                {
                    await mediator.Send(new ProcessOrderCommand(@event), ct);
                }
                catch (Exception handlerEx)
                {
                    // Determine error type based on exception
                    var errorType = handlerEx is DomainException ? "ValidationError" : "TransientError";
                    logger.MessageRoutedToDlq(errorType, handlerEx.Message);
                    await SendToDlqAsync(result.Message.Value, errorType, handlerEx.Message, 0, ct);
                }

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

    private async Task SendToDlqAsync(
        string originalMessage,
        string errorType,
        string errorDetail,
        int retryCount,
        CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dlqPublisher = scope.ServiceProvider.GetRequiredService<IDlqPublisher>();

            var dlqMessage = new DlqMessage(
                OriginalMessage: originalMessage,
                ErrorType: errorType,
                ErrorDetail: errorDetail,
                FailedAt: DateTimeOffset.UtcNow,
                RetryCount: retryCount,
                SourceService: SourceService);

            await dlqPublisher.PublishAsync(dlqMessage, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish message to DLQ. ErrorType={ErrorType}", errorType);
        }
    }
}

/// <summary>
/// Configuration options for the Kafka consumer, including DLQ topic.
/// </summary>
public class KafkaConsumerOptions
{
    /// <summary>Gets or sets the Kafka bootstrap servers.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Gets or sets the consumer group ID.</summary>
    public string GroupId { get; set; } = "order-intake-group";

    /// <summary>Gets or sets the order-placed topic name.</summary>
    public string OrderPlacedTopic { get; set; } = "order-placed";

    /// <summary>Gets or sets the dead letter queue topic name.</summary>
    public string DlqTopic { get; set; } = "order-placed-dlq";
}
