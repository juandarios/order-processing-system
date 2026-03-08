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

/// <summary>
/// Tests idempotency behaviour of <see cref="ProcessStockValidatedCommandHandler"/>.
/// </summary>
public class ProcessStockValidatedIdempotencyTests
{
    private readonly IOrderSagaRepository _sagaRepository = Substitute.For<IOrderSagaRepository>();
    private readonly IPaymentServiceClient _paymentClient = Substitute.For<IPaymentServiceClient>();
    private readonly ILogger<ProcessStockValidatedCommandHandler> _logger =
        Substitute.For<ILogger<ProcessStockValidatedCommandHandler>>();

    private readonly ProcessStockValidatedCommandHandler _handler;

    public ProcessStockValidatedIdempotencyTests()
    {
        _handler = new ProcessStockValidatedCommandHandler(_sagaRepository, _paymentClient, _logger);
    }

    [Fact]
    public async Task HandleStockValidated_WithDuplicateOrderId_DoesNotCreateDuplicateSaga()
    {
        // Arrange — an existing saga already exists in StockValidated state (already processed once)
        var orderId = Guid.NewGuid();
        var existingSaga = new OrderSaga
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CurrentState = OrderSagaStatus.StockValidated,
            TotalAmount = 99.99m,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        };

        _sagaRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(existingSaga);

        var notification = new StockValidatedNotification(
            OrderId: orderId,
            StockValidated: true,
            TotalAmount: 99.99m,
            Currency: "USD",
            Items: [new StockItemValidationResult(Guid.NewGuid(), 1, true)],
            OccurredAt: DateTimeOffset.UtcNow);

        // Act
        await _handler.Handle(new ProcessStockValidatedCommand(notification), CancellationToken.None);

        // Assert — no new saga created, no payment initiated again
        await _sagaRepository.DidNotReceive().AddAsync(Arg.Any<OrderSaga>(), Arg.Any<CancellationToken>());
        await _paymentClient.DidNotReceive()
            .InitiatePaymentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePaymentProcessed_WithDuplicateNotification_IgnoresGracefully()
    {
        // Arrange — saga is already in a terminal state (PaymentConfirmed)
        var orderId = Guid.NewGuid();
        var finishedSaga = new OrderSaga
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CurrentState = OrderSagaStatus.PaymentConfirmed,
            TotalAmount = 99.99m,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        _sagaRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(finishedSaga);

        var handler = new OrderOrchestrator.Application.Commands.ProcessPaymentProcessed
            .ProcessPaymentProcessedCommandHandler(
                _sagaRepository,
                Substitute.For<ILogger<OrderOrchestrator.Application.Commands.ProcessPaymentProcessed
                    .ProcessPaymentProcessedCommandHandler>>());

        var notification = new PaymentProcessedNotification(
            OrderId: orderId,
            PaymentId: Guid.NewGuid(),
            Status: "approved",
            Reason: null,
            Amount: 99.99m,
            Currency: "USD",
            OccurredAt: DateTimeOffset.UtcNow);

        // Act
        var act = async () => await handler.Handle(
            new OrderOrchestrator.Application.Commands.ProcessPaymentProcessed.ProcessPaymentProcessedCommand(notification),
            CancellationToken.None);

        // Assert — no exception, UpdateAsync not called (saga already terminal)
        await act.Should().NotThrowAsync();
        await _sagaRepository.DidNotReceive().UpdateAsync(Arg.Any<OrderSaga>(), Arg.Any<CancellationToken>());
    }
}
