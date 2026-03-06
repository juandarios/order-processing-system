using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace PaymentService.IntegrationTests.Infrastructure;

public class PaymentServiceWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public WireMockServer GatewayMock { get; private set; } = null!;
    public WireMockServer OrchestratorMock { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        GatewayMock = WireMockServer.Start();
        OrchestratorMock = WireMockServer.Start();
    }

    public new async Task DisposeAsync()
    {
        GatewayMock.Stop();
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
                ["ConnectionStrings:PaymentDb"] = _postgres.GetConnectionString(),
                ["Services:PaymentGateway"] = GatewayMock.Urls[0] + "/",
                ["Services:Orchestrator"] = OrchestratorMock.Urls[0] + "/",
            });
        });
    }
}
