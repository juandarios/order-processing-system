# Order Processing System — Claude Code Context

This file provides Claude Code with all the context needed to work on this project.
Read it entirely before writing any code or creating any file.

---

## Project Overview

A distributed order processing system built with .NET 8 to learn and demonstrate:
- Kafka event consumption and production
- Clean Architecture + CQRS + DDD (pragmatic)
- State machine orchestration with Stateless
- Synchronous and asynchronous service communication
- Webhooks
- PostgreSQL with EF Core and Dapper
- Unit, integration and E2E testing

---

## Repository Structure (mono-repo)

```
order-processing-system/
├── docker-compose.yml
├── order-processing-system.sln
├── CLAUDE.md
├── ARCHITECTURE.md
│
├── src/
│   ├── OrderProducer/                 ← minimal structure (no domain)
│   ├── OrderIntake/                   ← Clean Architecture
│   ├── PaymentService/                ← Clean Architecture
│   ├── OrderOrchestrator/             ← Clean Architecture
│   ├── Mocks/
│   │   ├── StockService/              ← minimal structure
│   │   └── PaymentGateway/            ← minimal structure
│   ├── Shared/                        ← shared contracts (events, DTOs)
│   └── AppHost/                       ← .NET Aspire orchestration
│
└── tests/
    ├── OrderIntake.UnitTests/
    ├── OrderIntake.IntegrationTests/
    ├── PaymentService.UnitTests/
    ├── PaymentService.IntegrationTests/
    ├── OrderOrchestrator.UnitTests/
    ├── OrderOrchestrator.IntegrationTests/
    └── E2E/
```

---

## Services

| # | Name | Responsibility | ORM | Incoming | Outgoing |
|---|---|---|---|---|---|
| **S0** | Order Producer | REST API + Kafka Producer | - | HTTP (Postman) | Kafka → order-placed |
| **S1** | Order Intake | Kafka Consumer. Saves order, validates stock, notifies orchestrator. Routes failures to DLQ. Publishes validation errors to order-validation-errors. | EF Core | Kafka ← order-placed | HTTP → Stock Mock, HTTP → S3, Kafka → order-placed-dlq, Kafka → order-validation-errors |
| **S2** | Payment Service | REST API. Receives payment request, calls gateway, receives webhook | Dapper | HTTP (S3), HTTP /webhook (Gateway Mock) | HTTP → Gateway Mock, HTTP → S3 |
| **S3** | Order Orchestrator | REST API + State Machine. Orchestrates full order lifecycle | Dapper | HTTP (S1), HTTP (S2) | HTTP → S2 |
| **Mock 1** | Stock Service | Simulates stock validation | - | HTTP (S1) | HTTP response → S1 |
| **Mock 2** | Payment Gateway | Simulates async payment gateway with webhook | - | HTTP (S2) | HTTP 202 → S2, HTTP /webhook → S2 |

---

## Internal Structure — Domain-rich Services (S1, S2, S3)

Clean Architecture with the following dependency rule:
> **Domain defines contracts (interfaces). Infrastructure implements them. Application uses them. No one depends outward.**

```
ServiceName/
├── ServiceName.Api/             ← HTTP entry point, DI configuration
├── ServiceName.Application/     ← use cases, commands, queries, interfaces (ports)
├── ServiceName.Domain/          ← entities, value objects, domain events, enums
└── ServiceName.Infrastructure/  ← repositories, Kafka, HTTP clients (adapters)
```

**Dependency direction:**
- `Domain` → no references to anyone
- `Application` → references `Domain` only
- `Infrastructure` → references `Application` and `Domain`
- `Api` → references `Infrastructure` and `Application`

---

## Internal Structure — Simple Services (S0, Mocks)

```
ServiceName/
├── Program.cs
├── appsettings.json
├── Dockerfile
└── Controllers/
    ├── MainController.cs
    └── ConfigController.cs      ← mocks only: POST /config/{mock}
```

---

## Shared Project

Contains contracts shared across services. No business logic.

```
Shared/
├── Events/
│   └── OrderPlacedEvent.cs      ← Kafka event envelope + payload
└── Contracts/
    ├── StockValidatedNotification.cs   ← S1 → S3
    ├── PaymentProcessedNotification.cs ← S2 → S3
    └── InitiatePaymentRequest.cs       ← S3 → S2
```

---

## Domain Model (S1 — Order Intake)

### Entities
- `Order` — aggregate root. Contains business rules (e.g. can only confirm a pending order).

### Value Objects
- `Money` — amount + currency. Immutable.
- `Address` — street, city, country, zip code. Immutable.
- `OrderLine` — productId, productName, quantity, unitPrice. Immutable.

