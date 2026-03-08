using Mediator;
using Shared.Contracts;

namespace OrderOrchestrator.Application.Commands.ProcessStockValidated;

/// <summary>
/// Command to process a stock-validated notification from Order Intake (S1).
/// Creates or updates the order saga and triggers payment if stock is available.
/// </summary>
/// <param name="Notification">The stock validation notification from S1.</param>
public record ProcessStockValidatedCommand(StockValidatedNotification Notification) : ICommand;
