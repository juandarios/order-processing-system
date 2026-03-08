using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PaymentService.Application.Commands.InitiatePayment;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Exceptions;
using Xunit;

namespace PaymentService.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="InitiatePaymentCommandHandler"/> focusing on idempotency behaviour
/// and the fire-and-forget gateway call introduced in the Phase 1 / Phase 2 refactor.
/// </summary>
public class InitiatePaymentCommandHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGatewayClient _gatewayClient = Substitute.For<IPaymentGatewayClient>();
    private readonly IOrchestratorClient _orchestratorClient = Substitute.For<IOrchestratorClient>();
    private readonly ILogger<InitiatePaymentCommandHandler> _logger =
        Substitute.For<ILogger<InitiatePaymentCommandHandler>>();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InitiatePaymentCommandHandler _handler;

    /// <summary>
    /// Initializes a new instance of <see cref="InitiatePaymentCommandHandlerTests"/>
    /// wiring up the handler with substituted dependencies.
    /// The <see cref="IServiceScopeFactory"/> is configured to return the same substitutes
    /// so that background-scope resolutions hit the same mocks.
    /// </summary>
    public InitiatePaymentCommandHandlerTests()
    {
        // Build a scope factory that resolves the same substitutes used in Phase 2.
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IPaymentGatewayClient)).Returns(_gatewayClient);
        serviceProvider.GetService(typeof(IPaymentRepository)).Returns(_paymentRepository);
        serviceProvider.GetService(typeof(IOrchestratorClient)).Returns(_orchestratorClient);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var asyncScope = Substitute.For<IAsyncDisposable>();

        // IServiceScopeFactory.CreateAsyncScope() returns an AsyncServiceScope (a struct),
        // so we cannot mock it directly with NSubstitute. Instead we inject a real
        // ServiceCollection that resolves our substitutes by instance.
        var services = new ServiceCollection();
        services.AddSingleton(_paymentRepository);
        services.AddSingleton(_gatewayClient);
        services.AddSingleton(_orchestratorClient);

        var realProvider = services.BuildServiceProvider();
        _scopeFactory = realProvider.GetRequiredService<IServiceScopeFactory>();

        // gatewayClient is no longer injected directly into the handler — it is resolved from
        // the background DI scope (via _scopeFactory) to avoid capturing scoped services.
        _handler = new InitiatePaymentCommandHandler(
            _paymentRepository, _scopeFactory, _logger);
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
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When no payment exists for the order, the handler creates a new payment record
    /// and fires off the gateway call in background. The payment ID is returned immediately.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_WithNewOrderId_CreatesPaymentAndReturnsId()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Both the idempotency check and the re-query after insert return null.
        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var command = new InitiatePaymentCommand(orderId, 50.00m, "USD");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — new record inserted and a payment ID returned
        result.Should().NotBeEmpty();
        await _paymentRepository.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());

        // Allow background Task.Run to complete before asserting gateway call
        await Task.Delay(100);
        await _gatewayClient.Received(1).ChargeAsync(
            Arg.Any<Guid>(), orderId, 50.00m, "USD", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the gateway is unavailable, the handler must still return a payment ID
    /// (202 Accepted to S3) — the exception must NOT propagate from <see cref="InitiatePaymentCommandHandler.Handle"/>.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_WhenGatewayUnavailable_Returns202AndPersistsPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        _gatewayClient
            .ChargeAsync(Arg.Any<Guid>(), orderId, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentGatewayUnavailableException("Gateway down"));

        var command = new InitiatePaymentCommand(orderId, 75.00m, "EUR");

        // Act — must NOT throw; Phase 1 already returned before Phase 2 runs
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert Phase 1: payment persisted and ID returned
        result.Should().NotBeEmpty();
        await _paymentRepository.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Gateway unavailability must not propagate an exception from the Handle method.
    /// S3 receives 202 Accepted regardless of downstream gateway availability.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_WhenGatewayUnavailable_DoesNotPropagateErrorToS3()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        _gatewayClient
            .ChargeAsync(Arg.Any<Guid>(), orderId, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentGatewayUnavailableException("Circuit open"));

        var command = new InitiatePaymentCommand(orderId, 100.00m, "USD");

        // Act + Assert: Handle must complete without throwing
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// When the gateway is unreachable (HttpRequestException inner exception), the handler must
    /// notify the orchestrator with <c>status = "failed"</c> and <c>reason = "gateway_unavailable"</c>.
    /// </summary>
    [Fact]
    public async Task ChargeGateway_WhenGatewayUnreachable_NotifiesOrchestratorWithFailedAndGatewayUnavailable()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var innerException = new System.Net.Http.HttpRequestException("Connection refused");
        var gatewayException = new PaymentGatewayUnavailableException("Gateway unreachable", innerException);

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        _gatewayClient
            .ChargeAsync(Arg.Any<Guid>(), orderId, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(gatewayException);

        // Return a Pending payment when GetByIdAsync is called inside the catch block.
        _paymentRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => Payment.Create(ci.ArgAt<Guid>(0), orderId, 80.00m, "USD"));

        var command = new InitiatePaymentCommand(orderId, 80.00m, "USD");

        // Act — must not throw
        await _handler.Handle(command, CancellationToken.None);

        // Allow the background Task.Run to complete
        await Task.Delay(300);

        // Assert — orchestrator notified with failed / gateway_unavailable
        await _orchestratorClient.Received(1).NotifyPaymentProcessedAsync(
            Arg.Is<Shared.Contracts.PaymentProcessedNotification>(n =>
                n.OrderId == orderId &&
                n.Status == "failed" &&
                n.Reason == "gateway_unavailable"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the gateway call times out (TaskCanceledException inner exception), the handler must
    /// notify the orchestrator with <c>status = "expired"</c> and <c>reason = "gateway_timeout"</c>.
    /// </summary>
    [Fact]
    public async Task ChargeGateway_WhenGatewayTimesOut_NotifiesOrchestratorWithExpiredAndGatewayTimeout()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var innerException = new TaskCanceledException("The request was canceled due to a timeout.");
        var gatewayException = new PaymentGatewayUnavailableException("Gateway timed out", innerException);

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        _gatewayClient
            .ChargeAsync(Arg.Any<Guid>(), orderId, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(gatewayException);

        // Return a Pending payment when GetByIdAsync is called inside the catch block.
        _paymentRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => Payment.Create(ci.ArgAt<Guid>(0), orderId, 90.00m, "USD"));

        var command = new InitiatePaymentCommand(orderId, 90.00m, "USD");

        // Act — must not throw
        await _handler.Handle(command, CancellationToken.None);

        // Allow the background Task.Run to complete
        await Task.Delay(300);

        // Assert — orchestrator notified with expired / gateway_timeout
        await _orchestratorClient.Received(1).NotifyPaymentProcessedAsync(
            Arg.Is<Shared.Contracts.PaymentProcessedNotification>(n =>
                n.OrderId == orderId &&
                n.Status == "expired" &&
                n.Reason == "gateway_timeout"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cancelling the request CancellationToken after Handle returns must not prevent the
    /// background task from completing. The background gateway call uses CancellationToken.None
    /// and is independent of the HTTP request lifecycle.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_WhenRequestCancelled_BackgroundTaskContinues()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _paymentRepository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var command = new InitiatePaymentCommand(orderId, 30.00m, "USD");
        using var cts = new CancellationTokenSource();

        // Act — call Handle then immediately cancel the request token
        var result = await _handler.Handle(command, cts.Token);
        cts.Cancel();

        // Assert Phase 1 completed successfully: payment persisted and ID returned
        result.Should().NotBeEmpty();
        await _paymentRepository.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());

        // Allow background task to run despite the request token being cancelled
        await Task.Delay(200);

        // Assert Phase 2 still ran: gateway was called using CancellationToken.None
        await _gatewayClient.Received(1).ChargeAsync(
            Arg.Any<Guid>(), orderId, 30.00m, "USD", CancellationToken.None);
    }

    /// <summary>
    /// When two sequential requests carry different orderId and amount values, each background
    /// gateway call must use exactly the data from its own request — no data mixing between requests.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_MultipleSequentialRequests_EachUsesCorrectData()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();

        _paymentRepository.GetByOrderIdAsync(orderId1, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);
        _paymentRepository.GetByOrderIdAsync(orderId2, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var command1 = new InitiatePaymentCommand(orderId1, 111.11m, "USD");
        var command2 = new InitiatePaymentCommand(orderId2, 222.22m, "EUR");

        // Act — two sequential requests
        var result1 = await _handler.Handle(command1, CancellationToken.None);
        var result2 = await _handler.Handle(command2, CancellationToken.None);

        // Allow both background tasks to complete
        await Task.Delay(200);

        // Assert — each gateway call received the correct orderId and amount
        await _gatewayClient.Received(1).ChargeAsync(
            Arg.Any<Guid>(), orderId1, 111.11m, "USD", Arg.Any<CancellationToken>());
        await _gatewayClient.Received(1).ChargeAsync(
            Arg.Any<Guid>(), orderId2, 222.22m, "EUR", Arg.Any<CancellationToken>());

        // Each handler returned a distinct payment ID
        result1.Should().NotBe(result2);
    }
}
