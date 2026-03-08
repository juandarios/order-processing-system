using System.Diagnostics;

namespace OrderProducer.Telemetry;

/// <summary>
/// Provides the ActivitySource for OpenTelemetry instrumentation in the Order Producer service.
/// </summary>
public static class KafkaActivitySource
{
    /// <summary>The name of the service, used as the ActivitySource name.</summary>
    public const string ServiceName = "order-producer";

    /// <summary>The ActivitySource used to create spans for Kafka producer operations.</summary>
    public static readonly ActivitySource Source = new(ServiceName);
}
