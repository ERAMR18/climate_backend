using Climate.Contracts.Common;

namespace Climate.Contracts.Audit;

public sealed record AuditLogRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid UserId,
    string UserName,
    string Action,
    string Resource,
    string? ResourceId,
    string Description,
    string? IpAddress)
    : IntegrationEvent(EventId, OccurredAt, CorrelationId);
