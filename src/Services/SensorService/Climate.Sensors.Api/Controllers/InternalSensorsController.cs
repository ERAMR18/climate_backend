using Climate.Contracts.Sensors;
using Climate.Sensors.Api.Configuration;
using Climate.Sensors.Application.Sensors;
using Climate.Sensors.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Climate.Sensors.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/internal/sensors")]
public sealed class InternalSensorsController(
    ISensorCatalogRepository repository,
    IOptions<InternalApiOptions> options) : ControllerBase
{
    [HttpGet("{id:guid}/active")]
    public async Task<ActionResult<bool>> IsActive(Guid id, [FromServices] ISensorCatalogRepository repository, CancellationToken token)
    {
        if (!Request.Headers.TryGetValue(InternalApiOptions.HeaderName, out var key) || key.ToString() != options.Value.ApiKey) return Unauthorized();
        var sensor = await repository.GetSensorAsync(id, token);
        return Ok(sensor is { IsActive: true } && sensor.Community.IsActive);
    }

    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyCollection<SensorSummary>>> GetActive(
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(InternalApiOptions.HeaderName, out var suppliedKey) ||
            !string.Equals(suppliedKey.ToString(), options.Value.ApiKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var sensors = await repository.SearchSensorsAsync(null, null, true, null, null, cancellationToken);
        SensorSummary[] response = sensors
            .Where(sensor => sensor.IsActive && sensor.Community.IsActive)
            .Select(sensor => new SensorSummary(
                sensor.Id,
                sensor.CommunityId,
                sensor.Name,
                sensor.Code,
                (Climate.Contracts.Sensors.SensorType)sensor.Type,
                sensor.Unit,
                sensor.IsActive))
            .ToArray();
        return Ok(response);
    }
}
