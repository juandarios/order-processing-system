using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Xunit;

namespace OrderIntake.UnitTests.Infrastructure;

/// <summary>
/// Unit tests verifying that W3C Trace Context extraction logic works correctly
/// for the Kafka consumer trace propagation implemented in <c>KafkaConsumer</c>.
/// These tests exercise the propagation helper logic in isolation — without spinning
/// up a full Kafka consumer loop — to keep them fast and deterministic.
/// </summary>
public class KafkaTracePropagationTests
{
    /// <summary>
    /// The W3C Trace Context propagator used in production code.
    /// Using the concrete <see cref="TraceContextPropagator"/> directly in tests
    /// avoids a dependency on SDK-level configuration of the default propagator.
    /// </summary>
    private static readonly TraceContextPropagator Propagator = new();

    /// <summary>
    /// Helper that replicates the extraction delegate used in <c>KafkaConsumer.ConsumeAsync</c>,
    /// using the W3C Trace Context propagator directly.
    /// </summary>
    private static PropagationContext ExtractContext(Headers headers) =>
        Propagator.Extract(
            default,
            headers,
            static (hdrs, key) =>
            {
                var header = hdrs.FirstOrDefault(h => h.Key == key);
                return header != null
                    ? new[] { Encoding.UTF8.GetString(header.GetValueBytes()) }
                    : Array.Empty<string>();
            });

    /// <summary>
    /// Helper that replicates the injection delegate used in <c>KafkaProducerService.PublishAsync</c>,
    /// using the W3C Trace Context propagator directly.
    /// </summary>
    private static Headers InjectContext(ActivityContext activityContext)
    {
        var headers = new Headers();
        Propagator.Inject(
            new PropagationContext(activityContext, Baggage.Current),
            headers,
            static (hdrs, key, value) => hdrs.Add(key, Encoding.UTF8.GetBytes(value)));
        return headers;
    }

    /// <summary>
    /// Verifies that when a Kafka message contains a valid <c>traceparent</c> header,
    /// the extraction produces a non-default <see cref="ActivityContext"/> that preserves
    /// the original trace ID and span ID.
    /// </summary>
    [Fact]
    public void ExtractContext_WhenMessageHasValidTraceparentHeader_ReturnsPopulatedActivityContext()
    {
        // Arrange — create a real ActivityContext with a known trace ID and span ID.
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        using var source = new ActivitySource("test-source");
        using var activity = source.StartActivity("test-span", ActivityKind.Producer);
        activity.Should().NotBeNull("the listener must sample all sources");

        var originalTraceId = activity!.TraceId.ToHexString();
        var originalSpanId  = activity.SpanId.ToHexString();

        // Inject the context into Kafka headers (same way the producer does it).
        var headers = InjectContext(activity.Context);

        // Act — extract the context from headers (same way the consumer does it).
        var extracted = ExtractContext(headers);

        // Assert
        extracted.ActivityContext.IsValid()
            .Should().BeTrue("a valid traceparent header must yield a valid ActivityContext");

        extracted.ActivityContext.TraceId.ToHexString()
            .Should().Be(originalTraceId, "the extracted trace ID must match the injected one");

        extracted.ActivityContext.SpanId.ToHexString()
            .Should().Be(originalSpanId, "the extracted span ID must match the injected parent span ID");
    }

    /// <summary>
    /// Verifies that when a Kafka message has no trace headers, the extraction
    /// returns a default (invalid) <see cref="ActivityContext"/> without throwing.
    /// </summary>
    [Fact]
    public void ExtractContext_WhenMessageHasNoTraceHeaders_ReturnsDefaultActivityContext()
    {
        // Arrange
        var headers = new Headers(); // empty — no traceparent

        // Act
        var extracted = ExtractContext(headers);

        // Assert
        extracted.ActivityContext.IsValid()
            .Should().BeFalse("an empty header collection must yield a default (invalid) ActivityContext");
    }

    /// <summary>
    /// Verifies that starting an <see cref="Activity"/> with a default (invalid) parent context
    /// succeeds and does not throw, matching the behaviour of the consumer when no trace headers
    /// are present in the incoming Kafka message.
    /// </summary>
    [Fact]
    public void StartActivity_WithDefaultParentContext_DoesNotThrow()
    {
        // Arrange
        using var source = new ActivitySource("test-consumer-source");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var defaultContext = default(ActivityContext); // represents "no parent"

        // Act
        var act = () =>
        {
            using var activity = source.StartActivity(
                "order-placed consume",
                ActivityKind.Consumer,
                defaultContext);
            // activity may be null when there is no listener — that is acceptable.
        };

        // Assert
        act.Should().NotThrow(
            "starting an activity with a default parent context must never throw an exception");
    }
}
