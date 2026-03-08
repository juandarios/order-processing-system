using Microsoft.Extensions.Logging;

namespace PaymentService.Application.Logging;

/// <summary>
/// Source-generated structured log messages for the Payment Service.
/// </summary>
public static partial class PaymentLogMessages
{
    /// <summary>Logs that a payment was created.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Payment {PaymentId} created for order {OrderId}")]
    public static partial void PaymentCreated(this ILogger logger, Guid paymentId, Guid orderId);

    /// <summary>Logs that a charge was sent to the gateway.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Gateway charge sent for payment {PaymentId}")]
    public static partial void GatewayChargeSent(this ILogger logger, Guid paymentId);

    /// <summary>Logs that a payment was approved.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Payment {PaymentId} approved for order {OrderId}")]
    public static partial void PaymentApproved(this ILogger logger, Guid paymentId, Guid orderId);

    /// <summary>Logs that a payment was rejected.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment {PaymentId} rejected for order {OrderId} with reason {Reason}")]
    public static partial void PaymentRejected(this ILogger logger, Guid paymentId, Guid orderId, string? reason);

    /// <summary>Logs that a payment expired.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment {PaymentId} expired for order {OrderId}")]
    public static partial void PaymentExpired(this ILogger logger, Guid paymentId, Guid orderId);

    /// <summary>Logs that the orchestrator was notified of the payment result.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Orchestrator notified for payment {PaymentId} order {OrderId} status {Status}")]
    public static partial void OrchestratorNotified(this ILogger logger, Guid paymentId, Guid orderId, string status);

    /// <summary>Logs that a duplicate payment request was detected and the existing payment is returned.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate payment request detected for OrderId={OrderId}. Returning existing payment.")]
    public static partial void DuplicatePaymentDetected(this ILogger logger, Guid orderId);

    /// <summary>Logs that a duplicate webhook notification was ignored because the payment is already in a terminal state.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Duplicate webhook ignored for PaymentId={PaymentId}. Payment already in terminal state.")]
    public static partial void DuplicateWebhookIgnored(this ILogger logger, Guid paymentId);

    /// <summary>Logs that the payment was persisted and 202 Accepted was returned to S3.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Payment {PaymentId} persisted for order {OrderId}. Returning 202 Accepted to caller.")]
    public static partial void PaymentPersistedAndAccepted(this ILogger logger, Guid paymentId, Guid orderId);

    /// <summary>Logs that the background gateway call has started.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Background gateway call started for payment {PaymentId} order {OrderId}.")]
    public static partial void GatewayCallStartedInBackground(this ILogger logger, Guid paymentId, Guid orderId);

    /// <summary>Logs that the background gateway call failed after all resilience retries.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Background gateway call failed for payment {PaymentId} order {OrderId}: {Reason}")]
    public static partial void GatewayCallFailed(this ILogger logger, Guid paymentId, Guid orderId, string reason);

    /// <summary>Logs that the background gateway call succeeded (202 Accepted). The gateway will deliver the result via webhook.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Background gateway call succeeded for payment {PaymentId} order {OrderId}. Awaiting webhook callback.")]
    public static partial void GatewayCallSucceeded(this ILogger logger, Guid paymentId, Guid orderId);

    /// <summary>
    /// Logs that the payment gateway was unreachable (connectivity failure — gateway never contacted).
    /// This differs from a timeout, where the gateway was reached but did not respond in time.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Payment gateway unavailable for order {OrderId} payment {PaymentId}. All retry attempts exhausted. Error: {Error}")]
    public static partial void GatewayUnavailable(this ILogger logger, Guid paymentId, Guid orderId, string error);

    /// <summary>
    /// Logs that the payment gateway timed out (gateway was reached but did not respond in time).
    /// This differs from unavailability, where the gateway could not be contacted at all.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Payment gateway timed out for order {OrderId} payment {PaymentId}. All retry attempts exhausted.")]
    public static partial void GatewayTimeout(this ILogger logger, Guid paymentId, Guid orderId);
}