### Domain Events
- `OrderValidatedEvent` — raised when order passes stock validation.

### Enums
- `OrderStatus` — Pending, StockValidated, Cancelled, PaymentPending, PaymentConfirmed, Failed.

---

## State Machine (S3 — Order Orchestrator)

Library: **Stateless**

| State | Description | Ball is at |
|---|---|---|
| `Pending` | Order received, waiting for stock validation | S1 |
| `StockValidated` | Stock confirmed, waiting for S3 to initiate payment | S3 |
| `Cancelled` | Insufficient stock | Terminal |
| `PaymentPending` | Payment initiated, waiting for S2 response | S2 |
| `PaymentConfirmed` | Payment approved | Terminal |
| `Failed` | Payment rejected, expired, or orchestrator timeout | Terminal |

| From | Trigger | To |
|---|---|---|
| `Pending` | Stock ok | `StockValidated` |
| `Pending` | No stock | `Cancelled` |
| `StockValidated` | S3 calls S2 successfully | `PaymentPending` |
| `PaymentPending` | Payment approved | `PaymentConfirmed` |
| `PaymentPending` | Payment rejected or expired | `Failed` |
| `PaymentPending` | Orchestrator timeout (5 min) | `Failed` |

Timeout detection: background polling job queries every 30 seconds:
```sql
SELECT * FROM order_sagas
WHERE current_state = 'PaymentPending'
AND timeout_at < NOW()
```

---

## Database Schema

### S1 — order-intake-db (EF Core)

```sql
orders (id UUID PK, status VARCHAR(50), customer_id UUID, customer_email VARCHAR(255),
        total_amount DECIMAL(18,2), currency VARCHAR(3),
        shipping_street VARCHAR(255), shipping_city VARCHAR(100),
        shipping_country VARCHAR(2), shipping_zip_code VARCHAR(20),
        created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)

order_lines (id UUID PK, order_id UUID FK, product_id UUID,
             product_name VARCHAR(255), quantity INT,
             unit_price DECIMAL(18,2), currency VARCHAR(3))
```

### S2 — payment-db (Dapper)

```sql
payments (id UUID PK, order_id UUID UNIQUE, status VARCHAR(50),
          amount DECIMAL(18,2), currency VARCHAR(3),
          rejection_reason VARCHAR(100) NULL, gateway_response VARCHAR(50) NULL,
          created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
```

### S3 — orchestrator-db (Dapper)

```sql
order_sagas (id UUID PK, order_id UUID UNIQUE, current_state VARCHAR(50),
             total_amount DECIMAL(18,2), currency VARCHAR(3),
             payment_id UUID NULL, payment_initiated_at TIMESTAMPTZ NULL,
             timeout_at TIMESTAMPTZ NULL,
             created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
```

---

## Kafka

- **Client:** Confluent.Kafka with custom abstraction layer (do not use MassTransit for Kafka)
- **Topics:** `order-placed`, `order-placed-dlq` (dead letter queue), `order-validation-errors` (validation topic)
- **Event format:** JSON with envelope (eventId, eventType, occurredAt, version, payload)
- **IDs:** UUID v7 for all identifiers (use `UUIDNext` package in .NET 8)
- **Consensus:** KRaft mode — Kafka's built-in consensus mechanism, no ZooKeeper container needed. Two listeners: one for the internal Docker network, one for the host machine.

---

## HTTP Contracts

### Stock Validation (S1 → Mock 1)
```
GET /stock/availability?productId={id}&quantity={qty}
200 OK          → stock available
409 Conflict    → out of stock
400 Bad Request → invalid parameters
500             → internal error
```

### Stock Validated Notification (S1 → S3)
```
POST /orchestrator/orders/stock-validated
{ orderId, stockValidated, totalAmount, currency, items[], occurredAt }
```

### Initiate Payment (S3 → S2)
```
POST /payments
{ orderId, amount, currency }
```

### Payment Gateway (S2 → Mock 2)
```
POST /charge
→ 202 Accepted (async)
→ calls back: POST /payments/webhook { orderId, paymentId, status, reason, amount, currency }
```

### Payment Processed Notification (S2 → S3)
```
POST /orchestrator/orders/payment-processed
{ orderId, paymentId, status, reason, amount, currency, occurredAt }
```

---

## Mock Configuration

Both mocks expose a `POST /config` endpoint that replaces the in-memory configuration.
Each test gets its own mock instance (Testcontainers for integration, sequential for E2E).

**Stock Mock:**
```json
POST /config/stock
{ "response": 200 }
```
`response`: `200`, `409`, `400` or `500`.

