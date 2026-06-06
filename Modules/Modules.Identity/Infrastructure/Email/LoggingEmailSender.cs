using Microsoft.Extensions.Logging;
using Modules.IdentityModule.Application.Abstractions;

namespace Modules.IdentityModule.Infrastructure.Email;

/// <summary>
/// Development / not-configured fallback <see cref="IEmailSender"/>. Writes the email to the log
/// instead of sending it, so local flows (e.g. password-reset) can surface the token without an
/// SMTP server. Used whenever <c>Email:SmtpHost</c> is not configured.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "[DEV EMAIL] To: {Email}\nSubject: {Subject}\n{Body}",
            message.ToEmail, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
