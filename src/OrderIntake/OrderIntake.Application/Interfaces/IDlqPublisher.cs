using OrderIntake.Application.Models;

namespace OrderIntake.Application.Interfaces;

/// <summary>
/// Contract for publishing failed messages to the Dead Letter Queue (DLQ).
/// Messages that cannot be processed (deserialization errors, validation errors,
/// duplicates, or transient errors after retry exhaustion) are routed here.
/// </summary>
public interface IDlqPublisher
{
    /// <summary>
    /// Publishes a failed message envelope to the DLQ topic.
    /// </summary>
    /// <param name="message">The DLQ envelope containing the original message and error details.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync(DlqMessage message, CancellationToken ct = default);
}
