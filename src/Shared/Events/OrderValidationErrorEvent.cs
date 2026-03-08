namespace Shared.Events;

/// <summary>
/// Event published to the validation error topic when an order fails business validation.
/// Carries the original raw message and the reason for rejection.
/// </summary>
/// <param name="OriginalMessage">The raw Kafka message that failed validation.</param>
/// <param name="ErrorType">Short classification of the error (e.g. DomainValidation, StockConflict).</param>
/// <param name="ErrorDetail">Human-readable description of the validation failure.</param>
/// <param name="OccurredAt">UTC timestamp when the validation error was detected.</param>
/// <param name="SourceService">Name of the service that detected the validation failure.</param>
public record OrderValidationErrorEvent(
    string OriginalMessage,
    string ErrorType,
    string ErrorDetail,
    DateTimeOffset OccurredAt,
    string SourceService);
