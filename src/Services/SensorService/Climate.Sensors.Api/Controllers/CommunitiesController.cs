using Climate.Sensors.Api.Errors;
using Climate.Contracts.Audit;
using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Application.Communities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Sensors.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communities")]
public sealed class CommunitiesController(ICommunityService service, AuditWriter auditWriter) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CommunityResponse>>> GetAll(
        [FromServices] ISensorCatalogRepository repository, CancellationToken cancellationToken,
        [FromQuery] string? search = null, [FromQuery] bool? isActive = null,
        [FromQuery] string? municipality = null, [FromQuery] string? department = null) =>
        Ok(await repository.SearchCommunitiesAsync(search, isActive, municipality, department, cancellationToken));

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Roles = "Administrator")]
    public Task<ActionResult> Activate(Guid id, [FromServices] ISensorCatalogRepository repository, CancellationToken token) => SetStatus(id, true, repository, token);

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "Administrator")]
    public Task<ActionResult> Deactivate(Guid id, [FromServices] ISensorCatalogRepository repository, CancellationToken token) => SetStatus(id, false, repository, token);

    private async Task<ActionResult> SetStatus(Guid id, bool active, ISensorCatalogRepository repository, CancellationToken token)
    {
        var community = await repository.GetCommunityAsync(id, token);
        if (community is null) return NotFound();
        community.SetStatus(active);
        await repository.SaveChangesAsync(token);
        await auditWriter.RecordAsync(User, active ? AuditActions.Activate : AuditActions.Deactivate, "Community", id.ToString(), "Community status updated.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
        return NoContent();
    }

    [HttpGet("{id:guid}", Name = "GetCommunityById")]
    public async Task<ActionResult<CommunityResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<CommunityResponse>> Create(
        CreateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.IsSuccess) await auditWriter.RecordAsync(User, AuditActions.Create, "Community", result.Value.Id.ToString(), "Community created.", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<CommunityResponse>> Update(
        Guid id,
        UpdateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess) await auditWriter.RecordAsync(User, AuditActions.Update, "Community", id.ToString(), "Community updated.", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return this.ToActionResult(result);
    }
}
