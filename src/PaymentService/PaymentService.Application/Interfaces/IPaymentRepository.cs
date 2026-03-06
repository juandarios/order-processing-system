using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Repository contract for Payment persistence using Dapper.
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Retrieves a payment by its unique identifier.
    /// </summary>
    /// <param name="id">The payment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The payment, or null if not found.</returns>
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a payment by the associated order identifier.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The payment, or null if not found.</returns>
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new payment.
    /// </summary>
    /// <param name="payment">The payment to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Payment payment, CancellationToken ct = default);

    /// <summary>
    /// Persists changes to an existing payment.
    /// </summary>
    /// <param name="payment">The payment with updated state.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
