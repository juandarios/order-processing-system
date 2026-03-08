using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PaymentService.Application.Commands.InitiatePayment;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="InitiatePaymentCommandHandler"/> focusing on idempotency behaviour.
/// </summary>
public class InitiatePaymentCommandHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGatewayClient _gatewayClient = Substitute.For<IPaymentGatewayClient>();
    private readonly ILogger<InitiatePaymentCommandHandler> _logger =
        Substitute.For<ILogger<InitiatePaymentCommandHandler>>();

    private readonly InitiatePaymentCommandHandler _handler;

    /// <summary>
    /// Initializes a new instance of <see cref="InitiatePaymentCommandHandlerTests"/>
    /// wiring up the handler with substituted dependencies.
    /// </summary>
    public InitiatePaymentCommandHandlerTests()
    {
        _handler = new InitiatePaymentCommandHandler(_paymentRepository, _gatewayClient, _logger);
    }

    /// <summary>
    /// When a payment for the order already exists (duplicate request), the handler must
    /// return the existing payment ID without creating a new record or calling the gateway.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_WithDuplicateOrderId_ReturnsExistingPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var existingPaymentId = Guid.NewGuid();
        var existingPayment = Payment.Create(existingPaymentId, orderId, 99.99m, "USD");

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(existingPayment);

        var command = new InitiatePaymentCommand(orderId, 99.99m, "USD");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — existing ID returned, no new record, gateway not called
        result.Should().Be(existingPaymentId);
        await _paymentRepository.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _gatewayClient.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When no payment exists for the order, the handler creates a new payment record
    /// and calls the gateway.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_WithNewOrderId_CreatesPaymentAndCallsGateway()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // First call (idempotency check) returns null; second call (re-query after insert) also returns null.
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var command = new InitiatePaymentCommand(orderId, 50.00m, "USD");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — new record inserted and gateway called
        result.Should().NotBeEmpty();
        await _paymentRepository.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _gatewayClient.Received(1).ChargeAsync(orderId, 50.00m, "USD", Arg.Any<CancellationToken>());
    }
}
