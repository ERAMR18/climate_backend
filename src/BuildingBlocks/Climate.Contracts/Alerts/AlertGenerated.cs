using Climate.Contracts.Common;

namespace Climate.Contracts.Alerts;

public sealed record AlertGenerated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid AlertId,
    Guid SensorId,
    Guid CommunityId,
    RiskType RiskType,
    AlertLevel Level,
    string Title,
    string Description,
    decimal SensorValue,
    decimal ThresholdValue,
    DateTimeOffset GeneratedAt)
    : IntegrationEvent(EventId, OccurredAt, CorrelationId);
