using Climate.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Sensors.Api.Errors;

internal static class ResultExtensions
{
    public static ActionResult ToActionResult(this ControllerBase controller, Result result) =>
        result.IsSuccess ? controller.NoContent() : controller.ToProblem(result.Error);

    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblem(result.Error);

    public static ObjectResult ToProblem(this ControllerBase controller, ApplicationError error)
    {
        int status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var details = new ProblemDetails { Status = status, Title = error.Code, Detail = error.Description };
        details.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;
        return new ObjectResult(details) { StatusCode = status };
    }
}
