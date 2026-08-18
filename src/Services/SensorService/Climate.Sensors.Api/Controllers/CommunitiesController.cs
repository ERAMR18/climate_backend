using Climate.Sensors.Api.Errors;
using Climate.Sensors.Application.Communities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Sensors.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communities")]
public sealed class CommunitiesController(ICommunityService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CommunityResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}", Name = "GetCommunityById")]
    public async Task<ActionResult<CommunityResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "ManageSensors")]
    public async Task<ActionResult<CommunityResponse>> Create(
        CreateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManageSensors")]
    public async Task<ActionResult<CommunityResponse>> Update(
        Guid id,
        UpdateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return this.ToActionResult(result);
    }
}
