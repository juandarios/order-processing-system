using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Domain.Entities;
using OrderOrchestrator.Domain.Enums;

namespace OrderOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Dapper-based repository for OrderSaga persistence.
/// Implements <see cref="IOrderSagaRepository"/>.
/// </summary>
public class OrderSagaRepository(IOptions<OrchestratorDatabaseOptions> options) : IOrderSagaRepository
{
    private Npgsql.NpgsqlConnection CreateConnection() =>
        new NpgsqlConnection(options.Value.ConnectionString);

    /// <inheritdoc />
    public async Task<OrderSaga?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<OrderSagaRow>(
            "SELECT * FROM order_sagas WHERE order_id = @OrderId", new { OrderId = orderId });
        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task AddAsync(OrderSaga saga, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        // ON CONFLICT DO NOTHING provides database-level idempotency as a second line of defence.
        // If a saga already exists for order_id (unique constraint), the INSERT is silently skipped.
        await conn.ExecuteAsync("""
            INSERT INTO order_sagas (id, order_id, current_state, total_amount, currency,
                                     payment_id, payment_initiated_at, timeout_at, created_at, updated_at)
            VALUES (@Id, @OrderId, @CurrentState, @TotalAmount, @Currency,
                    @PaymentId, @PaymentInitiatedAt, @TimeoutAt, @CreatedAt, @UpdatedAt)
            ON CONFLICT (order_id) DO NOTHING
            """,
            new
            {
                saga.Id,
                saga.OrderId,
                CurrentState = saga.CurrentState.ToString(),
                saga.TotalAmount,
                saga.Currency,
                saga.PaymentId,
                saga.PaymentInitiatedAt,
                saga.TimeoutAt,
                saga.CreatedAt,
                saga.UpdatedAt
            });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(OrderSaga saga, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE order_sagas
            SET current_state        = @CurrentState,
                payment_id           = @PaymentId,
                payment_initiated_at = @PaymentInitiatedAt,
                timeout_at           = @TimeoutAt,
                updated_at           = @UpdatedAt
            WHERE id = @Id
            """,
            new
            {
                saga.Id,
                CurrentState = saga.CurrentState.ToString(),
                saga.PaymentId,
                saga.PaymentInitiatedAt,
                saga.TimeoutAt,
                saga.UpdatedAt
            });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OrderSaga>> GetTimedOutSagasAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<OrderSagaRow>("""
            SELECT * FROM order_sagas
            WHERE current_state = 'PaymentPending'
            AND timeout_at < NOW()
            """);
        return rows.Select(MapToDomain);
    }

    private static OrderSaga MapToDomain(OrderSagaRow row) => new()
    {
        Id = row.id,
        OrderId = row.order_id,
        CurrentState = Enum.Parse<OrderSagaStatus>(row.current_state, ignoreCase: true),
        TotalAmount = row.total_amount,
        Currency = row.currency,
        PaymentId = row.payment_id,
        PaymentInitiatedAt = row.payment_initiated_at.HasValue
            ? new DateTimeOffset(row.payment_initiated_at.Value, TimeSpan.Zero) : null,
        TimeoutAt = row.timeout_at.HasValue
            ? new DateTimeOffset(row.timeout_at.Value, TimeSpan.Zero) : null,
        CreatedAt = new DateTimeOffset(row.created_at, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(row.updated_at, TimeSpan.Zero)
    };

    // Snake_case property names match PostgreSQL column names for Dapper constructor mapping.
    private record OrderSagaRow(
        Guid id,
        Guid order_id,
        string current_state,
        decimal total_amount,
        string currency,
        Guid? payment_id,
        DateTime? payment_initiated_at,
        DateTime? timeout_at,
        DateTime created_at,
        DateTime updated_at);
}

/// <summary>
/// Database connection options for the Order Orchestrator.
/// </summary>
public class OrchestratorDatabaseOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
