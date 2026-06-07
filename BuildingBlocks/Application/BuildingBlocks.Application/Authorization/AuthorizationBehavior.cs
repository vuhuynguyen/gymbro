using BuildingBlocks.Application.Pipeline;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace BuildingBlocks.Application.Authorization;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITenantAuthorizedRequest authorized)
            return await next();

        if (tenantContext.TenantId is null)
        {
            if (ResultPipelineHelper.TryCreateFailure<TResponse>(
                    Validation("TenantId", "X-Tenant-Id header is required."),
                    out var missingTenant))
                return missingTenant;

            throw new InvalidOperationException(
                $"Tenant authorization failed but response type {typeof(TResponse).Name} is not Result/Result<T>.");
        }

        var tenantId = tenantContext.TenantId.Value;
        if (!await tenantAuth.HasPermissionAsync(tenantId, authorized.RequiredPermission, cancellationToken))
        {
            var message = TenantPermissionMessages.GetUnauthorizedMessage(authorized.RequiredPermission);
            if (ResultPipelineHelper.TryCreateFailure<TResponse>(
                    Unauthorized("Unauthorized", message),
                    out var denied))
                return denied;

            throw new InvalidOperationException(
                $"Tenant authorization failed but response type {typeof(TResponse).Name} is not Result/Result<T>.");
        }

        return await next();
    }
}
