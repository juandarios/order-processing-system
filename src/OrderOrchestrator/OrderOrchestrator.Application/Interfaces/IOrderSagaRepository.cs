using OrderOrchestrator.Domain.Entities;

namespace OrderOrchestrator.Application.Interfaces;

/// <summary>
/// Repository contract for OrderSaga persistence (Dapper).
/// Defined in Application layer; implemented in Infrastructure.
/// </summary>
public interface IOrderSagaRepository
{
    /// <summary>Retrieves a saga by its order identifier.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OrderSaga?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Persists a new saga.</summary>
    /// <param name="saga">The saga to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(OrderSaga saga, CancellationToken ct = default);

    /// <summary>Persists changes to an existing saga.</summary>
    /// <param name="saga">The saga with updated state.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(OrderSaga saga, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all sagas in PaymentPending state whose timeout has passed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<OrderSaga>> GetTimedOutSagasAsync(CancellationToken ct = default);
}
