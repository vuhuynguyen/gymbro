using BuildingBlocks.Shared.Errors;

namespace BuildingBlocks.Shared.Results;

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failure result");

    private Result(T value)
        : base(true, null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        _value = default;
    }

    public static Result<T> Success(T value)
        => new(value);

    public static new Result<T> Failure(Error error)
        => new(error);

    // 🔥 Optional (recommended)
    public static implicit operator Result<T>(T value)
        => Success(value);
}