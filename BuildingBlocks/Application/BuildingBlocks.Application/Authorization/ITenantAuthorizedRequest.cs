using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Marks a MediatR request whose handler requires a validated tenant context and a single static permission.
/// Enforced by <see cref="AuthorizationBehavior{TRequest,TResponse}"/> before the handler runs.
/// </summary>
public interface ITenantAuthorizedRequest
{
    Permission RequiredPermission { get; }
}
