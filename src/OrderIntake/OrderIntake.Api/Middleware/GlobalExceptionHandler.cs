using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderIntake.Domain.Exceptions;

namespace OrderIntake.Api.Middleware;

/// <summary>
/// Global exception handler that converts exceptions to RFC 7807 ProblemDetails responses.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Domain rule violation"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            StockServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Stock service unavailable"),
            OrchestratorUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Orchestrator unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        logger.LogError(exception, "Unhandled exception: {Title}", title);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message
            }, ct);

        return true;
    }
}
