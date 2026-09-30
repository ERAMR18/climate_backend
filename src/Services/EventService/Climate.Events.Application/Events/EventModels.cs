using Climate.Contracts.Alerts;
using Climate.Events.Domain.Events;

namespace Climate.Events.Application.Events;

public sealed record EventFilter(RiskType? RiskType, AlertLevel? AlertLevel, Guid? SensorId,
    Guid? CommunityId, DateTimeOffset? From, DateTimeOffset? To);

public sealed record EventResponse(Guid Id, Guid AlertId, Guid SensorId, Guid CommunityId,
    RiskType RiskType, AlertLevel AlertLevel, string Description, DateTimeOffset OccurredAt,
    DateTimeOffset? ResolvedAt, decimal? Value = null, string Status = "Active", Guid? ResponsibleUserId = null)
{
    public static EventResponse FromEntity(ClimateEvent value) => new(value.Id, value.AlertId, value.SensorId,
        value.CommunityId, value.RiskType, value.AlertLevel, value.Description, value.OccurredAt, value.ResolvedAt, value.Value, value.Status, value.ResponsibleUserId);
}

public sealed record RecordClimateEventRequest(Guid EventId, Guid AlertId, Guid SensorId, Guid CommunityId,
    RiskType RiskType, AlertLevel AlertLevel, string Description, DateTimeOffset OccurredAt,
    DateTimeOffset? ResolvedAt, decimal? Value = null, string Status = "Active", Guid? ResponsibleUserId = null);
