using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PaymentService.Application.Commands.ProcessWebhook;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using Shared.Contracts;
using Xunit;

namespace PaymentService.UnitTests.Application;

public class ProcessWebhookCommandHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IOrchestratorClient _orchestratorClient = Substitute.For<IOrchestratorClient>();
    private readonly ILogger<ProcessWebhookCommandHandler> _logger =
        Substitute.For<ILogger<ProcessWebhookCommandHandler>>();

    private readonly ProcessWebhookCommandHandler _handler;

    public ProcessWebhookCommandHandlerTests()
    {
        _handler = new ProcessWebhookCommandHandler(_paymentRepository, _orchestratorClient, _logger);
    }

    [Fact]
    public async Task Handle_WithApprovedStatus_ShouldApprovePaymentAndNotifyOrchestrator()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var payment = Payment.Create(paymentId, orderId, 99.99m, "USD");

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(payment);

        PaymentProcessedNotification? captured = null;
        await _orchestratorClient.NotifyPaymentProcessedAsync(
            Arg.Do<PaymentProcessedNotification>(n => captured = n),
            Arg.Any<CancellationToken>());

        var command = new ProcessWebhookCommand(orderId, paymentId, "approved", null, 99.99m, "USD");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Approved);
        await _orchestratorClient.Received(1)
            .NotifyPaymentProcessedAsync(Arg.Any<PaymentProcessedNotification>(), Arg.Any<CancellationToken>());
        captured!.Status.Should().Be("approved");
        captured.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Handle_WithRejectedStatus_ShouldRejectPaymentAndNotifyOrchestrator()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var payment = Payment.Create(Guid.NewGuid(), orderId, 99.99m, "USD");

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var command = new ProcessWebhookCommand(
            orderId, Guid.NewGuid(), "rejected", "insufficient_funds", 99.99m, "USD");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Rejected);
        payment.RejectionReason.Should().Be("insufficient_funds");
    }

    [Fact]
    public async Task Handle_WithPaymentNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _paymentRepository.GetByOrderIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var command = new ProcessWebhookCommand(Guid.NewGuid(), Guid.NewGuid(), "approved", null, 99.99m, "USD");

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PaymentService.Domain.Exceptions.NotFoundException>();
    }

    /// <summary>
    /// When the payment is already in a terminal state (e.g. Approved), a duplicate webhook
    /// notification must be ignored gracefully — no update is persisted and no error is thrown.
    /// </summary>
    [Fact]
    public async Task ProcessWebhook_WithDuplicateNotification_IgnoresGracefully()
    {
        // Arrange — payment already approved (terminal state)
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var payment = Payment.Create(paymentId, orderId, 99.99m, "USD");
        payment.Approve("approved");

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var command = new ProcessWebhookCommand(orderId, paymentId, "approved", null, 99.99m, "USD");

        // Act — no exception expected
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert — no update persisted, no error
        await act.Should().NotThrowAsync();
        await _paymentRepository.DidNotReceive().UpdateAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _orchestratorClient.DidNotReceive()
            .NotifyPaymentProcessedAsync(Arg.Any<PaymentProcessedNotification>(), Arg.Any<CancellationToken>());
    }
}
