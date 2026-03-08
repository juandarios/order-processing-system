using System.Diagnostics;

namespace OrderIntake.Infrastructure.Telemetry;

/// <summary>
/// Provides the ActivitySource for OpenTelemetry instrumentation in the Order Intake service.
/// </summary>
public static class KafkaActivitySource
{
    /// <summary>The name of the service, used as the ActivitySource name.</summary>
    public const string ServiceName = "order-intake";

    /// <summary>The ActivitySource used to create spans for Kafka consumer operations.</summary>
    public static readonly ActivitySource Source = new(ServiceName);
}
