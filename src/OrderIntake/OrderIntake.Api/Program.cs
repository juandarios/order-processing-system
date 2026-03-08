using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OrderIntake.Api.Middleware;
using OrderIntake.Application.Interfaces;
using OrderIntake.Infrastructure.HttpClients;
using OrderIntake.Infrastructure.Kafka;
using OrderIntake.Infrastructure.Persistence;
using OrderIntake.Infrastructure.Persistence.Repositories;

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
    c.SwaggerDoc("v1", new() { Title = "Order Intake API", Version = "v1" });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// EF Core
builder.Services.AddDbContext<OrderDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("OrderIntakeDb")));

// Repository
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// HTTP Clients
builder.Services.AddHttpClient<IStockServiceClient, StockServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:StockService"]!);
});
builder.Services.AddHttpClient<IOrchestratorClient, OrchestratorClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Orchestrator"]!);
});

// Kafka consumer
builder.Services.Configure<KafkaConsumerOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddKeyedSingleton<IEventConsumer, KafkaConsumer>("kafka");
builder.Services.AddHostedService<KafkaConsumerHostedService>();

// OpenTelemetry — traces, metrics, logs exported via OTLP
// OTLP endpoint is configured via OTEL_EXPORTER_OTLP_ENDPOINT environment variable.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("order-intake"))
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

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler();
app.MapControllers();

app.Run();

/// <summary>
/// Entry point marker for integration test WebApplicationFactory.
/// </summary>
public partial class Program;
