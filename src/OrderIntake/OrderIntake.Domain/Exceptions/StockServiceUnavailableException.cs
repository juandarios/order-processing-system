namespace OrderIntake.Domain.Exceptions;

/// <summary>
/// Exception thrown when the Stock Service is unavailable after all resilience retries have been exhausted.
/// Maps to HTTP 503 Service Unavailable in the API layer.
/// </summary>
public class StockServiceUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="StockServiceUnavailableException"/>.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    public StockServiceUnavailableException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="StockServiceUnavailableException"/> with an inner exception.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    /// <param name="innerException">The original exception that caused this failure.</param>
    public StockServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
