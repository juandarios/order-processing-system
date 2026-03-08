using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OrderProducer.Kafka;
using OrderProducer.Telemetry;
using Shared.Contracts;
using Shared.Events;
using Xunit;

namespace OrderProducer.UnitTests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaProducerService"/> verifying that OpenTelemetry
/// trace context is correctly injected into Kafka message headers.
/// </summary>
public class KafkaProducerServiceTests
{
    /// <summary>
    /// Creates a minimal valid <see cref="OrderPlacedEvent"/> for test use.
    /// </summary>
    private static OrderPlacedEvent CreateEvent() => new(
        EventId: Guid.NewGuid(),
        EventType: "OrderPlaced",
        OccurredAt: DateTimeOffset.UtcNow,
        Version: 1,
        Payload: new OrderPlacedPayload(
            OrderId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "test@example.com",
            ShippingAddress: new ShippingAddressDto("1 Test St", "TestCity", "US", "00001"),
            Items: [new OrderItemDto(Guid.NewGuid(), "Widget", 1, 10m, "USD")],
            TotalAmount: 10m,
            Currency: "USD"));

    /// <summary>
    /// Builds a stubbed producer that captures the message passed to ProduceAsync.
    /// </summary>
    private static (IProducer<string, string> producer, Func<Message<string, string>?> getCapture)
        BuildCapturingProducer()
    {
        var producer = Substitute.For<IProducer<string, string>>();
        Message<string, string>? captured = null;

        producer
            .ProduceAsync(
                Arg.Any<string>(),
                Arg.Do<Message<string, string>>(m => captured = m),
                Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, string>
            {
                TopicPartitionOffset = new TopicPartitionOffset("order-placed", 0, 1)
            });

        return (producer, () => captured);
    }

    /// <summary>
    /// Verifies that when an Activity is active (sampling is enabled), the produced Kafka message
    /// contains a <c>traceparent</c> header with a valid W3C Trace Context value.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenActivityIsActive_InjectsTraceContextInKafkaHeaders()
    {
        // Arrange
        // Register a listener that enables sampling for ALL ActivitySources,
        // including KafkaActivitySource.Source ("order-producer").
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        var logger = Substitute.For<ILogger<KafkaProducerService>>();
        var (producer, getCapture) = BuildCapturingProducer();

        // Use TraceContextPropagator directly — equivalent to the SDK default in production.
        using var sut = new KafkaProducerService(
            producer, "order-placed", logger, new TraceContextPropagator());

        // Act
        await sut.PublishAsync(CreateEvent());
        var capturedMessage = getCapture();

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Headers.Should().NotBeNull();

        var traceparentHeader = capturedMessage.Headers.FirstOrDefault(h => h.Key == "traceparent");
        traceparentHeader.Should().NotBeNull(
            "traceparent header must be injected when activity sampling is enabled and an activity is started");

        var traceparentValue = Encoding.UTF8.GetString(traceparentHeader!.GetValueBytes());
        // W3C traceparent format: 00-<traceId(32 hex)>-<spanId(16 hex)>-<flags(2 hex)>
        traceparentValue.Should().MatchRegex(
            @"^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$",
            "traceparent must conform to the W3C Trace Context specification");
    }

    /// <summary>
    /// Verifies that when no ActivityListener is registered (simulating a service with no
    /// OpenTelemetry SDK configured), <see cref="ActivitySource.StartActivity"/> returns
    /// <see langword="null"/> and the produced Kafka message is published without trace headers.
    /// This test does NOT register an ActivityListener so that <c>StartActivity</c> returns null.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenNoActivityListenerIsRegistered_PublishesMessageWithoutTraceHeaders()
    {
        // Arrange — no ActivityListener is registered in this test, so KafkaActivitySource.Source
        // .StartActivity(...) will return null, and the propagation branch is skipped.
        Activity.Current = null;

        var logger = Substitute.For<ILogger<KafkaProducerService>>();
        var (producer, getCapture) = BuildCapturingProducer();

        using var sut = new KafkaProducerService(
            producer, "order-placed", logger, new TraceContextPropagator());

        // Act
        var act = async () => await sut.PublishAsync(CreateEvent());

        // Assert
        await act.Should().NotThrowAsync(
            "publishing without a trace context must be handled gracefully");

        var capturedMessage = getCapture();
        capturedMessage.Should().NotBeNull();

        // When no activity is started the service must not inject any traceparent header.
        var traceparentHeader = capturedMessage!.Headers.FirstOrDefault(h => h.Key == "traceparent");
        traceparentHeader.Should().BeNull(
            "no trace context should be injected when StartActivity returns null (no listener registered)");
    }
}