**Payment Gateway Mock:**
```json
POST /config/payment-gateway
{ "immediateResponse": 202, "webhookDelayMs": 3000, "webhookResult": "approved", "webhookReason": null }
```
`webhookResult`: `approved`, `rejected` or `expired`.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | .NET 8 LTS |
| Kafka client | Confluent.Kafka (with custom abstraction) |
| State machine | Stateless |
| ORM (S1) | Entity Framework Core |
| ORM (S2, S3) | Dapper |
| Database | PostgreSQL |
| Architecture | Clean Architecture + CQRS |
| Domain | Pragmatic DDD |
| Mediator | Mediator (MIT community fork by martinothamar — do NOT use MediatR) |
| Validation | FluentValidation (Mediator pipeline behavior) |
| Error handling | Global middleware + ProblemDetails (RFC 7807) |
| Logging | .NET native logger + OpenTelemetry (OTLP export) |
| Observability | OpenTelemetry.Extensions.Hosting + Instrumentation.AspNetCore + Instrumentation.Http + Exporter.OpenTelemetryProtocol |
| IDs | UUID v7 via UUIDNext package |
| Local orchestration | .NET Aspire (AppHost) |
| Message broker | Apache Kafka in **KRaft mode** (no ZooKeeper) |
| Infrastructure | Docker Compose (for E2E tests and CI) |
| Unit tests | xUnit + FluentAssertions + NSubstitute |
| Integration tests | xUnit + WebApplicationFactory + Testcontainers |
| E2E tests | xUnit + HttpClient |
| HTTP mocks (tests) | WireMock.NET |

---

## .NET 8 Optimizations

Apply these patterns consistently across the codebase.

### Logging Source Generators
Always use source-generated log methods instead of direct `ILogger` calls in production code.

```csharp
public static partial class OrderLogMessages
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Order {OrderId} validated with status {Status}")]
    public static partial void OrderValidated(
        this ILogger logger, Guid orderId, string status);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Payment {PaymentId} failed with reason {Reason}")]
    public static partial void PaymentFailed(
        this ILogger logger, Guid paymentId, string reason);
}
```

### JSON Source Generators
Always use source-generated serialization for Kafka messages and HTTP contracts.

```csharp
[JsonSerializable(typeof(OrderPlacedEvent))]
[JsonSerializable(typeof(StockValidatedNotification))]
[JsonSerializable(typeof(PaymentProcessedNotification))]
[JsonSerializable(typeof(InitiatePaymentRequest))]
public partial class AppJsonContext : JsonSerializerContext { }
```

### Primary Constructors
Use primary constructors for handlers, services, and repositories.

```csharp
public class ProcessOrderCommandHandler(
    IOrderRepository repository,
    IStockServiceClient stockClient,
    ILogger<ProcessOrderCommandHandler> logger)
    : IRequestHandler<ProcessOrderCommand> { }
```

### Records
Use records for value objects, commands, queries, and DTOs.

```csharp
public record Money(decimal Amount, string Currency);
public record Address(string Street, string City, string Country, string ZipCode);
public record ProcessOrderCommand(Guid OrderId, Money Total) : IRequest;
public record CreateOrderResponse(Guid OrderId, string Status);
```

### Minimal APIs
Use Minimal APIs for S0 (Order Producer) and all Mocks. Use Controllers for S1, S2, S3.

```csharp
// S0 and Mocks
app.MapPost("/orders", async (CreateOrderRequest request, IMediator mediator) =>
{
    var result = await mediator.Send(new ProcessOrderCommand(request));
    return Results.Created($"/orders/{result.OrderId}", result);
});
```

### IExceptionHandler
Use the official .NET 8 `IExceptionHandler` for global error handling in S1, S2, S3.

```csharp
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (400, "Domain rule violation"),
            NotFoundException => (404, "Resource not found"),
            _ => (500, "An unexpected error occurred")
        };
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title }, ct);
        return true;
    }
}
```

### Keyed Services
Use keyed services for `IEventConsumer` to allow swapping implementations by environment.

```csharp
builder.Services.AddKeyedScoped<IEventConsumer, KafkaConsumer>("kafka");
builder.Services.AddKeyedScoped<IEventConsumer, InMemoryConsumer>("inmemory");
```

---

## Coding Standards

### Language
- **All code, comments, and documentation must be in English.**
- All XML documentation comments on public types and members (entities, value objects, interfaces, commands, queries, handlers, controllers).
- Use `<summary>`, `<param>`, `<returns>`, and `<exception>` tags where applicable.

