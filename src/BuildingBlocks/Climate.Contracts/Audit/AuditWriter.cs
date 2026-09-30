using System.Diagnostics;
using System.Security.Claims;

namespace Climate.Contracts.Audit;

public sealed class AuditWriter(IAuditEventPublisher publisher)
{
    // Reserved actor for internal service operations; it is not an Identity account.
    public Task RecordSystemAsync(string action, string resource, string? resourceId, string description, CancellationToken token) =>
        RecordAsync(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), "system", action, resource, resourceId, description, null, token);

    public Task RecordAsync(ClaimsPrincipal user, string action, string resource, string? resourceId,
        string description, string? ipAddress, CancellationToken cancellationToken)
    {
        string? id=user.FindFirst(ClaimTypes.NameIdentifier)?.Value??user.FindFirst("sub")?.Value;
        string name=user.Identity?.Name??user.FindFirst(ClaimTypes.Name)?.Value??"unknown";
        return Guid.TryParse(id,out Guid userId)
            ? RecordAsync(userId,name,action,resource,resourceId,description,ipAddress,cancellationToken)
            : Task.CompletedTask;
    }

    public Task RecordAsync(Guid userId,string userName,string action,string resource,string? resourceId,
        string description,string? ipAddress,CancellationToken cancellationToken)
    {
        DateTimeOffset now=DateTimeOffset.UtcNow;
        var message=new AuditLogRequested(Guid.NewGuid(),now,Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),userId,userName,action,resource,resourceId,description,ipAddress);
        return publisher.PublishAsync(message,cancellationToken);
    }
}
