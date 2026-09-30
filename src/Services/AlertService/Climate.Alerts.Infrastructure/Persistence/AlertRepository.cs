using Climate.Alerts.Application.Abstractions;
using Climate.Alerts.Domain.Alerts;
using Microsoft.EntityFrameworkCore;

namespace Climate.Alerts.Infrastructure.Persistence;

internal sealed class AlertRepository(AlertsDbContext dbContext) : IAlertRepository
{
    public async Task AddAsync(ClimateAlert alert, CancellationToken cancellationToken) =>
        await dbContext.Alerts.AddAsync(alert, cancellationToken);

    public Task<ClimateAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Alerts.SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public Task<ClimateAlert?> GetActiveAsync(
        Guid sensorId,
        RiskType riskType,
        CancellationToken cancellationToken) =>
        dbContext.Alerts.SingleOrDefaultAsync(
            alert => alert.SensorId == sensorId && alert.AlertType == riskType && alert.IsActive,
            cancellationToken);

    public async Task<IReadOnlyCollection<ClimateAlert>> ListAsync(
        RiskType? riskType,
        AlertLevel? level,
        Guid? sensorId,
        Guid? communityId,
        bool? isActive,
        CancellationToken cancellationToken, DateTimeOffset? from = null, DateTimeOffset? until = null, AlertStatus? status = null)
    {
        IQueryable<ClimateAlert> query = dbContext.Alerts.AsNoTracking();
        if (riskType.HasValue)
        {
            query = query.Where(alert => alert.AlertType == riskType.Value);
        }

        if (level.HasValue)
        {
            query = query.Where(alert => alert.Level == level.Value);
        }

        if (sensorId.HasValue)
        {
            query = query.Where(alert => alert.SensorId == sensorId.Value);
        }

        if (communityId.HasValue)
        {
            query = query.Where(alert => alert.CommunityId == communityId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(alert => alert.IsActive == isActive.Value);
        }

        if (from.HasValue) query = query.Where(x => x.GeneratedAt >= from);
        if (until.HasValue) query = query.Where(x => x.GeneratedAt <= until);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(alert => alert.GeneratedAt).ToArrayAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
