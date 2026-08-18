using Climate.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Alerts.Api.Errors;

internal static class ResultExtensions
{
    public static ActionResult ToActionResult(this ControllerBase controller, Result result) =>
        result.IsSuccess ? controller.NoContent() : controller.ToProblem(result.Error);

    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblem(result.Error);

    private static ObjectResult ToProblem(this ControllerBase controller, ApplicationError error)
    {
        int status = error.Type switch
        {
            ErrorType.Validation => 422,
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            _ => 500
        };
        var details = new ProblemDetails { Status = status, Title = error.Code, Detail = error.Description };
        details.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;
        return new ObjectResult(details) { StatusCode = status };
    }
}
