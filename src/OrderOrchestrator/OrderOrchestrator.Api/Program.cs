using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OrderOrchestrator.Api.Middleware;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Infrastructure.BackgroundJobs;
using OrderOrchestrator.Infrastructure.HttpClients;
using OrderOrchestrator.Infrastructure.Persistence;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Mediator (MIT community fork — replaces MediatR)
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Order Orchestrator API", Version = "v1" });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// Database (Dapper)
builder.Services.Configure<OrchestratorDatabaseOptions>(opts =>
    opts.ConnectionString = builder.Configuration.GetConnectionString("OrchestratorDb")!);
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IOrderSagaRepository, OrderSagaRepository>();

// HTTP Clients with resilience policies
builder.Services.AddHttpClient<IPaymentServiceClient, PaymentServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PaymentService"]!);
})
.AddResilienceHandler("payment-service", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => (int)r.StatusCode >= 500)
    });
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 1.0,
        MinimumThroughput = 5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(30)
    });
});

// Background jobs
builder.Services.AddHostedService<TimeoutPollingJob>();

// OpenTelemetry — traces, metrics, logs exported via OTLP
// OTLP endpoint is configured via OTEL_EXPORTER_OTLP_ENDPOINT environment variable.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("order-orchestrator"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeFormattedMessage = true;
    o.AddOtlpExporter();
});

// Exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Initialize database schema
var dbInit = app.Services.GetRequiredService<DatabaseInitializer>();
await dbInit.InitializeAsync();

app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler();
app.MapControllers();

app.Run();

/// <summary>
/// Entry point marker for integration test WebApplicationFactory.
/// </summary>
public partial class Program;
