using Climate.Contracts.Sensors;
using Climate.Sensors.Api.Configuration;
using Climate.Sensors.Application.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Climate.Sensors.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/internal/sensors")]
public sealed class InternalSensorsController(
    ISensorService service,
    IOptions<InternalApiOptions> options) : ControllerBase
{
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyCollection<SensorSummary>>> GetActive(
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(InternalApiOptions.HeaderName, out var suppliedKey) ||
            !string.Equals(suppliedKey.ToString(), options.Value.ApiKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        IReadOnlyCollection<SensorResponse> sensors = await service.GetAllAsync(cancellationToken);
        SensorSummary[] response = sensors
            .Where(sensor => sensor.IsActive)
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
