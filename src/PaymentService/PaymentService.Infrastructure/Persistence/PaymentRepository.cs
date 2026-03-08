using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Dapper-based repository for payment persistence.
/// Implements <see cref="IPaymentRepository"/>.
/// </summary>
public class PaymentRepository(IOptions<DatabaseOptions> options) : IPaymentRepository
{
    private IDbConnection CreateConnection() => new NpgsqlConnection(options.Value.ConnectionString);

    /// <inheritdoc />
    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<PaymentRow>(
            "SELECT * FROM payments WHERE id = @Id", new { Id = id });
        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<PaymentRow>(
            "SELECT * FROM payments WHERE order_id = @OrderId", new { OrderId = orderId });
        return row is null ? null : MapToDomain(row);
    }

    /// <inheritdoc />
    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        // ON CONFLICT DO NOTHING provides database-level idempotency as a second line of defence.
        // If a payment already exists for order_id (unique constraint), the INSERT is silently skipped.
        await conn.ExecuteAsync("""
            INSERT INTO payments (id, order_id, status, amount, currency, rejection_reason, gateway_response, created_at, updated_at)
            VALUES (@Id, @OrderId, @Status, @Amount, @Currency, @RejectionReason, @GatewayResponse, @CreatedAt, @UpdatedAt)
            ON CONFLICT (order_id) DO NOTHING
            """,
            new
            {
                payment.Id,
                payment.OrderId,
                Status = payment.Status.ToString(),
                payment.Amount,
                payment.Currency,
                payment.RejectionReason,
                payment.GatewayResponse,
                payment.CreatedAt,
                payment.UpdatedAt
            });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE payments
            SET status = @Status,
                rejection_reason = @RejectionReason,
                gateway_response = @GatewayResponse,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new
            {
                payment.Id,
                Status = payment.Status.ToString(),
                payment.RejectionReason,
                payment.GatewayResponse,
                payment.UpdatedAt
            });
    }

    private static Payment MapToDomain(PaymentRow row)
    {
        var payment = Payment.Create(row.id, row.order_id, row.amount, row.currency);

        var statusEnum = Enum.Parse<PaymentStatus>(row.status, ignoreCase: true);
        if (statusEnum == PaymentStatus.Approved)
            payment.Approve(row.gateway_response ?? string.Empty);
        else if (statusEnum == PaymentStatus.Rejected)
            payment.Reject(row.rejection_reason ?? "unknown", row.gateway_response ?? string.Empty);
        else if (statusEnum == PaymentStatus.Expired)
            payment.Expire();

        return payment;
    }

    // Snake_case property names match PostgreSQL column names for Dapper constructor mapping.
    private record PaymentRow(
        Guid id,
        Guid order_id,
        string status,
        decimal amount,
        string currency,
        string? rejection_reason,
        string? gateway_response,
        DateTime created_at,
        DateTime updated_at);
}

/// <summary>
/// Database connection options for the Payment Service.
/// </summary>
public class DatabaseOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
