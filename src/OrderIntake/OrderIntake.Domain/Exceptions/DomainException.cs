namespace OrderIntake.Domain.Exceptions;

/// <summary>
/// Exception thrown when a domain business rule is violated.
/// Maps to HTTP 400 Bad Request in the API layer.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="DomainException"/> with the specified message.
    /// </summary>
    /// <param name="message">A message describing the violated business rule.</param>
    public DomainException(string message) : base(message) { }
}
