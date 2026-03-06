using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace OrderOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Initializes the orchestrator database schema on startup.
/// </summary>
public class DatabaseInitializer(IOptions<OrchestratorDatabaseOptions> options, ILogger<DatabaseInitializer> logger)
{
    /// <summary>
    /// Creates the order_sagas table if it does not already exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var conn = new NpgsqlConnection(options.Value.ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS order_sagas (
                id                   UUID          PRIMARY KEY,
                order_id             UUID          NOT NULL UNIQUE,
                current_state        VARCHAR(50)   NOT NULL,
                total_amount         DECIMAL(18,2) NOT NULL,
                currency             VARCHAR(3)    NOT NULL,
                payment_id           UUID          NULL,
                payment_initiated_at TIMESTAMPTZ   NULL,
                timeout_at           TIMESTAMPTZ   NULL,
                created_at           TIMESTAMPTZ   NOT NULL,
                updated_at           TIMESTAMPTZ   NOT NULL
            )
            """);

        logger.LogInformation("Orchestrator database initialized");
    }
}
