using Climate.Contracts.Alerts;
using Climate.Events.Domain.Events;

namespace Climate.Events.Application.Abstractions;

public interface IClimateEventRepository
{
    Task<IReadOnlyCollection<ClimateEvent>> ListPageAsync(Climate.Events.Application.Events.EventFilter filter, CancellationToken cancellationToken);
    Task<ClimateEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ClimateEvent?> GetByAlertIdAsync(Guid alertId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClimateEvent>> ListAsync(RiskType? riskType, AlertLevel? level, Guid? sensorId,
        Guid? communityId, DateTimeOffset? from, DateTimeOffset? until, CancellationToken cancellationToken);
    Task AddAsync(ClimateEvent climateEvent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
