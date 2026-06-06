using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Modules.IdentityModule.Application.Abstractions;
using Modules.IdentityModule.Infrastructure.Email;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public sealed class RequestPasswordResetHandler(
    UserManager<AppUser> userManager,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions)
    : IRequestHandler<RequestPasswordResetCommand, Result>
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emailSender.SendAsync(BuildMessage(email, token), cancellationToken);
        }

        // Always succeed to avoid account enumeration.
        return Result.Success();
    }

    private EmailMessage BuildMessage(string email, string token)
    {
        var body = string.IsNullOrWhiteSpace(_emailOptions.ResetPasswordUrl)
            ? $"Use the following token to reset your GymBro password:\n\n{token}\n\n" +
              "If you did not request this, you can ignore this email."
            : $"Reset your GymBro password using the link below:\n\n{BuildResetLink(email, token)}\n\n" +
              "If you did not request this, you can ignore this email.";

        return new EmailMessage(email, "Reset your GymBro password", body);
    }

    private string BuildResetLink(string email, string token)
    {
        var baseUrl = _emailOptions.ResetPasswordUrl!.TrimEnd('/');
        return $"{baseUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }
}
