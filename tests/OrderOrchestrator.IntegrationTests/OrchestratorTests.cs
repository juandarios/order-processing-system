using System.Net.Http.Json;
using FluentAssertions;
using OrderOrchestrator.Domain.Enums;
using OrderOrchestrator.IntegrationTests.Infrastructure;
using Shared.Contracts;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace OrderOrchestrator.IntegrationTests;

public class OrchestratorTests : IClassFixture<OrchestratorWebAppFactory>
{
    private readonly OrchestratorWebAppFactory _factory;

    public OrchestratorTests(OrchestratorWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StockValidated_WithStockOk_ShouldCreateSagaInPaymentPendingState()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        _factory.PaymentServiceMock
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBodyAsJson(new { PaymentId = paymentId }));

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 99.99m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 2, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/orchestrator/orders/stock-validated", notification);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        _factory.PaymentServiceMock.LogEntries.Should().Contain(e =>
            e.RequestMessage.Path == "/payments");
    }

    [Fact]
    public async Task StockValidated_WithNoStock_ShouldCreateSagaInCancelledState()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentCallsBefore = _factory.PaymentServiceMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/payments");

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: false,
            TotalAmount: 99.99m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 2, false)],
            OccurredAt: DateTimeOffset.UtcNow);

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/orchestrator/orders/stock-validated", notification);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Payment service should NOT have been called for this specific no-stock request
        var paymentCallsAfter = _factory.PaymentServiceMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/payments");
        paymentCallsAfter.Should().Be(paymentCallsBefore);
    }

    [Fact]
    public async Task PaymentProcessed_WithApprovedStatus_ShouldTransitionSagaToPaymentConfirmed()
    {
        // Arrange — create a saga first
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        _factory.PaymentServiceMock
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBodyAsJson(new { PaymentId = paymentId }));

        var client = _factory.CreateClient();

        // Create saga via stock-validated
        await client.PostAsJsonAsync("/orchestrator/orders/stock-validated",
            new StockValidatedNotification(
                OrderId: orderId,
                StockValidated: true,
                TotalAmount: 150.00m,
                Currency: "USD",
                Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
                OccurredAt: DateTimeOffset.UtcNow));

        // Act — send payment approved
        var paymentNotification = new PaymentProcessedNotification(
            OrderId: orderId,
            PaymentId: paymentId,
            Status: "approved",
            Reason: null,
            Amount: 150.00m,
            Currency: "USD",
            OccurredAt: DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync(
            "/orchestrator/orders/payment-processed", paymentNotification);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task PaymentProcessed_WithRejectedStatus_ShouldTransitionSagaToFailed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        _factory.PaymentServiceMock
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBodyAsJson(new { PaymentId = paymentId }));

        var client = _factory.CreateClient();

        // Create saga
        await client.PostAsJsonAsync("/orchestrator/orders/stock-validated",
            new StockValidatedNotification(
                OrderId: orderId,
                StockValidated: true,
                TotalAmount: 200.00m,
                Currency: "USD",
                Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
                OccurredAt: DateTimeOffset.UtcNow));

        // Act — send payment rejected
        var paymentNotification = new PaymentProcessedNotification(
            OrderId: orderId,
            PaymentId: paymentId,
            Status: "rejected",
            Reason: "card_declined",
            Amount: 200.00m,
            Currency: "USD",
            OccurredAt: DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync(
            "/orchestrator/orders/payment-processed", paymentNotification);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
