using Climate.Audit.Api.Errors;
using Climate.Audit.Application.Auditing;
using Climate.Contracts.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Climate.Audit.Api.Controllers;
[ApiController,Authorize(Roles=SystemRoles.Administrator),Route("api/v1/audit")]
public sealed class AuditController(IAuditService service):ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyCollection<AuditResponse>>> List([FromQuery]Guid? userId,[FromQuery]string? action,[FromQuery]string? resource,[FromQuery]DateTimeOffset? from,[FromQuery]DateTimeOffset? to,CancellationToken ct)
    { if(from>to)return ValidationProblem("'from' must be before or equal to 'to'."); return Ok(await service.ListAsync(new(userId,action,resource,from,to),ct)); }
    [HttpGet("{id:guid}")] public async Task<ActionResult<AuditResponse>> Get(Guid id,CancellationToken ct)=>this.ToActionResult(await service.GetAsync(id,ct));
}
