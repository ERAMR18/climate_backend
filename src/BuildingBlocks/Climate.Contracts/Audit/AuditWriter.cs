using System.Net.Http.Json;
using System.Security.Claims;

namespace Climate.Contracts.Audit;

public sealed class AuditWriter(HttpClient httpClient, string apiKey)
{
    public Task RecordAsync(ClaimsPrincipal user, string action, string resource, string? resourceId,
        string description, string? ipAddress, CancellationToken cancellationToken)
    {
        string? id=user.FindFirst(ClaimTypes.NameIdentifier)?.Value??user.FindFirst("sub")?.Value;
        string name=user.Identity?.Name??user.FindFirst(ClaimTypes.Name)?.Value??"unknown";
        return Guid.TryParse(id,out Guid userId)
            ? RecordAsync(userId,name,action,resource,resourceId,description,ipAddress,cancellationToken)
            : Task.CompletedTask;
    }

    public async Task RecordAsync(Guid userId,string userName,string action,string resource,string? resourceId,
        string description,string? ipAddress,CancellationToken cancellationToken)
    {
        DateTimeOffset now=DateTimeOffset.UtcNow;
        var message=new AuditLogRequested(Guid.NewGuid(),now,Guid.NewGuid().ToString(),userId,userName,action,resource,resourceId,description,ipAddress);
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/v1/internal/audit"){Content=JsonContent.Create(new
        { message.EventId,message.UserId,message.UserName,message.Action,message.Resource,message.ResourceId,message.Description,message.IpAddress,Timestamp=message.OccurredAt })};
        request.Headers.Add("X-Internal-Api-Key",apiKey);
        using HttpResponseMessage response=await httpClient.SendAsync(request,cancellationToken); response.EnsureSuccessStatusCode();
    }
}