### Style
- `var` for obvious types, explicit types when the type is not immediately clear from context.
- Always `async/await` throughout the entire call chain.
- Interfaces prefixed with `I` (e.g. `IOrderRepository`).
- PascalCase for classes, methods, properties. camelCase for local variables and parameters.
- Records for value objects, commands, queries and DTOs.

### XML Documentation Example
```csharp
/// <summary>
/// Represents a customer order in the system.
/// </summary>
public class Order
{
    /// <summary>
    /// Confirms the order, transitioning it from Pending to StockValidated status.
    /// </summary>
    /// <exception cref="DomainException">Thrown when the order is not in Pending status.</exception>
    public void Confirm() { ... }
}
```

### Error Handling
- Global exception handling middleware returns `ProblemDetails` (RFC 7807).
- Custom domain exceptions:
  - `DomainException` → 400 Bad Request
  - `NotFoundException` → 404 Not Found
  - Unhandled exceptions → 500 Internal Server Error
- Never expose internal exception details in production responses.
- Never capture scoped services or request CancellationTokens inside `Task.Run` or fire-and-forget operations. Always create an independent scope via `IServiceScopeFactory.CreateAsyncScope()` and resolve services from that scope. Only capture primitive values (Guid, string, decimal, int) from the outer scope.
- Always use `CancellationToken.None` in background tasks that must complete independently of the originating HTTP request lifecycle.

### Validation
- FluentValidation for all request DTOs.
- Validators registered as MediatR pipeline behaviors (validate before handler executes).
- Return 400 with ProblemDetails containing validation errors.

### Logging
- Use .NET native `ILogger<T>`.
- Log at appropriate levels: `Information` for business events, `Warning` for recoverable issues, `Error` for failures.
- Always include relevant IDs in log messages (orderId, paymentId, etc.).

```csharp
_logger.LogInformation("Order {OrderId} stock validated successfully", orderId);
_logger.LogError("Payment {PaymentId} failed with reason {Reason}", paymentId, reason);
```

### Swagger / OpenAPI
- All APIs must have Swagger enabled.
- All controllers and endpoints must have XML documentation comments.
- Use `[ProducesResponseType]` attributes on all endpoints.

```csharp
/// <summary>
/// Publishes a new order to the Kafka topic.
/// </summary>
/// <param name="request">The order details.</param>
/// <returns>The created order ID.</returns>
[HttpPost]
[ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateOrder(CreateOrderRequest request) { ... }
```

### Configuration
- Non-sensitive config in `appsettings.json` (timeouts, topic names, service URLs).
- Sensitive config (passwords, connection strings) via environment variables.
- Always use the Options pattern for typed configuration.

```csharp
public class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string OrderPlacedTopic { get; set; } = string.Empty;
}
```

---

## Testing Conventions

### Naming
```
MethodName_Scenario_ExpectedResult
ProcessOrder_WithValidStock_ReturnsSuccess
ProcessOrder_WithInsufficientStock_ReturnsCancelled
```

### Structure (AAA)
```csharp
// Arrange
var command = new ProcessOrderCommand(...);

// Act
var result = await _handler.Handle(command, CancellationToken.None);

// Assert
result.Should().NotBeNull();
result.Status.Should().Be(OrderStatus.StockValidated);
```

### Unit Tests
- Test domain logic in isolation (no infrastructure).
- Mock all external dependencies with NSubstitute.

### Integration Tests
- Use Testcontainers to spin up real PostgreSQL and Kafka instances.
- Use WebApplicationFactory to host the API in memory.
- Use WireMock.NET to mock external HTTP services.
- Each test class gets its own container instance.

### E2E Tests
- Run against Docker Compose (all services up).
- Call services via HttpClient.
- Configure mocks via their `/config` endpoints before each test.
- Tests run sequentially to avoid configuration conflicts.

---

## Git Conventions

### Conventional Commits
```
feat: add order validation handler
fix: correct timeout calculation in orchestrator
chore: add docker compose for kafka
docs: update architecture decision for payment flow
test: add integration tests for payment service
refactor: extract money value object
```

### Branch naming
```
feat/order-intake-kafka-consumer
fix/payment-timeout-calculation
chore/docker-compose-setup
```

---

## .NET Aspire

The `AppHost` project orchestrates all services for local development.

- Use Aspire dashboard for logs, traces and metrics during development.
- Docker Compose is used for E2E tests and CI (not Aspire).
- Both coexist: Aspire for dev, Docker Compose for automated testing.

---

## Important Rules

