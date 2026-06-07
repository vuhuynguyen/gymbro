using WebApi.Http;

namespace WebApi.Middleware;

/// <summary>
/// Enforces header/media-type API version negotiation for <c>/api</c> requests (the version is not in the URL —
/// see <see cref="ApiVersioning"/>). An explicit but unsupported version → <c>400</c>; an omitted version →
/// the default (latest). The resolved version is stashed in <see cref="HttpContext.Items"/> (so a future v2 can
/// branch on it) and echoed in the <c>X-Api-Version</c> response header. CORS preflight (OPTIONS) and non-API
/// paths (health probes, OpenAPI/Scalar) pass straight through.
/// </summary>
public sealed class ApiVersionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || !context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var requested = ApiVersioning.ReadRequestedVersion(context.Request);
        if (requested is not null && !ApiVersioning.IsSupported(requested))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Unsupported API version",
                status = StatusCodes.Status400BadRequest,
                detail =
                    $"API version '{requested}' is not supported. Supported version(s): "
                    + string.Join(", ", ApiVersioning.Supported)
                    + $". Omit the {ApiVersioning.HeaderName} header to use the latest.",
            });
            return;
        }

        var resolved = requested ?? ApiVersioning.Default;
        context.Items[ApiVersioning.HttpContextItemKey] = resolved;
        context.Response.Headers[ApiVersioning.HeaderName] = resolved;

        await next(context);
    }
}
