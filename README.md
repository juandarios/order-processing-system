# Order Processing System

A distributed order processing system built with **.NET 8** to demonstrate event-driven architecture, Clean Architecture, CQRS, DDD, and saga orchestration.

---

## Architecture

```
Postman → OrderProducer → Kafka → OrderIntake → StockService (mock)
                                        ↓
                              OrderOrchestrator ← PaymentService
                                        ↓               ↑
                              PaymentService → PaymentGateway (mock)
                                                        ↓ webhook
                              OrderOrchestrator ←── PaymentService
```

| # | Service | Responsibility | ORM |
|---|---|---|---|
| S0 | **OrderProducer** | REST API → publishes orders to Kafka | — |
| S1 | **OrderIntake** | Consumes Kafka, validates stock, notifies orchestrator | EF Core |
| S2 | **PaymentService** | Processes payments via gateway webhook | Dapper |
| S3 | **OrderOrchestrator** | Saga state machine — coordinates the full order lifecycle | Dapper |
| M1 | **StockService** | Configurable mock — simulates stock validation | — |
| M2 | **PaymentGateway** | Configurable mock — simulates async payment gateway | — |

### Order Saga States

```
Pending → StockValidated → PaymentPending → PaymentConfirmed
        ↓                                 ↓
     Cancelled                          Failed (rejected / timeout)
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET 8 |
| Message broker | Apache Kafka (KRaft — no ZooKeeper) |
| ORM (S1) | Entity Framework Core |
| ORM (S2, S3) | Dapper |
| Database | PostgreSQL (one DB per service) |
| State machine | Stateless |
| Architecture | Clean Architecture + CQRS + MediatR |
| Domain | Pragmatic DDD |
| Validation | FluentValidation |
| IDs | UUID v7 (UUIDNext) |
| Local dev | .NET Aspire |
| Infrastructure | Docker Compose |
| Unit tests | xUnit + FluentAssertions + NSubstitute |
| Integration tests | WebApplicationFactory + Testcontainers + WireMock.NET |
| E2E tests | xUnit + HttpClient (against Docker Compose) |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Quick Start

```bash
# Start all services (Kafka, PostgreSQL, all .NET services)
docker compose up -d --build

# The following ports are exposed:
#   5000 → OrderProducer
#   5001 → OrderIntake
#   5010 → StockService (mock)
#   5020 → PaymentService
#   5030 → OrderOrchestrator
#   5040 → PaymentGateway (mock)
#   5432 → PostgreSQL
#   9092 → Kafka
```

### Place an order (example)

```bash
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "018e5e25-0000-7000-8000-000000000001",
    "customerEmail": "john@example.com",
    "shippingAddress": {
      "street": "742 Evergreen Terrace",
      "city": "Springfield",
      "country": "US",
      "zipCode": "12345"
    },
    "items": [{
      "productId": "018e5e26-0000-7000-8000-000000000001",
      "productName": "Laptop Pro 15",
      "quantity": 1,
      "unitPrice": 999.99,
      "currency": "USD"
    }]
  }'
```

### Configure mock behavior

```bash
# Stock available (default)
curl -X POST http://localhost:5010/config/stock -H "Content-Type: application/json" -d '{"response": 200}'

# Stock unavailable
curl -X POST http://localhost:5010/config/stock -H "Content-Type: application/json" -d '{"response": 409}'

# Payment approved after 1s
curl -X POST http://localhost:5040/config/payment-gateway \
  -H "Content-Type: application/json" \
  -d '{"immediateResponse": 202, "webhookDelayMs": 1000, "webhookResult": "approved", "webhookReason": null, "webhookUrl": "http://payment-service:8080/payments/webhook"}'

# Payment rejected after 1s
curl -X POST http://localhost:5040/config/payment-gateway \
  -H "Content-Type: application/json" \
  -d '{"immediateResponse": 202, "webhookDelayMs": 1000, "webhookResult": "rejected", "webhookReason": "card_declined", "webhookUrl": "http://payment-service:8080/payments/webhook"}'
```

---

## Running Tests

### Unit tests (no infrastructure required)

```bash
dotnet test tests/OrderIntake.UnitTests
dotnet test tests/PaymentService.UnitTests
dotnet test tests/OrderOrchestrator.UnitTests
```

### Integration tests (requires Docker — Testcontainers spins up PostgreSQL automatically)

```bash
dotnet test tests/OrderIntake.IntegrationTests
dotnet test tests/PaymentService.IntegrationTests
dotnet test tests/OrderOrchestrator.IntegrationTests
```

### E2E tests (requires all services running via Docker Compose)

```bash
docker compose up -d --build --wait
dotnet test tests/E2E
```

---

## Project Structure

```
order-processing-system/
├── docker-compose.yml
├── order-processing-system.sln
├── ARCHITECTURE.md          ← detailed architecture documentation
├── CLAUDE.md                ← Claude Code context
│
├── src/
│   ├── OrderProducer/                  S0 — minimal structure
│   ├── OrderIntake/                    S1 — Clean Architecture (4 projects)
│   ├── PaymentService/                 S2 — Clean Architecture (4 projects)
│   ├── OrderOrchestrator/              S3 — Clean Architecture (4 projects)
│   ├── Mocks/
│   │   ├── StockService/
│   │   └── PaymentGateway/
│   ├── Shared/                         shared contracts (events, DTOs)
│   └── AppHost/                        .NET Aspire (local dev)
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

For detailed architecture decisions, data models, state machine, and API contracts see [ARCHITECTURE.md](ARCHITECTURE.md).
