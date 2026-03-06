using FluentAssertions;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;
using Xunit;

namespace PaymentService.UnitTests.Domain;

public class PaymentTests
{
    [Fact]
    public void Create_WithValidData_ShouldBePending()
    {
        // Arrange & Act
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.RejectionReason.Should().BeNull();
        payment.GatewayResponse.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenPending_ShouldTransitionToApproved()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD");

        // Act
        payment.Approve("approved");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.GatewayResponse.Should().Be("approved");
    }

    [Fact]
    public void Approve_WhenNotPending_ShouldThrowDomainException()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD");
        payment.Approve("approved");

        // Act
        var act = () => payment.Approve("approved");

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*Pending*");
    }

    [Fact]
    public void Reject_WhenPending_ShouldTransitionToRejected()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD");

        // Act
        payment.Reject("insufficient_funds", "rejected");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Rejected);
        payment.RejectionReason.Should().Be("insufficient_funds");
    }

    [Fact]
    public void Expire_WhenPending_ShouldTransitionToExpired()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD");

        // Act
        payment.Expire();

        // Assert
        payment.Status.Should().Be(PaymentStatus.Expired);
    }
}
