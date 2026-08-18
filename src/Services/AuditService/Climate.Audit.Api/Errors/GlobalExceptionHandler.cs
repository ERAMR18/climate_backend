using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace Climate.Audit.Api.Errors;
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger):IExceptionHandler
{
    private static readonly Action<ILogger,string,Exception?> LogUnhandled=LoggerMessage.Define<string>(LogLevel.Error,new EventId(5000,nameof(LogUnhandled)),"Unhandled Audit Service error. TraceId: {TraceId}");
    public async ValueTask<bool> TryHandleAsync(HttpContext c,Exception e,CancellationToken ct)
    { LogUnhandled(logger,c.TraceIdentifier,e); c.Response.StatusCode=500; await c.Response.WriteAsJsonAsync(new ProblemDetails{Status=500,Title="audit.unexpected_error",Detail="An unexpected error occurred.",Extensions={{"traceId",c.TraceIdentifier}}},ct); return true; }
}
