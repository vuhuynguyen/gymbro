using BuildingBlocks.Application.Messaging;
using Modules.IdentityModule.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Modules.IdentityModule.Application.EventHandlers;

/// <summary>
/// Deletes the Identity <see cref="AppUser"/> when the matching domain User is removed
/// (the two live in separate stores with no FK — DB3). Idempotent and resilient: a missing
/// AppUser is treated as a no-op so the originating delete is never rolled back.
/// </summary>
public sealed class UserDeletedNotificationHandler : INotificationHandler<UserDeletedNotification>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<UserDeletedNotificationHandler> _logger;

    public UserDeletedNotificationHandler(
        UserManager<AppUser> userManager,
        ILogger<UserDeletedNotificationHandler> logger)
    {
        _userManager = userManager;
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

        var result = await _userManager.DeleteAsync(appUser);
        if (!result.Succeeded)
        {
            // Don't throw — the domain user is already deleted; surface for ops follow-up.
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError(
                "Failed to delete Identity AppUser for domain user {DomainUserId}: {Errors}",
                notification.DomainUserId, errors);
            return;
        }

        _logger.LogInformation(
            "Deleted Identity AppUser for domain user {DomainUserId}", notification.DomainUserId);
    }
}
