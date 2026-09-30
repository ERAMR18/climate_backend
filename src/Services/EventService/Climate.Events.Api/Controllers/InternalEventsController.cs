using Climate.Events.Api.Configuration;
using Climate.Contracts.Audit;
using Climate.Events.Api.Errors;
using Climate.Events.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Climate.Events.Api.Controllers;

[ApiController, AllowAnonymous, ApiExplorerSettings(IgnoreApi = true), Route("api/v1/internal/events")]
public sealed class InternalEventsController(IClimateEventService service, IOptions<InternalApiOptions> options, AuditWriter auditWriter) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EventResponse>> Record(RecordClimateEventRequest request, CancellationToken token)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var key) ||
            !string.Equals(key.ToString(), options.Value.ApiKey, StringComparison.Ordinal)) return Unauthorized();
        var result = await service.RecordAsync(request, token);
        if (result.IsSuccess)
            await auditWriter.RecordSystemAsync("RecordEvent", "Event", result.Value.Id.ToString(), "Climate event recorded.", token);
        return this.ToActionResult(result);
    }
}
