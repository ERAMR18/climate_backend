using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Alerts.Api.Errors;

internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandledException = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(5000, nameof(LogUnhandledException)),
        "Unhandled exception while processing request {TraceId}");

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = 409;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext { HttpContext = context,
                ProblemDetails = new ProblemDetails { Status = 409, Title = "The alert changed. Reload before retrying." } });
        }
        LogUnhandledException(logger, context.TraceIdentifier, exception);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = 500,
                Title = "An unexpected error occurred.",
                Detail = "The server could not complete the request.",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            },
            Exception = exception
        });
    }
}
