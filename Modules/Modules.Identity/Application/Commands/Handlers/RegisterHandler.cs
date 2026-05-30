using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class RegisterHandler(
    UserManager<AppUser> userManager,
    TokenService tokenService,
    IPublisher publisher)
    : IRequestHandler<RegisterCommand, Result<string>>
{
    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return Result<string>.Failure(Error.Conflict("Email already registered"));

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<string>.Failure(Error.Validation(message));
        }

        user.SetDomainUserId(Guid.NewGuid());
        await userManager.UpdateAsync(user);

        await publisher.Publish(
            new UserRegisteredNotification(user.DomainUserId, user.Email!, request.FullName.Trim()),
            cancellationToken);

        return Result<string>.Success(tokenService.GenerateToken(user));
    }
}
