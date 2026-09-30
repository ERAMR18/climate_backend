using System.Security.Claims;
using Climate.Identity.Api.Errors;
using Climate.Identity.Application.Users;
using Climate.Contracts.Audit;
using Climate.Contracts.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Identity.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController(IUserService userService, AuditWriter auditWriter) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AdministratorsOnly")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken token)
    {
        if (!SystemRoles.IsDefined(request.Role)) return ValidationProblem("Invalid role.");
        var created = await userService.RegisterAsync(new(request.Username, request.Email, request.Password), token);
        if (!created.IsSuccess) return this.ToProblem(created.Error);
        var updated = await userService.UpdateAsync(created.Value.Id, new(request.Username, request.Email, request.Role), token);
        if (!updated.IsSuccess) return this.ToProblem(updated.Error);
        await auditWriter.RecordAsync(User, AuditActions.Create, "User", updated.Value.Id.ToString(), "User created by administrator.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
        return CreatedAtAction(nameof(GetById), new { id = updated.Value.Id }, updated.Value);
    }

    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken)
    {
        string? identifier = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(identifier, out Guid userId))
        {
            return Unauthorized();
        }

        var result = await userService.GetByIdAsync(userId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Policy = "AdministratorsOnly")]
    [ProducesResponseType<IReadOnlyCollection<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(
        CancellationToken cancellationToken, [FromQuery] string? search = null, [FromQuery] string? role = null, [FromQuery] bool? isActive = null) =>
        Ok(await userService.SearchAsync(search, role, isActive, cancellationToken));

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [Authorize(Policy = "AdministratorsOnly")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await userService.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdministratorsOnly")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.UpdateAsync(id, request, cancellationToken);
        if(result.IsSuccess) await auditWriter.RecordAsync(User,AuditActions.Update,"User",id.ToString(),"User updated.",HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "AdministratorsOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SetStatus(
        Guid id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.SetStatusAsync(id, request, cancellationToken);
        if(result.IsSuccess) await auditWriter.RecordAsync(User,request.IsActive ? AuditActions.Activate : AuditActions.Deactivate,"User",id.ToString(),"User status updated.",HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
        return this.ToActionResult(result);
    }
}
