using Climate.Contracts.Common;
using Climate.Contracts.Sensors;

namespace Climate.Contracts.Monitoring;

public sealed record SensorReadingRecorded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid ReadingId,
    Guid SensorId,
    Guid CommunityId,
    SensorType SensorType,
    decimal Value,
    string Unit,
    DateTimeOffset RecordedAt)
    : IntegrationEvent(EventId, OccurredAt, CorrelationId);
