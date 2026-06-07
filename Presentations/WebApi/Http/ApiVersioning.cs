namespace WebApi.Http;

/// <summary>
/// Header/media-type API version negotiation — the version is deliberately <b>not</b> in the URL, so routes
/// stay clean (`/api/sessions`, not `/api/v1/sessions`) and a future breaking change never rewrites every
/// client URL or the refresh-cookie path.
///
/// <para>A client MAY pin a version with the <c>X-Api-Version</c> request header, or the <c>v</c> parameter on
/// the <c>Accept</c> media type (e.g. <c>Accept: application/json; v=1.0</c>). When omitted, the <see cref="Default"/>
/// (latest) version is served — so the co-deployed SPA carries no version literal at all. An explicit but
/// unsupported version is rejected (400) rather than silently served as latest. The resolved version is echoed
/// in the <c>X-Api-Version</c> response header for discoverability.</para>
///
/// <para>This is intentionally a thin, dependency-free layer (we ship offline and the only cached versioning
/// package is the deprecated <c>Microsoft.AspNetCore.Mvc.Versioning</c>). It is forward-compatible with adopting
/// <c>Asp.Versioning.Mvc</c> later: same header/media-type reader, same default-version semantics. A real v2 then
/// branches per resolved version (a new controller/action selected by it) — still with no URL change.</para>
/// </summary>
public static class ApiVersioning
{
    /// <summary>Request/response header carrying the API version.</summary>
    public const string HeaderName = "X-Api-Version";

    /// <summary><see cref="HttpContext.Items"/> key holding the resolved version for the current request.</summary>
    public const string HttpContextItemKey = "ResolvedApiVersion";

    /// <summary>Version served when the client does not pin one.</summary>
    public const string Default = "1.0";

    /// <summary>Versions this build can serve.</summary>
    public static readonly IReadOnlyList<string> Supported = ["1.0"];

    public static bool IsSupported(string version) =>
        Supported.Contains(version, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The version the client explicitly requested via <c>X-Api-Version</c>, else the <c>v</c> parameter of an
    /// <c>Accept</c> media type, else <c>null</c> (meaning "no preference → serve the default").
    /// </summary>
    public static string? ReadRequestedVersion(HttpRequest request)
    {
        var header = request.Headers[HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();

        foreach (var accept in request.Headers.Accept)
        {
            if (string.IsNullOrEmpty(accept))
                continue;

            foreach (var segment in accept.Split(';'))
            {
                var part = segment.Trim();
                if (part.StartsWith("v=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = part[2..].Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
        }

        return null;
    }
}
