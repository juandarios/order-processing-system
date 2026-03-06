using MediatR;
using OrderIntake.Application.Interfaces;

namespace OrderIntake.Application.Queries.GetOrder;

/// <summary>
/// Handles the <see cref="GetOrderQuery"/> by retrieving an order from the repository.
/// </summary>
public class GetOrderQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderQuery, GetOrderResponse?>
{
    /// <summary>
    /// Retrieves an order by identifier.
    /// </summary>
    /// <param name="request">The query containing the order ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order response DTO, or null if not found.</returns>
    public async Task<GetOrderResponse?> Handle(GetOrderQuery request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null) return null;

        return new GetOrderResponse(
            order.Id,
            order.Status.ToString(),
            order.CustomerId,
            order.CustomerEmail,
            order.Total.Amount,
            order.Total.Currency,
            order.CreatedAt);
    }
}
