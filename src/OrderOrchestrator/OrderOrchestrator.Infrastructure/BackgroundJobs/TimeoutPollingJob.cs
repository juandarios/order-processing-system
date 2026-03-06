using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderOrchestrator.Application.Interfaces;
using OrderOrchestrator.Application.Logging;
using OrderOrchestrator.Application.StateMachine;

namespace OrderOrchestrator.Infrastructure.BackgroundJobs;

/// <summary>
/// Background polling job that detects timed-out payment sagas every 30 seconds
/// and transitions them to the Failed state.
/// </summary>
public class TimeoutPollingJob(
    IServiceScopeFactory scopeFactory,
    ILogger<TimeoutPollingJob> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TimeoutPollingJob started, polling every {Interval}s", PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTimedOutSagasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during timeout polling");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessTimedOutSagasAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderSagaRepository>();

        var timedOut = await repository.GetTimedOutSagasAsync(ct);

        foreach (var saga in timedOut)
        {
            try
            {
                logger.SagaTimedOut(saga.OrderId);
                var machine = new OrderStateMachine(saga);
                machine.FireTimeout();
                await repository.UpdateAsync(saga, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process timeout for order {OrderId}", saga.OrderId);
            }
        }
    }
}
