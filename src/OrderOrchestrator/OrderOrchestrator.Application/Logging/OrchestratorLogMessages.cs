using Microsoft.Extensions.Logging;

namespace OrderOrchestrator.Application.Logging;

/// <summary>
/// Source-generated structured log messages for the Order Orchestrator.
/// </summary>
public static partial class OrchestratorLogMessages
{
    /// <summary>Logs saga creation.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Saga {SagaId} created for order {OrderId}")]
    public static partial void SagaCreated(this ILogger logger, Guid sagaId, Guid orderId);

    /// <summary>Logs a saga state transition.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Saga for order {OrderId} transitioned to {State}")]
    public static partial void SagaTransitioned(this ILogger logger, Guid orderId, string state);

    /// <summary>Logs that a saga was cancelled due to no stock.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Saga for order {OrderId} cancelled (no stock)")]
    public static partial void SagaCancelled(this ILogger logger, Guid orderId);

    /// <summary>Logs payment approved.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Payment approved for order {OrderId}")]
    public static partial void PaymentApproved(this ILogger logger, Guid orderId);

    /// <summary>Logs payment failed or rejected.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment failed for order {OrderId} with status {Status}")]
    public static partial void PaymentFailed(this ILogger logger, Guid orderId, string status);

    /// <summary>Logs timeout processing.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeout detected for order {OrderId}, failing saga")]
    public static partial void SagaTimedOut(this ILogger logger, Guid orderId);

    /// <summary>Logs detection of a duplicate saga for an order that already has a saga in progress.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate saga detected for order {OrderId}. Using existing saga.")]
    public static partial void DuplicateSagaDetected(this ILogger logger, Guid orderId);

    /// <summary>Logs that a duplicate payment processed notification was ignored gracefully.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Duplicate payment notification ignored for order {OrderId}. Saga is already in a terminal or processed state.")]
    public static partial void DuplicateNotificationIgnored(this ILogger logger, Guid orderId);

    /// <summary>Logs that the payment service is unavailable after all retries.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Payment service unavailable for order {OrderId}. Saga remains in StockValidated state. Reason: {Reason}")]
    public static partial void PaymentInitiationFailed(this ILogger logger, Guid orderId, string reason);
}
