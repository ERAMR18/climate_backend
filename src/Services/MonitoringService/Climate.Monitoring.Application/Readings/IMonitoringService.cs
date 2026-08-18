using Climate.SharedKernel.Results;

namespace Climate.Monitoring.Application.Readings;

public interface IMonitoringService
{
    Task<IReadOnlyCollection<SensorReadingResponse>> GetCurrentAsync(CancellationToken cancellationToken);
    Task<Result<SensorReadingResponse>> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<SensorReadingResponse>>> GetHistoryAsync(
        Guid sensorId,
        HistoryFilter filter,
        CancellationToken cancellationToken);
    Task<Result<SensorReadingResponse>> CreateAsync(CreateReadingRequest request, CancellationToken cancellationToken);
    Task<Result<SensorChartResponse>> GetChartAsync(
        Guid sensorId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        string interval,
        CancellationToken cancellationToken);
    Task GenerateSimulationBatchAsync(CancellationToken cancellationToken);
}
