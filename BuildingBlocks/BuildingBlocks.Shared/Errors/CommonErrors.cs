namespace BuildingBlocks.Shared.Errors;

public static class CommonErrors
{
    public static Error Validation(string code, string message)
        => new(code, message);

    public static Error NotFound(string code, string message)
        => new(code, message);

    public static Error Unauthorized(string code, string message)
        => new(code, message);

    public static Error Conflict(string code, string message)
        => new(code, message);
}