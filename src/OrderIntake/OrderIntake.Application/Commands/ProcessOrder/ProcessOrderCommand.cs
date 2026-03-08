using Mediator;
using Shared.Events;

namespace OrderIntake.Application.Commands.ProcessOrder;

/// <summary>
/// Command to process an incoming order-placed event: persist the order, validate stock,
/// and notify the orchestrator.
/// </summary>
/// <param name="Event">The raw OrderPlaced event from Kafka.</param>
public record ProcessOrderCommand(OrderPlacedEvent Event) : ICommand;
