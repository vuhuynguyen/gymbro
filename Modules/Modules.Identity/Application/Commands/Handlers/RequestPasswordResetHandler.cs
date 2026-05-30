using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public sealed class RequestPasswordResetHandler(
    UserManager<AppUser> userManager,
    IHostEnvironment environment,
    ILogger<RequestPasswordResetHandler> logger)
    : IRequestHandler<RequestPasswordResetCommand, Result>
{
    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Development password-reset token for {Email}: {Token}",
                    request.Email,
                    token);
            }
            // Production: send token via email provider (not wired).
        }

        // Always succeed to avoid account enumeration.
        return Result.Success();
    }
}
