using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Initializes the payment database schema on startup.
/// </summary>
public class DatabaseInitializer(IOptions<DatabaseOptions> options, ILogger<DatabaseInitializer> logger)
{
    /// <summary>
    /// Creates the payments table if it does not already exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var conn = new NpgsqlConnection(options.Value.ConnectionString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS payments (
                id                  UUID          PRIMARY KEY,
                order_id            UUID          NOT NULL UNIQUE,
                status              VARCHAR(50)   NOT NULL,
                amount              DECIMAL(18,2) NOT NULL,
                currency            VARCHAR(3)    NOT NULL,
                rejection_reason    VARCHAR(100)  NULL,
                gateway_response    VARCHAR(50)   NULL,
                created_at          TIMESTAMPTZ   NOT NULL,
                updated_at          TIMESTAMPTZ   NOT NULL
            )
            """);

        logger.LogInformation("Payment database initialized");
    }
}
