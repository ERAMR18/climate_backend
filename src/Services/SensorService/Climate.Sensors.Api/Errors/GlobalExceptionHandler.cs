using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Climate.Sensors.Api.Errors;

internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandledException = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(5000, nameof(LogUnhandledException)),
        "Unhandled exception while processing request {TraceId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, httpContext.TraceIdentifier, exception);
        int status = exception is HttpRequestException or TaskCanceledException
            ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status == 503 ? "Community dependency unavailable." : "An unexpected error occurred.",
                Detail = "The server could not complete the request.",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            },
            Exception = exception
        });
    }
}
