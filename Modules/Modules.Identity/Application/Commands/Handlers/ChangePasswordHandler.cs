using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class ChangePasswordHandler(
    UserManager<AppUser> userManager,
    ICurrentUser currentUser)
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

        return Result.Success();
    }
}
