using System.Text.Json.Serialization;

namespace BuildingBlocks.Shared.Errors;

/// <summary>
/// The category of an <see cref="Error"/>. Controllers map this to an HTTP status (see WebApi
/// <c>ResultActionResultExtensions</c>) <b>independently of</b> the free-form <see cref="Error.Code"/>,
/// so a machine-readable code such as <c>"DailyLog.Closed"</c> can never silently change the response
/// status the way the previous last-dot-segment convention could.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation,
    NotFound,
    Conflict,

    /// <summary>Authenticated but not permitted (membership/role denial). Maps to HTTP 403.</summary>
    Unauthorized,

    /// <summary>Authenticated but not permitted. Maps to HTTP 403.</summary>
    Forbidden,
}

/// <summary>
/// The failure value carried by <c>Result</c>/<c>Result&lt;T&gt;</c>. <see cref="Code"/> is a stable,
/// machine-readable token (e.g. <c>"DailyLog.Closed"</c>, or the category name for the bare overloads);
/// <see cref="Type"/> is what controllers translate into an HTTP status.
/// </summary>
public record Error(string Code, string Message, [property: JsonIgnore] ErrorType Type)
{
    public static readonly Error None = new("", "", ErrorType.Failure);

    // Bare overloads — the machine code defaults to the category name.
    public static Error Validation(string message) => new("Validation", message, ErrorType.Validation);
    public static Error NotFound(string message) => new("NotFound", message, ErrorType.NotFound);
    public static Error Conflict(string message) => new("Conflict", message, ErrorType.Conflict);

    // Explicit-code overloads (previously the separate CommonErrors surface).
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
