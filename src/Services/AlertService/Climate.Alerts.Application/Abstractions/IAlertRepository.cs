using Climate.Alerts.Domain.Alerts;

namespace Climate.Alerts.Application.Abstractions;

public interface IAlertRepository
{
    Task AddAsync(ClimateAlert alert, CancellationToken cancellationToken);
    Task<ClimateAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ClimateAlert?> GetActiveAsync(Guid sensorId, RiskType riskType, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClimateAlert>> ListAsync(
        RiskType? riskType,
        AlertLevel? level,
        Guid? sensorId,
        Guid? communityId,
        bool? isActive,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
