namespace OrderIntake.Domain.Events;

/// <summary>
/// Domain event raised when an order has passed stock validation.
/// </summary>
/// <param name="OrderId">Unique identifier of the validated order.</param>
/// <param name="OccurredAt">UTC timestamp when validation completed.</param>
public record OrderValidatedEvent(Guid OrderId, DateTimeOffset OccurredAt);
