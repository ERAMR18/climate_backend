using Climate.Alerts.Api.Configuration;
using Climate.Alerts.Api.Errors;
using Climate.Alerts.Application.Alerts;
using Climate.Contracts.Monitoring;
using Climate.Contracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Climate.Alerts.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/internal/risk-evaluations")]
public sealed class InternalEvaluationController(
    IAlertService service,
    RealtimeWriter realtimeWriter,
    IOptions<InternalApiOptions> options) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IReadOnlyCollection<AlertResponse>>> Evaluate(
        SensorReadingRecorded reading,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(InternalApiOptions.HeaderName, out var suppliedKey) ||
            !string.Equals(suppliedKey.ToString(), options.Value.ApiKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var result = await service.EvaluateAsync(reading, cancellationToken);
        if(result.IsSuccess)
            foreach(AlertResponse alert in result.Value)
                await realtimeWriter.PublishAsync(RealtimeEventNames.AlertGenerated,alert,cancellationToken);
        return this.ToActionResult(result);
    }
}
