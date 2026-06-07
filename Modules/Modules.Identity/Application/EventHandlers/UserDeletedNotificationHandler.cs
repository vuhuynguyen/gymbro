using BuildingBlocks.Application.Messaging;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Modules.IdentityModule.Application.EventHandlers;

/// <summary>
/// Deletes the Identity <see cref="AppUser"/> when the matching domain User is removed
/// (the two live in separate stores with no FK — DB3). Runs inside the caller's cross-store
/// transaction, so a missing AppUser stays an idempotent no-op, but a failed delete throws to roll
/// the whole cross-store delete back rather than silently orphaning the AppUser.
/// </summary>
public sealed class UserDeletedNotificationHandler : INotificationHandler<UserDeletedNotification>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ISecurityStampCacheService _stampCache;
    private readonly ILogger<UserDeletedNotificationHandler> _logger;

    public UserDeletedNotificationHandler(
        UserManager<AppUser> userManager,
        ISecurityStampCacheService stampCache,
        ILogger<UserDeletedNotificationHandler> logger)
    {
        _userManager = userManager;
        _stampCache = stampCache;
        _logger = logger;
    }

    public async Task Handle(UserDeletedNotification notification, CancellationToken cancellationToken)
    {
        var appUser = _userManager.Users
            .FirstOrDefault(u => u.DomainUserId == notification.DomainUserId);

        if (appUser is null)
        {
            // Already gone (or never existed) — idempotent no-op.
            _logger.LogInformation(
                "No Identity AppUser found for domain user {DomainUserId}; nothing to delete",
                notification.DomainUserId);
            return;
        }

        var appUserId = appUser.Id.ToString();
        var result = await _userManager.DeleteAsync(appUser);
        if (!result.Succeeded)
        {
            // Throw so the caller's cross-store transaction rolls back the domain delete too —
            // better to fail the whole operation loudly than commit a half-deleted user.
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError(
                "Failed to delete Identity AppUser for domain user {DomainUserId}: {Errors}",
                notification.DomainUserId, errors);
            throw new InvalidOperationException(
                $"Failed to delete Identity AppUser for domain user {notification.DomainUserId}: {errors}");
        }

        // Drop any cached stamp so a deleted user cannot authenticate until the TTL would have lapsed.
        await _stampCache.EvictAsync(appUserId, cancellationToken);

        _logger.LogInformation(
            "Deleted Identity AppUser for domain user {DomainUserId}", notification.DomainUserId);
    }
}
