using Climate.Contracts.Alerts;
using Climate.Events.Api.Errors;
using Climate.Events.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Events.Api.Controllers;

[ApiController, Authorize, Route("api/v1/events")]
public sealed class EventsController(IClimateEventService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EventResponse>>> List([FromQuery] RiskType? riskType,
        [FromQuery] AlertLevel? alertLevel, [FromQuery] Guid? sensorId, [FromQuery] Guid? communityId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int? page = null, [FromQuery] int pageSize = 250, CancellationToken token = default)
    {
        if (from > to) return ValidationProblem("'from' must be before or equal to 'to'.");
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 500) return Problem(statusCode: 422, title: "Invalid pagination.");
        return Ok(await service.ListAsync(new(riskType, alertLevel, sensorId, communityId, from, to) { Page = page, PageSize = pageSize }, token));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventResponse>> Get(Guid id, CancellationToken token) =>
        this.ToActionResult(await service.GetByIdAsync(id, token));
}
