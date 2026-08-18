using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Climate.Monitoring.Infrastructure.Simulation;

internal sealed class ClimateSimulationWorker(
    IServiceScopeFactory scopeFactory,
    ISimulationControl control,
    IOptions<SimulationOptions> options,
    ILogger<ClimateSimulationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogCycleFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(6001, nameof(LogCycleFailure)),
        "Climate simulation cycle failed.");

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!control.IsRunning)
            {
                continue;
            }

            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IMonitoringService service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
                await service.GenerateSimulationBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogCycleFailure(logger, exception);
            }
        }
    }
}
