using Microsoft.Extensions.Logging;
using Modules.IdentityModule.Application.Abstractions;

namespace Modules.IdentityModule.Infrastructure.Email;

/// <summary>
/// Development-only fallback <see cref="IEmailSender"/>. Records that an email would have been sent
/// (masked recipient + subject) without an SMTP server. The body is never logged — it can contain
/// secrets such as a password-reset token — so only non-sensitive metadata is written. Wired up only
/// in Development when <c>Email:SmtpHost</c> is unset; non-Development hosts get <see cref="NoOpEmailSender"/>.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "[DEV EMAIL] To: {Email} Subject: {Subject} Body: [REDACTED]",
            EmailMasking.Mask(message.ToEmail), message.Subject);
        return Task.CompletedTask;
    }
}
