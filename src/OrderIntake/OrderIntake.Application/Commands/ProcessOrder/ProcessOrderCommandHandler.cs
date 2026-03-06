using MediatR;
using Microsoft.Extensions.Logging;
using OrderIntake.Application.Interfaces;
using OrderIntake.Application.Logging;
using OrderIntake.Domain.Entities;
using OrderIntake.Domain.ValueObjects;
using Shared.Contracts;
using Shared.Events;

namespace OrderIntake.Application.Commands.ProcessOrder;

/// <summary>
/// Handles the <see cref="ProcessOrderCommand"/>:
/// 1. Persists the order.
/// 2. Validates stock for each line item.
/// 3. Notifies the orchestrator with the result.
/// </summary>
public class ProcessOrderCommandHandler(
    IOrderRepository orderRepository,
    IStockServiceClient stockClient,
    IOrchestratorClient orchestratorClient,
    ILogger<ProcessOrderCommandHandler> logger)
    : IRequestHandler<ProcessOrderCommand>
{
    /// <summary>
    /// Processes the incoming order event.
    /// </summary>
    /// <param name="request">The command containing the event payload.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(ProcessOrderCommand request, CancellationToken ct)
    {
        var payload = request.Event.Payload;

        var order = Order.Create(
            id: payload.OrderId,
            customerId: payload.CustomerId,
            customerEmail: payload.CustomerEmail,
            total: new Money(payload.TotalAmount, payload.Currency),
            shippingAddress: new Address(
                payload.ShippingAddress.Street,
                payload.ShippingAddress.City,
                payload.ShippingAddress.Country,
                payload.ShippingAddress.ZipCode),
            lines: payload.Items.Select(i => new OrderLine(
                i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Currency)));

        await orderRepository.AddAsync(order, ct);
        logger.OrderReceived(order.Id);

        var stockResults = new List<StockItemValidationResult>();
        bool allInStock = true;

        foreach (var item in payload.Items)
        {
            bool available = await stockClient.IsAvailableAsync(item.ProductId, item.Quantity, ct);
            stockResults.Add(new StockItemValidationResult(item.ProductId, item.Quantity, available));

            if (!available)
            {
                allInStock = false;
                logger.StockUnavailable(order.Id, item.ProductId);
            }
        }

        if (allInStock)
        {
            order.MarkStockValidated();
            logger.OrderStockValidated(order.Id);
        }
        else
        {
            order.Cancel();
            logger.OrderCancelled(order.Id);
        }

        await orderRepository.UpdateAsync(order, ct);

        var notification = new StockValidatedNotification(
            OrderId: order.Id,
            StockValidated: allInStock,
            TotalAmount: payload.TotalAmount,
            Currency: payload.Currency,
            Items: stockResults,
            OccurredAt: DateTimeOffset.UtcNow);

        await orchestratorClient.NotifyStockValidatedAsync(notification, ct);
        logger.OrchestratorNotified(order.Id, allInStock);
    }
}
