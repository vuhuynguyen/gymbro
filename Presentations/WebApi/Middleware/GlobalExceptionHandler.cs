using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Middleware;

/// <summary>Maps unhandled exceptions to a JSON body without leaking stack traces.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        logger.LogError(
            exception,
            "Unhandled exception. TraceId={TraceId} Path={Path}",
            traceId,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        var body = new ProblemDetails
        {
            Title = "Server error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred. Try again later.",
            Instance = httpContext.Request.Path
        };
        body.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}
