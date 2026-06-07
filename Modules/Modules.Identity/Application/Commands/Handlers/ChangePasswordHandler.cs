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

public class ChangePasswordHandler(
    UserManager<AppUser> userManager,
    RefreshTokenService refreshTokenService,
    ICurrentUser currentUser,
    ISecurityStampCacheService stampCache)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.DomainUserId == currentUser.UserId, cancellationToken);

        if (user is null)
            return Result.Failure(Error.NotFound("User not found"));

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(Error.Validation(message));
        }

        // A password change rotates the SecurityStamp (done by Identity above); kill all other sessions
        // too so a stolen refresh token can't outlive the password it was issued under.
        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        await stampCache.EvictAsync(user.Id.ToString(), cancellationToken);

        return Result.Success();
    }
}
