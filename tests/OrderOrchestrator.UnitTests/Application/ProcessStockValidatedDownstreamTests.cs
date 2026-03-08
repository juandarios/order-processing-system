using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrderOrchestrator.Application.Commands.ProcessStockValidated;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Domain.Entities;
using OrderOrchestrator.Domain.Enums;
using Shared.Contracts;
using Xunit;

namespace OrderOrchestrator.UnitTests.Application;

/// <summary>
/// Tests that S3 does not propagate downstream (S2) failures to the caller.
/// </summary>
public class ProcessStockValidatedDownstreamTests
{
    private readonly IOrderSagaRepository _sagaRepository = Substitute.For<IOrderSagaRepository>();
    private readonly IPaymentServiceClient _paymentClient = Substitute.For<IPaymentServiceClient>();
    private readonly ILogger<ProcessStockValidatedCommandHandler> _logger =
        Substitute.For<ILogger<ProcessStockValidatedCommandHandler>>();

    private readonly ProcessStockValidatedCommandHandler _handler;

    public ProcessStockValidatedDownstreamTests()
    {
        _handler = new ProcessStockValidatedCommandHandler(_sagaRepository, _paymentClient, _logger);
    }

    [Fact]
    public async Task HandleStockValidated_WhenPaymentServiceUnavailable_HandlerCompletesSuccessfully()
    {
        // Arrange — payment service throws (simulating S2 down after Polly retries exhausted)
        var orderId = Guid.NewGuid();
        _paymentClient
            .InitiatePaymentAsync(orderId, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Payment service unavailable"));

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 120.00m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        // Act — handler must not throw even when S2 is unavailable
        var act = async () => await _handler.Handle(
            new ProcessStockValidatedCommand(notification), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleStockValidated_WhenPaymentServiceUnavailable_SagaRemainsInStockValidated()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _paymentClient
            .InitiatePaymentAsync(orderId, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Payment service unavailable"));

        OrderSaga? lastUpdatedSaga = null;
        await _sagaRepository.UpdateAsync(
            Arg.Do<OrderSaga>(s => lastUpdatedSaga = s),
            Arg.Any<CancellationToken>());

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 120.00m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        // Act
        await _handler.Handle(new ProcessStockValidatedCommand(notification), CancellationToken.None);

        // Assert — the last saga update should be StockValidated (payment initiation failed,
        // so we never transitioned to PaymentPending)
        lastUpdatedSaga.Should().NotBeNull();
        lastUpdatedSaga!.CurrentState.Should().Be(OrderSagaStatus.StockValidated);
    }
}
