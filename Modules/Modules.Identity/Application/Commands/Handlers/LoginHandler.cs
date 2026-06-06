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
    RefreshTokenService refreshTokenService)
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

        var accessToken = tokenService.GenerateToken(user);
        var refresh = await refreshTokenService.IssueAsync(user.Id, request.Ip, cancellationToken);

        return Result<TokenPair>.Success(new TokenPair(accessToken, refresh.Raw, refresh.ExpiresAtUtc));
    }
}
