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

        var valid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
            return Result<TokenPair>.Failure(Error.Validation("Invalid credentials"));

        // Stamp the caller's stored zone (the domain User's authoritative TimeZoneId) into the access token so
        // handlers resolve their day boundaries server-side without a per-request parameter.
        var timeZoneId = await sender.Send(new GetUserTimeZoneQuery(user.DomainUserId), cancellationToken);
        var accessToken = tokenService.GenerateToken(user, timeZoneId);
        var refresh = await refreshTokenService.IssueAsync(user.Id, request.Ip, cancellationToken);

        return Result<TokenPair>.Success(new TokenPair(accessToken, refresh.Raw, refresh.ExpiresAtUtc));
    }
}
