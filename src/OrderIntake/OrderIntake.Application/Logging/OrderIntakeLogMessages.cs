using Microsoft.Extensions.Logging;

namespace OrderIntake.Application.Logging;

/// <summary>
/// Source-generated structured log messages for Order Intake service.
/// </summary>
public static partial class OrderIntakeLogMessages
{
    /// <summary>Logs that an order was received from Kafka.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} received and persisted")]
    public static partial void OrderReceived(this ILogger logger, Guid orderId);

    /// <summary>Logs that stock was confirmed for an order.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} stock validated successfully")]
    public static partial void OrderStockValidated(this ILogger logger, Guid orderId);

    /// <summary>Logs that an order was cancelled due to stock shortage.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Order {OrderId} cancelled due to insufficient stock")]
    public static partial void OrderCancelled(this ILogger logger, Guid orderId);

    /// <summary>Logs that a specific product is unavailable.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Product {ProductId} unavailable for order {OrderId}")]
    public static partial void StockUnavailable(this ILogger logger, Guid orderId, Guid productId);

    /// <summary>Logs that the orchestrator was notified.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Orchestrator notified for order {OrderId}, stockValidated={StockValidated}")]
    public static partial void OrchestratorNotified(this ILogger logger, Guid orderId, bool stockValidated);

    /// <summary>Logs a Kafka consumer error.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Error consuming Kafka message for order")]
    public static partial void KafkaConsumeError(this ILogger logger, Exception ex);

    /// <summary>Logs that a message was routed to the DLQ.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Message routed to DLQ. ErrorType={ErrorType}, Detail={ErrorDetail}")]
    public static partial void MessageRoutedToDlq(this ILogger logger, string errorType, string errorDetail);

    /// <summary>Logs that a DLQ message was successfully published.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "DLQ message published. ErrorType={ErrorType}, Source={SourceService}")]
    public static partial void DlqMessagePublished(this ILogger logger, string errorType, string sourceService);

    /// <summary>Logs a DLQ monitoring entry (consumed by the DLQ consumer).</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "DLQ entry received. ErrorType={ErrorType}, Detail={ErrorDetail}, FailedAt={FailedAt}, Retries={RetryCount}, Source={SourceService}")]
    public static partial void DlqEntryReceived(this ILogger logger, string errorType, string errorDetail, DateTimeOffset failedAt, int retryCount, string sourceService);
}
