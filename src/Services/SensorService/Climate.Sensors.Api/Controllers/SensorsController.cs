using Climate.Sensors.Api.Errors;
using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Domain.Sensors;
using Climate.Sensors.Application.Sensors;
using Climate.Contracts.Audit;
using Climate.Contracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Sensors.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/sensors")]
public sealed class SensorsController(ISensorService service, AuditWriter auditWriter,RealtimeWriter realtimeWriter) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SensorResponse>>> GetAll(
        [FromServices] ISensorCatalogRepository repository, CancellationToken cancellationToken,
        [FromQuery] Guid? communityId = null, [FromQuery] SensorType? type = null, [FromQuery] bool? isActive = null,
        [FromQuery] string? code = null, [FromQuery] string? search = null) =>
        Ok((await repository.SearchSensorsAsync(communityId, type, isActive, code, search, cancellationToken)).Select(x => SensorResponse.FromEntity(x)).ToArray());

    [HttpGet("{id:guid}", Name = "GetSensorById")]
    public async Task<ActionResult<SensorResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "ManageSensors")]
    public async Task<ActionResult<SensorResponse>> Create(
        CreateSensorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess) return this.ToProblem(result.Error);
        await auditWriter.RecordAsync(User,AuditActions.Create,"Sensor",result.Value.Id.ToString(),"Sensor created.",HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManageSensors")]
    public async Task<ActionResult<SensorResponse>> Update(
        Guid id,
        UpdateSensorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        if(result.IsSuccess) await auditWriter.RecordAsync(User,AuditActions.Update,"Sensor",id.ToString(),"Sensor updated.",HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = "ManageSensors")]
    public Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, true, cancellationToken);

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = "ManageSensors")]
    public Task<ActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, false, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManageSensors")]
    public Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, false, cancellationToken, AuditActions.Delete);

    private async Task<ActionResult> ChangeStatus(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken, string? auditAction = null)
    {
        var result = await service.SetStatusAsync(id, isActive, cancellationToken);
        if(result.IsSuccess) await auditWriter.RecordAsync(User,auditAction ?? (isActive?AuditActions.Activate:AuditActions.Deactivate),"Sensor",id.ToString(),isActive?"Sensor activated.":"Sensor deactivated.",HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
        if(result.IsSuccess) await realtimeWriter.PublishAsync(RealtimeEventNames.SensorStatusChanged,new{id,isActive},cancellationToken);
        return this.ToActionResult(result);
    }
}
