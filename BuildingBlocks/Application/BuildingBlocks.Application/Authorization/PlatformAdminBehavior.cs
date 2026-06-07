using BuildingBlocks.Application.Pipeline;
using BuildingBlocks.Shared.Abstractions;
using MediatR;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Enforces platform-admin access for requests implementing <see cref="IPlatformAdminRequest"/>,
/// independently of the controller-level <c>PlatformAdmin</c> policy (defense in depth).
/// Denies with the <c>"AdminOnly"</c> error (→ 403) before the handler runs.
/// </summary>
public sealed class PlatformAdminBehavior<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IPlatformAdminRequest)
            return await next();

        if (!currentUser.IsAdmin)
        {
            if (ResultPipelineHelper.TryCreateFailure<TResponse>(
                    Unauthorized("AdminOnly", "Platform admin access required."),
                    out var denied))
                return denied;

            throw new InvalidOperationException(
                $"Platform admin authorization failed but response type {typeof(TResponse).Name} is not Result/Result<T>.");
        }

        return await next();
    }
}
