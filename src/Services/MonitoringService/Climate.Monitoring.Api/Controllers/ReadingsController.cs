using Climate.Contracts.Identity;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Domain.Readings;
using Climate.Monitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Climate.Monitoring.Api.Controllers;

[ApiController, Authorize, Route("api/v1/monitoring")]
public sealed class ReadingsController(MonitoringDbContext db, ISensorCatalogClient sensors) : ControllerBase
{
    [HttpGet("readings")]
    public async Task<ActionResult<ReadingPage>> List([FromQuery] Guid? communityId, [FromQuery] Guid? sensorId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken token = default)
    {
        if (from > to || page < 1 || page > 1000000 || pageSize < 1 || pageSize > 500) return ValidationProblem("Invalid range or pagination.");
        var query = db.Readings.AsNoTracking();
        if (communityId.HasValue) query = query.Where(x => x.CommunityId == communityId);
        if (sensorId.HasValue) query = query.Where(x => x.SensorId == sensorId);
        if (from.HasValue) query = query.Where(x => x.RecordedAt >= from);
        if (to.HasValue) query = query.Where(x => x.RecordedAt <= to);
        int total = await query.CountAsync(token);
        var rows = await query.OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(token);
        return Ok(new ReadingPage(rows.Select(SensorReadingResponse.FromEntity).ToArray(), total, page, pageSize));
    }

    [HttpGet("simulation/values/{sensorId:guid}")]
    public async Task<ActionResult<SimulationValueResponse>> GetValue(Guid sensorId, CancellationToken token)
    {
        var item = await db.SimulationOverrides.AsNoTracking().SingleOrDefaultAsync(x => x.SensorId == sensorId, token);
        return Ok(new SimulationValueResponse(sensorId, item?.Value, item?.UpdatedAt));
    }

    [HttpPut("simulation/values/{sensorId:guid}"), Authorize(Roles = SystemRoles.Administrator)]
    public async Task<ActionResult<SimulationValueResponse>> SetValue(Guid sensorId, SimulationValueRequest request, CancellationToken token)
    {
        if (request.Value is < -1000000m or > 1000000m) return ValidationProblem("Value must be between -1000000 and 1000000.");
        if (!(await sensors.GetActiveSensorsAsync(token)).Any(x => x.Id == sensorId && x.IsActive)) return NotFound();
        var item = await db.SimulationOverrides.SingleOrDefaultAsync(x => x.SensorId == sensorId, token);
        if (item is null) { item = new SimulationOverride { SensorId = sensorId }; db.SimulationOverrides.Add(item); }
        item.Value = request.Value; item.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token);
        return Ok(new SimulationValueResponse(sensorId, item.Value, item.UpdatedAt));
    }
    [HttpDelete("simulation/values/{sensorId:guid}"), Authorize(Roles = SystemRoles.Administrator)]
    public async Task<ActionResult> ClearValue(Guid sensorId, CancellationToken token)
    {
        var item = await db.SimulationOverrides.SingleOrDefaultAsync(x => x.SensorId == sensorId, token);
        if (item is not null) { db.SimulationOverrides.Remove(item); await db.SaveChangesAsync(token); }
        return NoContent();
    }
}
public sealed record SimulationValueRequest(decimal Value);
public sealed record SimulationValueResponse(Guid SensorId, decimal? Value, DateTimeOffset? UpdatedAt);
public sealed record ReadingPage(IReadOnlyCollection<SensorReadingResponse> Items, int Total, int Page, int PageSize);
