using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderIntake.Infrastructure.Kafka;
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
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove Kafka hosted service so it doesn't try to connect during tests
            var descriptor = services.SingleOrDefault(d =>
                d.ImplementationType == typeof(KafkaConsumerHostedService));
            if (descriptor != null)
                services.Remove(descriptor);
        });
    }
}
