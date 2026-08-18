using Climate.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Events.Api.Errors;

internal static class ResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess) return controller.Ok(result.Value);
        int status = result.Error.Type == ErrorType.NotFound ? 404 : 422;
        return new ObjectResult(new ProblemDetails { Status = status, Title = result.Error.Code,
            Detail = result.Error.Description, Extensions = { ["traceId"] = controller.HttpContext.TraceIdentifier } }) { StatusCode = status };
    }
}
