using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Events.Api.Errors;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandled = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(5000, nameof(LogUnhandled)), "Unhandled Event Service error. TraceId: {TraceId}");

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        LogUnhandled(logger, context.TraceIdentifier, exception);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 500, Title = "event.unexpected_error",
            Detail = "An unexpected error occurred.", Extensions = { ["traceId"] = context.TraceIdentifier } }, token);
        return true;
    }
}
