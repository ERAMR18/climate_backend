using System.Text.Json;
using Climate.Contracts.Realtime;
using Climate.Monitoring.Api.Configuration;
using Climate.Monitoring.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace Climate.Monitoring.Api.Controllers;
[ApiController,AllowAnonymous,ApiExplorerSettings(IgnoreApi=true),Route("api/v1/internal/realtime")]
public sealed class InternalRealtimeController(IRealtimePublisher publisher,IOptions<InternalApiOptions> options):ControllerBase
{
    [HttpPost] public async Task<ActionResult> Publish(InternalRealtimeRequest request,CancellationToken cancellationToken)
    {
        if(!Request.Headers.TryGetValue(InternalApiOptions.HeaderName,out var key)||!string.Equals(key.ToString(),options.Value.ApiKey,StringComparison.Ordinal))return Unauthorized();
        if(!RealtimeEventNames.All.Contains(request.EventName,StringComparer.Ordinal))return UnprocessableEntity();
        await publisher.PublishAsync(request.EventName,request.Payload,cancellationToken); return Accepted();
    }
}
public sealed record InternalRealtimeRequest(string EventName,JsonElement Payload);
