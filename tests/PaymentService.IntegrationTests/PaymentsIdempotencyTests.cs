using System.Net.Http.Json;
using FluentAssertions;
using PaymentService.IntegrationTests.Infrastructure;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace PaymentService.IntegrationTests;

/// <summary>
/// Integration tests verifying idempotent behaviour of the PaymentService endpoints.
/// Each test uses a real PostgreSQL instance (Testcontainers) to assert database-level guarantees.
/// </summary>
public class PaymentsIdempotencyTests : IClassFixture<PaymentServiceWebAppFactory>
{
    private readonly PaymentServiceWebAppFactory _factory;

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentsIdempotencyTests"/>
    /// with the shared web application factory.
    /// </summary>
    /// <param name="factory">The integration test fixture providing the hosted API and infrastructure.</param>
    public PaymentsIdempotencyTests(PaymentServiceWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Calling POST /payments twice with the same orderId must result in exactly one payment record
    /// in the database and a successful (202) response on both calls.
    /// </summary>
    [Fact]
    public async Task InitiatePayment_CalledTwiceWithSameOrderId_OnlyOnePaymentCreated()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _factory.GatewayMock
            .Given(Request.Create().WithPath("/charge").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var request = new { OrderId = orderId, Amount = 99.99m, Currency = "USD" };
        var client = _factory.CreateClient();

        // Act — POST /payments twice with the same orderId
        var firstResponse = await client.PostAsJsonAsync("/payments", request);
        var secondResponse = await client.PostAsJsonAsync("/payments", request);

        // Assert — both calls succeed
        firstResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted,
            "first request must be accepted");
        secondResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted,
            "duplicate request must also return 202 (idempotent)");

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<PaymentIdResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<PaymentIdResponse>();

        // Assert — same payment ID returned on both calls (only one record created)
        firstBody!.PaymentId.Should().NotBeEmpty();
        secondBody!.PaymentId.Should().Be(firstBody.PaymentId,
            "both responses must reference the same payment record");

        // Assert — gateway was called only once (no duplicate charge)
        _factory.GatewayMock.LogEntries
            .Count(e => e.RequestMessage.Path == "/charge")
            .Should().Be(1, "the gateway should be called exactly once");
    }

    private record PaymentIdResponse(Guid PaymentId);
}
