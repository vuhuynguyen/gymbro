using System.Diagnostics;
using BuildingBlocks.Shared.DomainPrimitives;
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
        // The request path is attacker-controlled; strip CR/LF before logging so it can't forge log lines.
        var sanitizedPath = SanitizeForLog(httpContext.Request.Path);

        // A domain-invariant breach that slipped past validation is a client error, not a server fault:
        // surface it as 400 with the (safe, author-written) invariant message instead of a generic 500.
        // Handlers still prefer Result failures for expected rules; this is the defensive backstop.
        if (exception is DomainException domainException)
        {
            logger.LogInformation(
                "Domain invariant violated. TraceId={TraceId} Path={Path} Reason={Reason}",
                traceId,
                sanitizedPath,
                domainException.Message);

            await WriteProblemAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Invalid request",
                domainException.Message,
                traceId,
                cancellationToken);
            return true;
        }

        logger.LogError(
            exception,
            "Unhandled exception. TraceId={TraceId} Path={Path}",
            traceId,
            sanitizedPath);

        await WriteProblemAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "Server error",
            "An unexpected error occurred. Try again later.",
            traceId,
            cancellationToken);
        return true;
    }

    private static string SanitizeForLog(PathString path) =>
        path.ToString().Replace("\r", string.Empty).Replace("\n", string.Empty);

    private static async Task WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string traceId,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var body = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        body.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
    }
}
