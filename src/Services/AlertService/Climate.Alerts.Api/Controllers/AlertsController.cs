using Climate.Alerts.Api.Errors;
using Climate.Alerts.Application.Alerts;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Realtime;
using Climate.Contracts.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Alerts.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/alerts")]
public sealed class AlertsController(IAlertService service,RealtimeWriter realtimeWriter, AuditWriter auditWriter) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AlertResponse>>> GetAll(
        [FromQuery] RiskType? riskType,
        [FromQuery] AlertLevel? alertLevel,
        [FromQuery] Guid? sensorId,
        [FromQuery] Guid? communityId,
        [FromQuery] bool? isActive,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] AlertStatus? status,
        CancellationToken cancellationToken)
    {
        if (from > to) return ValidationProblem("Invalid date range.");
        return Ok(await service.ListAsync(
            new AlertFilter(riskType, alertLevel, sensorId, communityId, isActive, from, to, status),
            cancellationToken));
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    public async Task<ActionResult<AlertResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/attend"), Authorize(Policy = "ManageAlerts")]
    public Task<ActionResult> Attend(Guid id, CancellationToken token) => Transition(id, false, token);
    [HttpPatch("{id:guid}/close"), Authorize(Policy = "ManageAlerts")]
    public Task<ActionResult> Close(Guid id, CancellationToken token) => Transition(id, true, token);
    private async Task<ActionResult> Transition(Guid id, bool close, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await service.TransitionAsync(id, userId, close, token);
        if (result.IsSuccess)
        {
            await auditWriter.RecordAsync(User, close ? AuditActions.CloseAlert : AuditActions.AttendAlert,
                "Alert", id.ToString(), close ? "Alert closed." : "Alert attended.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
            var alert = await service.GetByIdAsync(id, token);
            if (alert.IsSuccess) await realtimeWriter.PublishAsync(close ? RealtimeEventNames.AlertClosed : RealtimeEventNames.AlertAttended, alert.Value, token);
        }
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Policy = "ManageAlerts")]
    public Task<ActionResult> Resolve(Guid id, CancellationToken cancellationToken) => Transition(id, true, cancellationToken);
}
