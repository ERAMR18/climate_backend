using Climate.Communities.Api.Persistence;
using Climate.Contracts.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Climate.Communities.Api.Controllers;

[ApiController, Authorize, Route("api/v1/communities")]
public sealed class CommunitiesController(CommunityDbContext db, SensorCountsClient sensors, AuditWriter audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CommunityResponse>>> List(CancellationToken token,
        [FromQuery] string? search = null, [FromQuery] bool? isActive = null,
        [FromQuery] string? municipality = null, [FromQuery] string? department = null,
        [FromQuery] int? page = null, [FromQuery] int pageSize = 250)
    {
        IQueryable<Community> query = db.Communities.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(municipality)) query = query.Where(x => x.Municipality == municipality.Trim());
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(x => x.Department == department.Trim());
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 500) return Problem(statusCode: 422, title: "Invalid pagination.");
        query = query.OrderBy(x => x.Name).ThenBy(x => x.Id);
        if (page.HasValue) query = query.Skip((page.Value - 1) * pageSize).Take(pageSize);
        Community[] communities = await query.ToArrayAsync(token);
        Dictionary<Guid, int> counts = await sensors.GetAsync(token);
        return Ok(communities.Select(x => CommunityResponse.From(x, counts.GetValueOrDefault(x.Id))).ToArray());
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CommunityResponse>> Get(Guid id, CancellationToken token)
    {
        Community? entity = await db.Communities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
        if (entity is null) return NotFound();
        Dictionary<Guid, int> counts = await sensors.GetAsync(token);
        return Ok(CommunityResponse.From(entity, counts.GetValueOrDefault(id)));
    }
    [HttpPost, Authorize(Roles = "Administrator")]
    public async Task<ActionResult<CommunityResponse>> Create(CommunityRequest request, CancellationToken token)
    {
        if (!request.IsValid()) return Problem(statusCode: 422, title: "Invalid community fields or coordinates.");
        Community entity = new() { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        request.Apply(entity); db.Communities.Add(entity);
        await db.SaveChangesAsync(token);
        await audit.RecordAsync(User, "Create", "Community", entity.Id.ToString(), "Community created.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, CommunityResponse.From(entity));
    }
    [HttpPut("{id:guid}"), Authorize(Roles = "Administrator")]
    public async Task<ActionResult<CommunityResponse>> Update(Guid id, CommunityRequest request, CancellationToken token)
    {
        if (!request.IsValid()) return Problem(statusCode: 422, title: "Invalid community fields or coordinates.");
        Community? entity = await db.Communities.SingleOrDefaultAsync(x => x.Id == id, token);
        if (entity is null) return NotFound();
        request.Apply(entity); await db.SaveChangesAsync(token);
        await audit.RecordAsync(User, "Update", "Community", id.ToString(), "Community updated.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
        Dictionary<Guid, int> counts = await sensors.GetAsync(token);
        return Ok(CommunityResponse.From(entity, counts.GetValueOrDefault(id)));
    }
    [HttpPatch("{id:guid}/activate"), Authorize(Roles = "Administrator")]
    public Task<IActionResult> Activate(Guid id, CancellationToken token) => SetStatus(id, true, token);
    [HttpPatch("{id:guid}/deactivate"), Authorize(Roles = "Administrator")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken token) => SetStatus(id, false, token);
    private async Task<IActionResult> SetStatus(Guid id, bool active, CancellationToken token)
    {
        Community? entity = await db.Communities.SingleOrDefaultAsync(x => x.Id == id, token);
        if (entity is null) return NotFound();
        entity.IsActive = active; await db.SaveChangesAsync(token);
        await audit.RecordAsync(User, active ? "Activate" : "Deactivate", "Community", id.ToString(), "Community status updated.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
        return NoContent();
    }
}

[ApiController, AllowAnonymous, Route("api/v1/internal/communities")]
public sealed class InternalCommunitiesController(CommunityDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CommunityResponse>>> List(CancellationToken token)
    {
        if (Request.Headers["X-Internal-Api-Key"] != configuration["InternalApi:ApiKey"]) return Unauthorized();
        Community[] entities = await db.Communities.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(token);
        return Ok(entities.Select(x => CommunityResponse.From(x)).ToArray());
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CommunityResponse>> Get(Guid id, CancellationToken token)
    {
        if (Request.Headers["X-Internal-Api-Key"] != configuration["InternalApi:ApiKey"]) return Unauthorized();
        Community? entity = await db.Communities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
        return entity is null ? NotFound() : Ok(CommunityResponse.From(entity));
    }
}
