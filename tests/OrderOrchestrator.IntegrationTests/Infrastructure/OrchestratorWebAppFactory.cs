using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderOrchestrator.Infrastructure.BackgroundJobs;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace OrderOrchestrator.IntegrationTests.Infrastructure;

public class OrchestratorWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public WireMockServer PaymentServiceMock { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        PaymentServiceMock = WireMockServer.Start();
    }

    public new async Task DisposeAsync()
    {
        PaymentServiceMock.Stop();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrchestratorDb"] = _postgres.GetConnectionString(),
                ["Services:PaymentService"] = PaymentServiceMock.Urls[0] + "/",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the timeout polling job to avoid background interference in tests
            var descriptor = services.SingleOrDefault(d =>
                d.ImplementationType == typeof(TimeoutPollingJob));
            if (descriptor != null)
                services.Remove(descriptor);
        });
    }
}
