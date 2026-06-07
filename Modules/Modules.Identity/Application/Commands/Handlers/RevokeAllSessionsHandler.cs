using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class RevokeAllSessionsHandler(
    UserManager<AppUser> userManager,
    RefreshTokenService refreshTokenService,
    ICurrentUser currentUser,
    ISecurityStampCacheService stampCache)
    : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    public async Task<Result> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.DomainUserId == currentUser.UserId, cancellationToken);

        if (user is null)
            return Result.Failure(Error.NotFound("User not found"));

        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);

        // Rotating the SecurityStamp invalidates every live access token for this user; evicting the
        // cached stamp makes the per-request check pick up the new value immediately.
        await userManager.UpdateSecurityStampAsync(user);
        await stampCache.EvictAsync(user.Id.ToString(), cancellationToken);

        return Result.Success();
    }
}
