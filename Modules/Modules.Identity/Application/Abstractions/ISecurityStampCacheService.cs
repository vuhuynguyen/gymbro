namespace Modules.IdentityModule.Application.Abstractions;

/// <summary>
/// Tier-2 JWT revocation: compares the token's <c>stamp</c> claim against the user's current
/// SecurityStamp, with a short distributed-cache window to avoid a DB hit on every request.
/// </summary>
public interface ISecurityStampCacheService
{
    Task<bool> IsStampValidAsync(
        string appUserId,
        string tokenStamp,
        CancellationToken cancellationToken = default);

    Task EvictAsync(string appUserId, CancellationToken cancellationToken = default);
}
