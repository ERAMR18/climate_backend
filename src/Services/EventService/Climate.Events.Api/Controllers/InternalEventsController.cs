using Climate.Events.Api.Configuration;
using Climate.Events.Api.Errors;
using Climate.Events.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Climate.Events.Api.Controllers;

[ApiController, AllowAnonymous, ApiExplorerSettings(IgnoreApi = true), Route("api/v1/internal/events")]
public sealed class InternalEventsController(IClimateEventService service, IOptions<InternalApiOptions> options) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EventResponse>> Record(RecordClimateEventRequest request, CancellationToken token)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var key) ||
            !string.Equals(key.ToString(), options.Value.ApiKey, StringComparison.Ordinal)) return Unauthorized();
        return this.ToActionResult(await service.RecordAsync(request, token));
    }
}
