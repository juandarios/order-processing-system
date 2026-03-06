namespace OrderIntake.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// Maps to HTTP 404 Not Found in the API layer.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="NotFoundException"/> for the given entity.
    /// </summary>
    /// <param name="entityName">Name of the entity type that was not found.</param>
    /// <param name="id">The identifier that was looked up.</param>
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.") { }
}
