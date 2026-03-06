using FluentAssertions;
using OrderIntake.Domain.Entities;
using OrderIntake.Domain.Enums;
using OrderIntake.Domain.Exceptions;
using OrderIntake.Domain.ValueObjects;
using Xunit;

namespace OrderIntake.UnitTests.Domain;

public class OrderTests
{
    private static Order CreateValidOrder() =>
        Order.Create(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            customerEmail: "test@example.com",
            total: new Money(99.99m, "USD"),
            shippingAddress: new Address("123 Main St", "Springfield", "US", "12345"),
            lines: [new OrderLine(Guid.NewGuid(), "Widget", 2, 49.995m, "USD")]);

    [Fact]
    public void Create_WithValidData_ShouldBePending()
    {
        // Arrange & Act
        var order = CreateValidOrder();

        // Assert
        order.Status.Should().Be(OrderStatus.Pending);
        order.Lines.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithNoLines_ShouldThrowDomainException()
    {
        // Arrange & Act
        var act = () => Order.Create(
            Guid.NewGuid(), Guid.NewGuid(), "test@example.com",
            new Money(0m, "USD"),
            new Address("123 Main St", "City", "US", "12345"),
            []);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*at least one line*");
    }

    [Fact]
    public void MarkStockValidated_WhenPending_ShouldTransitionToStockValidated()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        order.MarkStockValidated();

        // Assert
        order.Status.Should().Be(OrderStatus.StockValidated);
        order.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void MarkStockValidated_WhenNotPending_ShouldThrowDomainException()
    {
        // Arrange
        var order = CreateValidOrder();
        order.Cancel();

        // Act
        var act = () => order.MarkStockValidated();

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*Pending*");
    }

    [Fact]
    public void Cancel_WhenPending_ShouldTransitionToCancelled()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        order.Cancel();

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenNotPending_ShouldThrowDomainException()
    {
        // Arrange
        var order = CreateValidOrder();
        order.MarkStockValidated();

        // Act
        var act = () => order.Cancel();

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*Pending*");
    }

    [Fact]
    public void Money_WithNegativeAmount_ShouldThrowDomainException()
    {
        // Arrange & Act
        var act = () => new Money(-1m, "USD");

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }

    [Fact]
    public void Money_WithEmptyCurrency_ShouldThrowDomainException()
    {
        // Arrange & Act
        var act = () => new Money(10m, "");

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*Currency*");
    }

    [Fact]
    public void OrderLine_WithZeroQuantity_ShouldThrowDomainException()
    {
        // Arrange & Act
        var act = () => new OrderLine(Guid.NewGuid(), "Widget", 0, 10m, "USD");

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*positive*");
    }
}
