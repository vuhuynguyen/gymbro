namespace BuildingBlocks.Shared.Errors;

public static class CommonErrors
{
    public static Error Validation(string code, string message)
        => new(code, message);

    public static Error NotFound(string code, string message)
        => new(code, message);

    public static Error Unauthorized(string code, string message)
        => new(code, message);

    // Membership/role denial (authenticated but not allowed). Maps to HTTP 403 via the
    // "Forbidden" suffix in ResultActionResultExtensions.
    public static Error Forbidden(string code, string message)
        => new(code, message);

    public static Error Conflict(string code, string message)
        => new(code, message);
}