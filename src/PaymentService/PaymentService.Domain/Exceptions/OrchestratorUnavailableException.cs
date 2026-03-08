namespace PaymentService.Domain.Exceptions;

/// <summary>
/// Exception thrown when the Order Orchestrator (S3) is unavailable after all resilience retries have been exhausted.
/// Maps to HTTP 503 Service Unavailable in the API layer.
/// </summary>
public class OrchestratorUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="OrchestratorUnavailableException"/>.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    public OrchestratorUnavailableException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="OrchestratorUnavailableException"/> with an inner exception.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    /// <param name="innerException">The original exception that caused this failure.</param>
    public OrchestratorUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
