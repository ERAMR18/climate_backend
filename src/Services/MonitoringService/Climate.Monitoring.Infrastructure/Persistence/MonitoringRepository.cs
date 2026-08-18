using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Domain.Readings;
using Microsoft.EntityFrameworkCore;

namespace Climate.Monitoring.Infrastructure.Persistence;

internal sealed class MonitoringRepository(MonitoringDbContext dbContext) : IMonitoringRepository
{
    public async Task AddAsync(SensorReading reading, CancellationToken cancellationToken) =>
        await dbContext.Readings.AddAsync(reading, cancellationToken);

    public async Task<IReadOnlyCollection<SensorReading>> GetCurrentAsync(CancellationToken cancellationToken) =>
        await dbContext.Readings
            .AsNoTracking()
            .GroupBy(reading => reading.SensorId)
            .Select(group => group.OrderByDescending(reading => reading.RecordedAt).First())
            .ToArrayAsync(cancellationToken);

    public Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken) =>
        dbContext.Readings.AsNoTracking()
            .Where(reading => reading.SensorId == sensorId)
            .OrderByDescending(reading => reading.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SensorReading>> GetHistoryAsync(
        Guid? sensorId,
        Guid? communityId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken)
    {
        IQueryable<SensorReading> query = dbContext.Readings.AsNoTracking();
        if (sensorId.HasValue)
        {
            query = query.Where(reading => reading.SensorId == sensorId.Value);
        }

        if (communityId.HasValue)
        {
            query = query.Where(reading => reading.CommunityId == communityId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(reading => reading.RecordedAt >= from.Value);
        }

        if (until.HasValue)
        {
            query = query.Where(reading => reading.RecordedAt <= until.Value);
        }

        return await query.OrderBy(reading => reading.RecordedAt).ToArrayAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Readings.ExecuteDeleteAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
