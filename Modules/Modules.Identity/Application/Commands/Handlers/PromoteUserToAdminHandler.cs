using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class PromoteUserToAdminHandler(
    UserManager<AppUser> userManager,
    ICurrentUser currentUser)
    : IRequestHandler<PromoteUserToAdminCommand, Result>
{
    public async Task<Result> Handle(PromoteUserToAdminCommand request, CancellationToken cancellationToken)
    {
        if (AdminPolicy.Deny(currentUser) is { } denied)
            return denied;

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Result.Failure(Error.NotFound("User not found."));

        user.SetPlatformAdmin(request.IsAdmin);
        await userManager.UpdateAsync(user);

        return Result.Success();
    }
}
