namespace BuildingBlocks.Shared.Errors;

public record Error(string Code, string Message)
{
    public static Error None => new("", "");

    public static Error Validation(string message)
        => new("Validation", message);

    public static Error NotFound(string message)
        => new("NotFound", message);

    public static Error Conflict(string message)
        => new("Conflict", message);
}