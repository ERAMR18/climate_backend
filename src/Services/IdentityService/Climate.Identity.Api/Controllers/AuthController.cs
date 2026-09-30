using Climate.Identity.Api.Errors;
using Climate.Identity.Application.Users;
using Climate.Contracts.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IUserService userService, AuditWriter auditWriter) : ControllerBase
{
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken token)
    {
        await auditWriter.RecordAsync(User, AuditActions.Logout, "Authentication", User.FindFirst("sub")?.Value,
            "User logged out.", HttpContext.Connection.RemoteIpAddress?.ToString(), token);
        return NoContent();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.RegisterAsync(request, cancellationToken);
        if (result.IsSuccess) await auditWriter.RecordAsync(result.Value.Id, result.Value.Username, AuditActions.Create, "User", result.Value.Id.ToString(), "User registered.", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(UsersController.GetById), "Users", new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.LoginAsync(request, cancellationToken);
        if(result.IsSuccess) await auditWriter.RecordAsync(result.Value.User.Id,result.Value.User.Username,AuditActions.Login,"Authentication",result.Value.User.Id.ToString(),"User logged in.",HttpContext.Connection.RemoteIpAddress?.ToString(),cancellationToken);
        return this.ToActionResult(result);
    }
}
