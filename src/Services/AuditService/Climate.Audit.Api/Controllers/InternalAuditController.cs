using Climate.Audit.Api.Configuration;
using Climate.Audit.Api.Errors;
using Climate.Audit.Application.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace Climate.Audit.Api.Controllers;
[ApiController,AllowAnonymous,ApiExplorerSettings(IgnoreApi=true),Route("api/v1/internal/audit")]
public sealed class InternalAuditController(IAuditService service,IOptions<InternalApiOptions> options):ControllerBase
{
    [HttpPost] public async Task<ActionResult<AuditResponse>> Record(RecordAuditRequest request,CancellationToken ct)
    { if(!Request.Headers.TryGetValue("X-Internal-Api-Key",out var key)||!string.Equals(key.ToString(),options.Value.ApiKey,StringComparison.Ordinal))return Unauthorized();
      return this.ToActionResult(await service.RecordAsync(request,ct)); }
}
