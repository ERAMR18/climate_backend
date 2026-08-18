using Climate.Monitoring.Api.Errors;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Application.Simulation;
using Climate.Contracts.Audit;
using Climate.Contracts.Realtime;
using Climate.Monitoring.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Monitoring.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/monitoring")]
public sealed class MonitoringController(
    IMonitoringService monitoringService,
    ISimulationService simulationService,
    AuditWriter auditWriter,
    IRealtimePublisher realtimePublisher) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<IReadOnlyCollection<SensorReadingResponse>>> GetCurrent(
        CancellationToken cancellationToken) =>
        Ok(await monitoringService.GetCurrentAsync(cancellationToken));

    [HttpGet("sensors/{sensorId:guid}/latest")]
    public async Task<ActionResult<SensorReadingResponse>> GetLatest(
        Guid sensorId,
        CancellationToken cancellationToken)
    {
        var result = await monitoringService.GetLatestAsync(sensorId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("sensors/{sensorId:guid}/history")]
    public async Task<ActionResult<IReadOnlyCollection<SensorReadingResponse>>> GetHistory(
        Guid sensorId,
        [FromQuery] Guid? communityId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var result = await monitoringService.GetHistoryAsync(
            sensorId,
            new HistoryFilter(communityId, from, to),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("sensors/{sensorId:guid}/chart")]
    public async Task<ActionResult<SensorChartResponse>> GetChart(
        Guid sensorId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string interval = "5m",
        CancellationToken cancellationToken = default)
    {
        var result = await monitoringService.GetChartAsync(sensorId, from, to, interval, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("readings")]
    [Authorize(Policy = "OperateSimulation")]
    public async Task<ActionResult<SensorReadingResponse>> CreateReading(
        CreateReadingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await monitoringService.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetLatest), new { sensorId = result.Value.SensorId }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpGet("simulation/status")]
    public ActionResult<SimulationStatusResponse> GetSimulationStatus() => Ok(simulationService.GetStatus());

    [HttpPost("simulation/start")]
    [Authorize(Policy = "OperateSimulation")]
    public async Task<ActionResult<SimulationStatusResponse>> Start(CancellationToken cancellationToken)
    { SimulationStatusResponse status=simulationService.Start(); await AuditAsync(AuditActions.StartSimulation,"Simulation started.",cancellationToken); return Ok(status); }

    [HttpPost("simulation/stop")]
    [Authorize(Policy = "OperateSimulation")]
    public async Task<ActionResult<SimulationStatusResponse>> Stop(CancellationToken cancellationToken)
    { SimulationStatusResponse status=simulationService.StopSimulation(); await AuditAsync(AuditActions.StopSimulation,"Simulation stopped.",cancellationToken); return Ok(status); }

    [HttpPost("simulation/reset")]
    [Authorize(Policy = "ResetSystem")]
    public async Task<ActionResult<SimulationStatusResponse>> Reset(CancellationToken cancellationToken)
    { SimulationStatusResponse status=await simulationService.ResetAsync(cancellationToken); await AuditAsync(AuditActions.ResetSystem,"Simulation reset.",cancellationToken); await realtimePublisher.PublishAsync(RealtimeEventNames.SystemReset,status,cancellationToken); return Ok(status); }

    [HttpPost("system/reset")]
    [Authorize(Policy = "ResetSystem")]
    public async Task<ActionResult<SimulationStatusResponse>> ResetSystem(CancellationToken cancellationToken)
    { SimulationStatusResponse status=await simulationService.ResetAsync(cancellationToken); await AuditAsync(AuditActions.ResetSystem,"System operational state reset.",cancellationToken); await realtimePublisher.PublishAsync(RealtimeEventNames.SystemReset,status,cancellationToken); return Ok(status); }

    private Task AuditAsync(string action,string description,CancellationToken cancellationToken)=>
        auditWriter.RecordAsync(User,action,"Simulation",null,description,HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
}
