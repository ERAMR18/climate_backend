using Climate.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;
namespace Climate.Audit.Api.Errors;
internal static class ResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ControllerBase c,Result<T> r)
    { if(r.IsSuccess)return c.Ok(r.Value); int s=r.Error.Type==ErrorType.NotFound?404:422;
      return new ObjectResult(new ProblemDetails{Status=s,Title=r.Error.Code,Detail=r.Error.Description,Extensions={{"traceId",c.HttpContext.TraceIdentifier}}}){StatusCode=s}; }
}
