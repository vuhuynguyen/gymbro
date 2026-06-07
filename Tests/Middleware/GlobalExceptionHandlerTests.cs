using System.Text;
using System.Text.Json;
using BuildingBlocks.Shared.DomainPrimitives;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Middleware;
using Xunit;

namespace Gymbro.Tests.Middleware;

/// <summary>
/// Locks the F5 fix: a <see cref="DomainException"/> (a business-invariant breach that slipped past
/// validation) maps to HTTP 400 with its message, while any other unhandled exception stays a 500 with
/// a generic body (no leak).
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(int status, string body)> HandleAsync(Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        await using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        bodyStream.Position = 0;
        var body = Encoding.UTF8.GetString(bodyStream.ToArray());
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task DomainException_maps_to_400_with_its_message()
    {
        var (status, body) = await HandleAsync(new DomainException("FrequencyDaysPerWeek must be 1..7."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("FrequencyDaysPerWeek must be 1..7.", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Generic_exception_maps_to_500_without_leaking_details()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("internal secret details"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("internal secret details", body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(500, doc.RootElement.GetProperty("status").GetInt32());
    }
}
