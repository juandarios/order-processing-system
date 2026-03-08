using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Application.Interfaces;
using OrderIntake.Infrastructure.Kafka;
using NSubstitute;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace OrderIntake.IntegrationTests.Infrastructure;

public class OrderIntakeWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public WireMockServer StockMock { get; private set; } = null!;
    public WireMockServer OrchestratorMock { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        StockMock = WireMockServer.Start();
        OrchestratorMock = WireMockServer.Start();
    }

    public new async Task DisposeAsync()
    {
        StockMock.Stop();
        OrchestratorMock.Stop();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderIntakeDb"] = _postgres.GetConnectionString(),
                ["Services:StockService"] = StockMock.Urls[0] + "/",
                ["Services:Orchestrator"] = OrchestratorMock.Urls[0] + "/",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:GroupId"] = "test-group",
                ["Kafka:OrderPlacedTopic"] = "test-order-placed",
                ["Kafka:DlqTopic"] = "test-order-placed-dlq",
                ["Kafka:ValidationErrorTopic"] = "test-order-validation-errors",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove Kafka hosted services so they don't try to connect during tests
            var kafkaDescriptor = services.SingleOrDefault(d =>
                d.ImplementationType == typeof(KafkaConsumerHostedService));
            if (kafkaDescriptor != null)
                services.Remove(kafkaDescriptor);

            var dlqDescriptor = services.SingleOrDefault(d =>
                d.ImplementationType == typeof(DlqConsumerHostedService));
            if (dlqDescriptor != null)
                services.Remove(dlqDescriptor);

            // Replace DLQ publisher with a no-op stub for tests
            var dlqPublisherDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IDlqPublisher));
            if (dlqPublisherDescriptor != null)
                services.Remove(dlqPublisherDescriptor);
            services.AddScoped<IDlqPublisher>(_ => Substitute.For<IDlqPublisher>());

            // Replace validation error publisher with a no-op stub for tests
            var validationErrorDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IValidationErrorPublisher));
            if (validationErrorDescriptor != null)
                services.Remove(validationErrorDescriptor);
            services.AddScoped<IValidationErrorPublisher>(_ => Substitute.For<IValidationErrorPublisher>());
        });
    }
}
