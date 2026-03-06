using OrderIntake.Domain.Entities;

namespace OrderIntake.Application.Interfaces;

/// <summary>
/// Repository contract for Order aggregate persistence.
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Retrieves an order by its unique identifier.
    /// </summary>
    /// <param name="id">The order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order, or null if not found.</returns>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Persists a new order.
    /// </summary>
    /// <param name="order">The order to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Order order, CancellationToken ct = default);

    /// <summary>
    /// Persists changes to an existing order.
    /// </summary>
    /// <param name="order">The order with updated state.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(Order order, CancellationToken ct = default);
}
