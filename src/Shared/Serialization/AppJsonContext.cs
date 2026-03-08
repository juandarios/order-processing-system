using System.Text.Json.Serialization;
using Shared.Contracts;
using Shared.Events;

namespace Shared.Serialization;

/// <summary>
/// Source-generated JSON serialization context for all shared contracts and events.
/// Avoids reflection at runtime, improving performance and enabling AOT compatibility.
/// </summary>
[JsonSerializable(typeof(OrderPlacedEvent))]
[JsonSerializable(typeof(OrderPlacedPayload))]
[JsonSerializable(typeof(ShippingAddressDto))]
[JsonSerializable(typeof(OrderItemDto))]
[JsonSerializable(typeof(StockValidatedNotification))]
[JsonSerializable(typeof(StockItemValidationResult))]
[JsonSerializable(typeof(PaymentProcessedNotification))]
[JsonSerializable(typeof(InitiatePaymentRequest))]
[JsonSerializable(typeof(OrderValidationErrorEvent))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonContext : JsonSerializerContext;
