using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Http;
using WebApi.Middleware;
using Xunit;

namespace Gymbro.Tests.Middleware;

/// <summary>
/// Locks the header-based API version negotiation (no version in the URL): omitted → default/latest,
/// explicit-but-unsupported → 400 (short-circuit), supported → pass-through, and non-/api or OPTIONS skipped.
/// </summary>
public sealed class ApiVersionMiddlewareTests
{
    private static DefaultHttpContext ApiContext(string path = "/api/sessions", string method = "GET")
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        ctx.Request.Path = path;
        ctx.Request.Method = method;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<bool> InvokeAsync(HttpContext ctx)
    {
        var nextCalled = false;
        var middleware = new ApiVersionMiddleware(_ =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(ctx);
        return nextCalled;
    }

    [Fact]
    public async Task Omitted_version_defaults_to_latest_and_stamps_response()
    {
        var ctx = ApiContext();

        Assert.True(await InvokeAsync(ctx));
        Assert.Equal(ApiVersioning.Default, ctx.Response.Headers[ApiVersioning.HeaderName].ToString());
        Assert.Equal(ApiVersioning.Default, ctx.Items[ApiVersioning.HttpContextItemKey] as string);
    }

    [Fact]
    public async Task Supported_version_header_passes_through()
    {
        var ctx = ApiContext();
        ctx.Request.Headers[ApiVersioning.HeaderName] = "1.0";

        Assert.True(await InvokeAsync(ctx));
        Assert.Equal("1.0", ctx.Response.Headers[ApiVersioning.HeaderName].ToString());
    }

    [Fact]
    public async Task Accept_media_type_v_parameter_is_honored()
    {
        var ctx = ApiContext();
        ctx.Request.Headers.Accept = "application/json; v=1.0";

        Assert.True(await InvokeAsync(ctx));
        Assert.Equal("1.0", ctx.Response.Headers[ApiVersioning.HeaderName].ToString());
    }

    [Fact]
    public async Task Unsupported_explicit_version_is_rejected_400_and_short_circuits()
    {
        var ctx = ApiContext();
        ctx.Request.Headers[ApiVersioning.HeaderName] = "9.9";

        var nextCalled = await InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Non_api_path_passes_through_even_with_a_bad_version()
    {
        var ctx = ApiContext(path: "/health");
        ctx.Request.Headers[ApiVersioning.HeaderName] = "9.9";

        Assert.True(await InvokeAsync(ctx));
        Assert.NotEqual(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Options_preflight_passes_through()
    {
        var ctx = ApiContext(method: "OPTIONS");
        ctx.Request.Headers[ApiVersioning.HeaderName] = "9.9";

        Assert.True(await InvokeAsync(ctx));
    }
}
