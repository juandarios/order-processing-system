using Mediator;

namespace OrderIntake.Application.Queries.GetOrder;

/// <summary>
/// Query to retrieve an order by its unique identifier.
/// </summary>
/// <param name="OrderId">The unique identifier of the order to retrieve.</param>
public record GetOrderQuery(Guid OrderId) : IQuery<GetOrderResponse?>;

/// <summary>
/// Response DTO for the GetOrderQuery.
/// </summary>
/// <param name="OrderId">Unique identifier of the order.</param>
/// <param name="Status">Current status of the order.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="CustomerEmail">Customer email address.</param>
/// <param name="TotalAmount">Total order amount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="CreatedAt">UTC timestamp of order creation.</param>
public record GetOrderResponse(
    Guid OrderId,
    string Status,
    Guid CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt);
