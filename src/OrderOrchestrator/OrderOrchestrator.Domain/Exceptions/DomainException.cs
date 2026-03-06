namespace OrderOrchestrator.Domain.Exceptions;

/// <summary>
/// Exception thrown when an orchestrator domain business rule is violated.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class DomainException : Exception
{
    /// <param name="message">Message describing the violated rule.</param>
    public DomainException(string message) : base(message) { }
}
