using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Modules.IdentityModule.Application.Models;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class RegisterHandler(
    UserManager<AppUser> userManager,
    TokenService tokenService,
    RefreshTokenService refreshTokenService,
    IPublisher publisher,
    ICrossStoreTransaction crossStoreTransaction)
    : IRequestHandler<RegisterCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return Result<TokenPair>.Failure(Error.Conflict("Email already registered"));

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        // The Identity AppUser (DB3) and the domain User + Tenant + Owner role (DB1, written by the
        // UserRegisteredNotification handler) must land together — otherwise a failure after CreateAsync
        // leaves a login with no workspace. One cross-store transaction makes both commit or roll back;
        // any throw here disposes the scope uncommitted, reverting the AppUser too.
        await using var transaction = await crossStoreTransaction.BeginAsync(cancellationToken);

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<TokenPair>.Failure(Error.Validation(message));
        }

        user.SetDomainUserId(Guid.NewGuid());
        await userManager.UpdateAsync(user);

        await publisher.Publish(
            new UserRegisteredNotification(user.DomainUserId, user.Email!, request.FullName.Trim()),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var accessToken = tokenService.GenerateToken(user);
        var refresh = await refreshTokenService.IssueAsync(user.Id, request.Ip, cancellationToken);

        return Result<TokenPair>.Success(new TokenPair(accessToken, refresh.Raw, refresh.ExpiresAtUtc));
    }
}
