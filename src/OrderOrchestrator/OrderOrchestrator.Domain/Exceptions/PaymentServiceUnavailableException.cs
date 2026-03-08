namespace OrderOrchestrator.Domain.Exceptions;

/// <summary>
/// Exception thrown when the Payment Service (S2) is unavailable after all resilience retries have been exhausted.
/// Maps to HTTP 503 Service Unavailable in the API layer.
/// </summary>
public class PaymentServiceUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="PaymentServiceUnavailableException"/>.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    public PaymentServiceUnavailableException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentServiceUnavailableException"/> with an inner exception.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    /// <param name="innerException">The original exception that caused this failure.</param>
    public PaymentServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