1. **Never place business logic in controllers.** Controllers only receive requests, send to Mediator, and return responses.
1a. **Do not use MediatR.** Use Mediator (MIT community fork by martinothamar, packages: `Mediator.Abstractions` + `Mediator.SourceGenerator`) instead. MediatR requires a paid commercial license since v12.4.0.
2. **Never reference Infrastructure from Domain.** Domain must have zero external dependencies.
3. **Never reference Infrastructure from Application.** Application only uses interfaces defined in its own `Interfaces/` folder.
4. **Always use UUID v7** for all new identifiers.
5. **Always add XML documentation** to public types and members.
6. **Always add Swagger attributes** to controller actions.
7. **Never hardcode connection strings or passwords.** Use environment variables.
8. **Always write tests** for new features: unit tests for domain logic, integration tests for infrastructure, E2E for full flows.
9. **OpenTelemetry is mandatory in all services.** Add `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, and `OpenTelemetry.Exporter.OpenTelemetryProtocol` to each .Api/.csproj and Mock .csproj. Configure via `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable (automatically read by the SDK). Service names must be lowercase hyphenated (e.g. `"order-intake"`). When producing Kafka messages, always inject the current trace context into message headers using `Propagators.DefaultTextMapPropagator.Inject`. When consuming Kafka messages, always extract the trace context from message headers using `Propagators.DefaultTextMapPropagator.Extract` and create a child Activity before processing. This enables end-to-end distributed trace visibility from S0 to PaymentConfirmed in the Aspire dashboard.
10. **S3 notification endpoints must always return 200 OK to the caller once the notification is persisted.** Never propagate downstream failures (e.g. S2 being unavailable) back to S1 or S2 callers. Downstream calls after saga persistence must be wrapped in try/catch. Log failures with `PaymentInitiationFailed` (LogError). Add `// TODO: Implement Outbox Pattern to handle prolonged S2 unavailability` at the catch site.
11. **S3 is responsible for its own retry logic when calling downstream services.** S1's responsibility ends when S3 confirms receipt of the stock-validated notification. What S3 does internally after that (e.g. calling S2) is S3's problem alone.
12. **All orchestrator handlers must be idempotent.** Always check for an existing saga before creating a new one. If a saga already exists for a given `orderId`, reuse it rather than inserting a duplicate. Use `INSERT ... ON CONFLICT (order_id) DO NOTHING` as a database-level second line of defence. Duplicate `PaymentProcessed` notifications must be ignored gracefully when the saga is not in `PaymentPending` state.
13. **Always include the Kerberos library in Dockerfiles that use Confluent.Kafka.** For Debian/Ubuntu-based images (`mcr.microsoft.com/dotnet/aspnet:8.0`), add `RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*` in the final stage. For Alpine-based images, use `RUN apk add --no-cache krb5-libs`. Omitting this causes a fatal `libgssapi_krb5.so.2` load error at container startup. Affected services: S1 (Order Intake) and S3 (Order Orchestrator).
14. **Every endpoint that creates or updates a record must be idempotent.** Always check for existing records before inserting (application-level first line of defence). Use `ON CONFLICT DO NOTHING` as a database-level second line of defence against race conditions. Never return 4xx or 5xx for duplicate requests from internal services — return 200 OK (or 202 Accepted) with the existing state instead. For Kafka consumers, detect duplicates in the command handler and throw a typed exception (e.g. `DuplicateOrderException`) that the consumer catches and routes to the DLQ with the appropriate error type before committing the offset.
15. **Services must separate persistence from downstream calls.** Always persist first, respond to the caller, then handle downstream communication asynchronously. Never block the caller response on downstream service availability. Use a background task (fire-and-forget via `Task.Run` with `CancellationToken.None`) with a dedicated DI scope (`IServiceScopeFactory.CreateAsyncScope()`) for downstream calls after persistence. Wrap all background failures in try/catch so they never propagate to the caller. Add `// TODO: Replace fire-and-forget with Outbox Pattern to guarantee delivery` at the fire-and-forget site.
16. **Never hardcode inter-service URLs in application code.** Always configure them via environment variables in docker-compose.yml and read them using the Options pattern (`IOptions<T>`). Inter-service URLs belong in `appsettings.json` as defaults (for local development) and are overridden in production via `ServiceName__PropertyName` environment variables in docker-compose.yml.
17. **Always distinguish between service unavailability and timeout when handling downstream failures.** When a downstream call fails, inspect the exception type to determine the root cause: `HttpRequestException` or `SocketException` means the gateway was unreachable (`gateway_unavailable`); `TaskCanceledException` or Polly `TimeoutRejectedException` means the gateway was reachable but did not respond in time (`gateway_timeout`). Use specific `status` and `reason` fields to communicate the exact failure cause to upstream services so they can react appropriately.
