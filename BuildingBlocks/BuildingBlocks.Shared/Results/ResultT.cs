using BuildingBlocks.Shared.Errors;

namespace BuildingBlocks.Shared.Results;

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value)
        : base(true, Error.None)
    {
        Value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        Value = default;
    }

    public static Result<T> Success(T value)
        => new(value);

    public static new Result<T> Failure(Error error)
        => new(error);
}