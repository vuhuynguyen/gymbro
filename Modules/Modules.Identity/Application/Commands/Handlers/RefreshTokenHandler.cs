using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Application.Models;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class RefreshTokenHandler(
    UserManager<AppUser> userManager,
    TokenService tokenService,
    RefreshTokenService refreshTokenService)
    : IRequestHandler<RefreshTokenCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var validation = await refreshTokenService.ValidateAsync(request.RawToken, cancellationToken);
        if (validation.IsFailure)
            return Result<TokenPair>.Failure(validation.Error);

        var token = validation.Value!;
        var user = await userManager.FindByIdAsync(token.UserId.ToString());
        if (user is null)
            return Result<TokenPair>.Failure(Error.Validation("Invalid refresh token."));

        var rotated = await refreshTokenService.RotateAsync(token, request.Ip, cancellationToken);
        var accessToken = tokenService.GenerateToken(user);

        return Result<TokenPair>.Success(new TokenPair(accessToken, rotated.Raw, rotated.ExpiresAtUtc));
    }
}
