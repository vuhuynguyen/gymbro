using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class PromoteUserToAdminHandler(
    UserManager<AppUser> userManager,
    ISecurityStampCacheService stampCache)
    : IRequestHandler<PromoteUserToAdminCommand, Result>
{
    public async Task<Result> Handle(PromoteUserToAdminCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Result.Failure(Error.NotFound("User not found."));

        var changed = user.IsPlatformAdmin != request.IsAdmin;
        user.SetPlatformAdmin(request.IsAdmin);
        await userManager.UpdateAsync(user);

        // `is_admin` is carried in the access token and gates BOTH the admin EF-filter bypass and
        // PlatformAdminBehavior. Without rotating the SecurityStamp, a demotion would
        // not take effect until the user's 15-minute access token expired — leaving a former admin with
        // full cross-tenant access in the interim. Rotate + evict (the RevokeAllSessions pattern) so the
        // per-request stamp check (Program.cs OnTokenValidated) revokes every live token on its next
        // request. Only on an actual change, so re-affirming a user's current status doesn't sign them out.
        if (changed)
        {
            await userManager.UpdateSecurityStampAsync(user);
            await stampCache.EvictAsync(user.Id.ToString(), cancellationToken);
        }

        return Result.Success();
    }
}
