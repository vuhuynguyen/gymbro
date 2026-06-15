using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Application.Models;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class LoginHandler(
    UserManager<AppUser> userManager,
    TokenService tokenService,
    RefreshTokenService refreshTokenService,
    ISender sender)
    : IRequestHandler<LoginCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return Result<TokenPair>.Failure(Error.Validation("Invalid credentials"));

        // A locked-out account is rejected before the password is even checked. The generic message keeps
        // the response enumeration-safe (identical to wrong-password/unknown-user). (Audit finding 8.)
        if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
            return Result<TokenPair>.Failure(Error.Validation("Invalid credentials"));

        var valid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
        {
            // Count the failure; AccessFailedAsync auto-locks once MaxFailedAccessAttempts is reached.
            if (userManager.SupportsUserLockout)
                await userManager.AccessFailedAsync(user);
            return Result<TokenPair>.Failure(Error.Validation("Invalid credentials"));
        }

        // Successful login clears the failed-attempt counter.
        if (userManager.SupportsUserLockout && await userManager.GetAccessFailedCountAsync(user) > 0)
            await userManager.ResetAccessFailedCountAsync(user);

        // Stamp the caller's stored zone (the domain User's authoritative TimeZoneId) into the access token so
        // handlers resolve their day boundaries server-side without a per-request parameter.
        var timeZoneId = await sender.Send(new GetUserTimeZoneQuery(user.DomainUserId), cancellationToken);
        var accessToken = tokenService.GenerateToken(user, timeZoneId);
        var refresh = await refreshTokenService.IssueAsync(user.Id, request.Ip, cancellationToken);

        return Result<TokenPair>.Success(new TokenPair(accessToken, refresh.Raw, refresh.ExpiresAtUtc));
    }
}
