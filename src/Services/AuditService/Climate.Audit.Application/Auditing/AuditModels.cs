using Climate.Audit.Domain.Auditing;

namespace Climate.Audit.Application.Auditing;

public sealed record AuditFilter(Guid? UserId, string? Action, string? Resource, DateTimeOffset? From, DateTimeOffset? To);
public sealed record AuditResponse(Guid Id, Guid UserId, string UserName, string Action, string Resource,
    string? ResourceId, string Description, string? IpAddress, DateTimeOffset Timestamp)
{
    public static AuditResponse FromEntity(AuditLog x) => new(x.Id,x.UserId,x.UserName,x.Action,x.Resource,x.ResourceId,x.Description,x.IpAddress,x.Timestamp);
}
public sealed record RecordAuditRequest(Guid EventId, Guid UserId, string UserName, string Action, string Resource,
    string? ResourceId, string Description, string? IpAddress, DateTimeOffset Timestamp);
