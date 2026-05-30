using System.Reflection;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;

namespace BuildingBlocks.Application.Pipeline;

/// <summary>
/// Builds failed <see cref="Result"/> / <see cref="Result{T}"/> instances for MediatR pipeline behaviors
/// without throwing when the response type is a known <c>Result</c> shape.
/// </summary>
public static class ResultPipelineHelper
{
    public static bool TryCreateFailure<TResponse>(Error error, out TResponse response)
    {
        response = default!;

        var type = typeof(TResponse);

        if (type == typeof(Result))
        {
            response = (TResponse)(object)Result.Failure(error);
            return true;
        }

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Result<>))
            return false;

        var resultType = typeof(Result<>).MakeGenericType(type.GenericTypeArguments[0]);
        var failureMethod = resultType.GetMethod(
            "Failure",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(Error)]);

        if (failureMethod?.Invoke(null, [error]) is not TResponse failure)
            return false;

        response = failure;
        return true;
    }
}
