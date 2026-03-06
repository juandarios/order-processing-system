using FluentAssertions;
using OrderOrchestrator.Application.StateMachine;
using OrderOrchestrator.Domain.Entities;
using OrderOrchestrator.Domain.Enums;
using Xunit;

namespace OrderOrchestrator.UnitTests.StateMachine;

public class OrderStateMachineTests
{
    private static OrderSaga CreatePendingSaga() => new()
    {
        Id = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        CurrentState = OrderSagaStatus.Pending,
        TotalAmount = 99.99m,
        Currency = "USD",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void FireStockOk_FromPending_ShouldTransitionToStockValidated()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);

        // Act
        machine.FireStockOk();

        // Assert
        saga.CurrentState.Should().Be(OrderSagaStatus.StockValidated);
    }

    [Fact]
    public void FireNoStock_FromPending_ShouldTransitionToCancelled()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);

        // Act
        machine.FireNoStock();

        // Assert
        saga.CurrentState.Should().Be(OrderSagaStatus.Cancelled);
    }

    [Fact]
    public void FirePaymentInitiated_FromStockValidated_ShouldTransitionToPaymentPending()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);
        machine.FireStockOk();

        // Act
        var paymentId = Guid.NewGuid();
        machine.FirePaymentInitiated(paymentId);

        // Assert
        saga.CurrentState.Should().Be(OrderSagaStatus.PaymentPending);
        saga.PaymentId.Should().Be(paymentId);
        saga.TimeoutAt.Should().NotBeNull();
        saga.TimeoutAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void FirePaymentApproved_FromPaymentPending_ShouldTransitionToPaymentConfirmed()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);
        machine.FireStockOk();
        machine.FirePaymentInitiated(Guid.NewGuid());

        // Act
        machine.FirePaymentApproved();

        // Assert
        saga.CurrentState.Should().Be(OrderSagaStatus.PaymentConfirmed);
    }

    [Fact]
    public void FirePaymentFailed_FromPaymentPending_ShouldTransitionToFailed()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);
        machine.FireStockOk();
        machine.FirePaymentInitiated(Guid.NewGuid());

        // Act
        machine.FirePaymentFailed();

        // Assert
        saga.CurrentState.Should().Be(OrderSagaStatus.Failed);
    }

    [Fact]
    public void FireTimeout_FromPaymentPending_ShouldTransitionToFailed()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);
        machine.FireStockOk();
        machine.FirePaymentInitiated(Guid.NewGuid());

        // Act
        machine.FireTimeout();

        // Assert
        saga.CurrentState.Should().Be(OrderSagaStatus.Failed);
    }

    [Fact]
    public void FireStockOk_FromCancelled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var saga = CreatePendingSaga();
        var machine = new OrderStateMachine(saga);
        machine.FireNoStock();

        // Act
        var act = () => machine.FireStockOk();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
