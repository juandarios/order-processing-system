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
}
