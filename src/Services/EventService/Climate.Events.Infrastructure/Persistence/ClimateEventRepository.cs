using Climate.Contracts.Alerts;
using Climate.Events.Application.Abstractions;
using Climate.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Climate.Events.Infrastructure.Persistence;

internal sealed class ClimateEventRepository(EventsDbContext dbContext) : IClimateEventRepository
{
    public Task<ClimateEvent?> GetByIdAsync(Guid id, CancellationToken token) => dbContext.Events.FirstOrDefaultAsync(x => x.Id == id, token);
    public Task<ClimateEvent?> GetByAlertIdAsync(Guid alertId, CancellationToken token) => dbContext.Events.FirstOrDefaultAsync(x => x.AlertId == alertId, token);
    public async Task<IReadOnlyCollection<ClimateEvent>> ListAsync(RiskType? riskType, AlertLevel? level,
        Guid? sensorId, Guid? communityId, DateTimeOffset? from, DateTimeOffset? until, CancellationToken token)
    {
        IQueryable<ClimateEvent> query = dbContext.Events.AsNoTracking();
        if (riskType.HasValue) query = query.Where(x => x.RiskType == riskType);
        if (level.HasValue) query = query.Where(x => x.AlertLevel == level);
        if (sensorId.HasValue) query = query.Where(x => x.SensorId == sensorId);
        if (communityId.HasValue) query = query.Where(x => x.CommunityId == communityId);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
        if (until.HasValue) query = query.Where(x => x.OccurredAt <= until);
        return await query.OrderByDescending(x => x.OccurredAt).ToArrayAsync(token);
    }
    public Task AddAsync(ClimateEvent item, CancellationToken token) => dbContext.Events.AddAsync(item, token).AsTask();
    public Task SaveChangesAsync(CancellationToken token) => dbContext.SaveChangesAsync(token);
}
