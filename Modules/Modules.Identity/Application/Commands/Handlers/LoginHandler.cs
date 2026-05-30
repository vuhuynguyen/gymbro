using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class LoginHandler(
    UserManager<AppUser> userManager,
    TokenService tokenService)
    : IRequestHandler<LoginCommand, Result<string>>
{
    public async Task<Result<string>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        
        if (user == null)
            return Result<string>.Failure(Error.Validation("Invalid credentials"));

        var valid = await userManager.CheckPasswordAsync(user, request.Password);
        
        return !valid ? Result<string>.Failure(Error.Validation("Invalid credentials")) : Result<string>.Success(tokenService.GenerateToken(user));
    }
}
