using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public sealed class ResetPasswordHandler(
    UserManager<AppUser> userManager,
    RefreshTokenService refreshTokenService,
    IMemoryCache cache)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Result.Failure(Error.Validation("Invalid reset request."));

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(Error.Validation(message));
        }

        // Reset rotates the SecurityStamp (Identity, above) — revoke refresh tokens and drop the cached
        // stamp so a forced reset boots every existing session immediately.
        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        cache.Remove(SecurityStampCache.KeyFor(user.Id.ToString()));

        return Result.Success();
    }
}
