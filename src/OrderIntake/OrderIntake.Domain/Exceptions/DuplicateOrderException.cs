namespace OrderIntake.Domain.Exceptions;

/// <summary>
/// Exception thrown when an order with the same identifier has already been processed.
/// Routes to the Dead Letter Queue with error type <c>DuplicateOrder</c> so that the Kafka
/// offset is committed without retrying or re-processing the message.
/// </summary>
public class DuplicateOrderException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="DuplicateOrderException"/> for the given order.
    /// </summary>
    /// <param name="orderId">The duplicate order identifier.</param>
    public DuplicateOrderException(Guid orderId)
        : base($"Order {orderId} has already been processed and will not be re-inserted.") { }
}
