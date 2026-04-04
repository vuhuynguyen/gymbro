using System.Reflection;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count == 0)
            return await next();

        return TryResultFailure(failures, out var resultResponse) 
            ? resultResponse
            : throw new ValidationException(failures);
    }

    private static bool TryResultFailure(
        IReadOnlyList<ValidationFailure> failures,
        out TResponse response)
    {
        response = default!;

        var type = typeof(TResponse);
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Result<>))
            return false;

        var message = string.Join("; ", failures.Select(f => f.ErrorMessage));
        var valueType = type.GenericTypeArguments[0];
        var resultType = typeof(Result<>).MakeGenericType(valueType);
        var failureMethod = resultType.GetMethod(
            "Failure",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(Error)]);

        if (failureMethod?.Invoke(null, [Error.Validation(message)]) is not TResponse r)
            return false;

        response = r;
        return true;
    }
}
