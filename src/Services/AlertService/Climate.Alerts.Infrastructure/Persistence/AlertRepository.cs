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
        CancellationToken cancellationToken)
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

        return await query.OrderByDescending(alert => alert.GeneratedAt).ToArrayAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
