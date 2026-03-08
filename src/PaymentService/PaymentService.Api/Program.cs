using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using PaymentService.Api.Middleware;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.HttpClients;
using PaymentService.Infrastructure.Persistence;

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
    c.SwaggerDoc("v1", new() { Title = "Payment Service API", Version = "v1" });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// Database (Dapper)
builder.Services.Configure<DatabaseOptions>(opts =>
    opts.ConnectionString = builder.Configuration.GetConnectionString("PaymentDb")!);
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

// HTTP Clients
builder.Services.AddHttpClient<IPaymentGatewayClient, PaymentGatewayClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PaymentGateway"]!);
});
builder.Services.AddHttpClient<IOrchestratorClient, OrchestratorClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Orchestrator"]!);
});

// OpenTelemetry — traces, metrics, logs exported via OTLP
// OTLP endpoint is configured via OTEL_EXPORTER_OTLP_ENDPOINT environment variable.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("payment-service"))
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
