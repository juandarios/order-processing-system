using OrderOrchestrator.Api.Middleware;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Infrastructure.BackgroundJobs;
using OrderOrchestrator.Infrastructure.HttpClients;
using OrderOrchestrator.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(OrderOrchestrator.Application.Commands.ProcessStockValidated.ProcessStockValidatedCommand).Assembly));

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

// HTTP Clients
builder.Services.AddHttpClient<IPaymentServiceClient, PaymentServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PaymentService"]!);
});

// Background jobs
builder.Services.AddHostedService<TimeoutPollingJob>();

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
