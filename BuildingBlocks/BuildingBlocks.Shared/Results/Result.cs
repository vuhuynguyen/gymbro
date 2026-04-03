namespace BuildingBlocks.Shared.Results;

using BuildingBlocks.Shared.Errors;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new ArgumentException("Success result cannot have error");

        if (!isSuccess && error is null)
            throw new ArgumentException("Failure result must have error");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new(true, null);

    public static Result Failure(Error error)
        => new(false, error);
}