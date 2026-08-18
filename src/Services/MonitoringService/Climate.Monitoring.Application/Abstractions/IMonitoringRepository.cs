using Climate.Monitoring.Domain.Readings;

namespace Climate.Monitoring.Application.Abstractions;

public interface IMonitoringRepository
{
    Task AddAsync(SensorReading reading, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SensorReading>> GetCurrentAsync(CancellationToken cancellationToken);
    Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SensorReading>> GetHistoryAsync(
        Guid? sensorId,
        Guid? communityId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken);
    Task DeleteAllAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
