using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OrderOrchestrator.Application.Commands.ProcessStockValidated;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Domain.Entities;
using OrderOrchestrator.Domain.Enums;
using Shared.Contracts;
using Xunit;

namespace OrderOrchestrator.UnitTests.Application;

public class ProcessStockValidatedCommandHandlerTests
{
    private readonly IOrderSagaRepository _sagaRepository = Substitute.For<IOrderSagaRepository>();
    private readonly IPaymentServiceClient _paymentClient = Substitute.For<IPaymentServiceClient>();
    private readonly ILogger<ProcessStockValidatedCommandHandler> _logger =
        Substitute.For<ILogger<ProcessStockValidatedCommandHandler>>();

    private readonly ProcessStockValidatedCommandHandler _handler;

    public ProcessStockValidatedCommandHandlerTests()
    {
        _handler = new ProcessStockValidatedCommandHandler(_sagaRepository, _paymentClient, _logger);
    }

    [Fact]
    public async Task Handle_WithStockOk_ShouldCreateSagaAndInitiatePayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 99.99m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        var paymentId = Guid.NewGuid();
        _paymentClient.InitiatePaymentAsync(orderId, 99.99m, "USD", Arg.Any<CancellationToken>())
            .Returns(paymentId);

        OrderSaga? capturedSaga = null;
        await _sagaRepository.AddAsync(Arg.Do<OrderSaga>(s => capturedSaga = s), Arg.Any<CancellationToken>());

        // Act
        await _handler.Handle(new ProcessStockValidatedCommand(notification), CancellationToken.None);

        // Assert
        await _sagaRepository.Received(1).AddAsync(Arg.Any<OrderSaga>(), Arg.Any<CancellationToken>());
        await _sagaRepository.Received(2).UpdateAsync(Arg.Any<OrderSaga>(), Arg.Any<CancellationToken>());
        await _paymentClient.Received(1).InitiatePaymentAsync(orderId, 99.99m, "USD", Arg.Any<CancellationToken>());

        capturedSaga!.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Handle_WithNoStock_ShouldCreateSagaInCancelledState()
    {
        // Arrange
        var notification = new StockValidatedNotification(
            OrderId: Guid.NewGuid(),
            StockValidated: false,
            TotalAmount: 99.99m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, false)],
            OccurredAt: DateTimeOffset.UtcNow);

        OrderSaga? capturedFinalSaga = null;
        await _sagaRepository.UpdateAsync(Arg.Do<OrderSaga>(s => capturedFinalSaga = s), Arg.Any<CancellationToken>());

        // Act
        await _handler.Handle(new ProcessStockValidatedCommand(notification), CancellationToken.None);

        // Assert
        await _paymentClient.DidNotReceive()
            .InitiatePaymentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        capturedFinalSaga!.CurrentState.Should().Be(OrderSagaStatus.Cancelled);
    }
}
