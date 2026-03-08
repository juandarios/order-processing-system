namespace OrderIntake.Application.Models;

/// <summary>
/// Envelope for a message that could not be processed and must be sent to the Dead Letter Queue.
/// </summary>
/// <param name="OriginalMessage">The raw original Kafka message value that failed.</param>
/// <param name="ErrorType">
/// Classification of the failure:
/// <c>DeserializationError</c> — message could not be deserialized;
/// <c>ValidationError</c> — business validation failed;
/// <c>DuplicateOrder</c> — order already exists in the database;
/// <c>TransientError</c> — transient infrastructure error after Polly retries exhausted.
/// </param>
/// <param name="ErrorDetail">Human-readable description of the specific error.</param>
/// <param name="FailedAt">UTC timestamp when the failure occurred.</param>
/// <param name="RetryCount">Number of processing attempts made before sending to DLQ.</param>
/// <param name="SourceService">Name of the service that produced this DLQ entry.</param>
public record DlqMessage(
    string OriginalMessage,
    string ErrorType,
    string ErrorDetail,
    DateTimeOffset FailedAt,
    int RetryCount,
    string SourceService);
