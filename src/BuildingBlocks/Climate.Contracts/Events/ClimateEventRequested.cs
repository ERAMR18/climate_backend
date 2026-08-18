using Climate.Contracts.Alerts;
using Climate.Contracts.Common;

namespace Climate.Contracts.Events;

public sealed record ClimateEventRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid AlertId,
    Guid SensorId,
    Guid CommunityId,
    RiskType RiskType,
    AlertLevel AlertLevel,
    string Description)
    : IntegrationEvent(EventId, OccurredAt, CorrelationId);
