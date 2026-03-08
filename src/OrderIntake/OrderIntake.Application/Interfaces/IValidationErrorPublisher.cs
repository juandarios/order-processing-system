using Shared.Events;

namespace OrderIntake.Application.Interfaces;

/// <summary>
/// Contract for publishing order validation errors to the dedicated validation error topic.
/// Validation errors are business rule violations that should not be retried — they represent
/// orders that are structurally invalid or violate domain constraints.
/// </summary>
public interface IValidationErrorPublisher
{
    /// <summary>
    /// Publishes an order validation error event to the validation error topic.
    /// </summary>
    /// <param name="errorEvent">The validation error event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync(OrderValidationErrorEvent errorEvent, CancellationToken ct = default);
}
