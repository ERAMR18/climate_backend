using Climate.Contracts.Realtime;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Domain.Readings;
using Climate.Monitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Climate.Monitoring.Api.Realtime;

public sealed class TelemetryDispatchWorker(IServiceScopeFactory scopes, ILogger<TelemetryDispatchWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> DispatchFailed = LoggerMessage.Define(
        LogLevel.Warning, new EventId(4001, "TelemetryDispatchFailed"), "Telemetry dispatch pending; retrying from persistent readings.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                MonitoringDbContext database = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
                IAlertEvaluationClient alerts = scope.ServiceProvider.GetRequiredService<IAlertEvaluationClient>();
                IRealtimePublisher realtime = scope.ServiceProvider.GetRequiredService<IRealtimePublisher>();
                SensorReading[] pending = await database.Readings.Where(item => item.DeviceId != null && !item.TelemetryDispatched)
                    .OrderBy(item => item.RecordedAt).Take(100).ToArrayAsync(stoppingToken);
                foreach (SensorReading reading in pending)
                {
                    // Alert Service deduplicates by readingId; SignalR consumers upsert by reading ID.
                    await alerts.EvaluateAsync(reading, stoppingToken);
                    await realtime.PublishAsync(RealtimeEventNames.SensorReadingUpdated, SensorReadingResponse.FromEntity(reading), stoppingToken);
                    reading.ConfirmTelemetryDispatch();
                    await database.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { DispatchFailed(logger, exception); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
