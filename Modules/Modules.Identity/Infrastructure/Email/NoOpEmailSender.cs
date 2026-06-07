using Microsoft.Extensions.Logging;
using Modules.IdentityModule.Application.Abstractions;

namespace Modules.IdentityModule.Infrastructure.Email;

/// <summary>
/// Fallback <see cref="IEmailSender"/> used in non-Development hosts when no SMTP is configured. Drops
/// the message and logs a misconfiguration warning — it never writes the body, so secret-bearing content
/// (e.g. a password-reset token) cannot leak to logs. This keeps the verbose dev <see cref="LoggingEmailSender"/>
/// strictly Development-only.
/// </summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email not sent: no SMTP is configured outside Development. Subject: {Subject}",
            message.Subject);
        return Task.CompletedTask;
    }
}
