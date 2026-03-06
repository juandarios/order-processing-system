namespace OrderOrchestrator.Domain.Exceptions;

/// <summary>
/// Exception thrown when an orchestrator resource is not found.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    /// <param name="entityName">Entity type name.</param>
    /// <param name="id">The identifier looked up.</param>
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.") { }
}
