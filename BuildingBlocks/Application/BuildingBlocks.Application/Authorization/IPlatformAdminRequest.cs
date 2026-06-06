namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Marks a MediatR request whose handler requires the caller to be a platform admin.
/// Enforced by <see cref="PlatformAdminBehavior{TRequest,TResponse}"/> before the handler runs —
/// independently of the controller-level <c>PlatformAdmin</c> policy (defense in depth).
/// </summary>
public interface IPlatformAdminRequest
{
}
