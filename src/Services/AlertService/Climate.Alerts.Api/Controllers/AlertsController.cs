using Climate.Alerts.Api.Errors;
using Climate.Alerts.Application.Alerts;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Alerts.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/alerts")]
public sealed class AlertsController(IAlertService service,RealtimeWriter realtimeWriter) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AlertResponse>>> GetAll(
        [FromQuery] RiskType? riskType,
        [FromQuery] AlertLevel? alertLevel,
        [FromQuery] Guid? sensorId,
        [FromQuery] Guid? communityId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(
            new AlertFilter(riskType, alertLevel, sensorId, communityId, isActive),
            cancellationToken));

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    public async Task<ActionResult<AlertResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Policy = "ManageAlerts")]
    public async Task<ActionResult> Resolve(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.ResolveAsync(id, cancellationToken);
        if(result.IsSuccess)
        {
            var alert=await service.GetByIdAsync(id,cancellationToken);
            if(alert.IsSuccess)await realtimeWriter.PublishAsync(RealtimeEventNames.AlertGenerated,alert.Value,cancellationToken);
        }
        return this.ToActionResult(result);
    }
}
