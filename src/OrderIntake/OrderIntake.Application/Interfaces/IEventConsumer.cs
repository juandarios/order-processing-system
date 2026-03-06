namespace OrderIntake.Application.Interfaces;

/// <summary>
/// Contract for consuming events from a message broker or in-memory source.
/// Use keyed services to swap between Kafka (production) and in-memory (tests).
/// </summary>
public interface IEventConsumer
{
    /// <summary>
    /// Starts the event consumption loop.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the consumer.</param>
    Task ConsumeAsync(CancellationToken ct);
}
