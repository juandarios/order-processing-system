namespace PaymentService.Domain.Exceptions;

/// <summary>
/// Exception thrown when the Payment Gateway is unavailable after all resilience retries have been exhausted.
/// Maps to HTTP 503 Service Unavailable in the API layer.
/// </summary>
public class PaymentGatewayUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="PaymentGatewayUnavailableException"/>.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    public PaymentGatewayUnavailableException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentGatewayUnavailableException"/> with an inner exception.
    /// </summary>
    /// <param name="message">A message describing the unavailability.</param>
    /// <param name="innerException">The original exception that caused this failure.</param>
    public PaymentGatewayUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
