using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.IdentityModule.Application.Abstractions;

namespace Modules.IdentityModule.Infrastructure.Email;

/// <summary>
/// SMTP-backed <see cref="IEmailSender"/>. Registered only when <c>Email:SmtpHost</c> is configured.
/// Delivery failures are logged and swallowed so callers (e.g. password-reset) keep their
/// enumeration-safe success response.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient(_options.SmtpHost!, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);

            using var mail = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false,
            };
            mail.To.Add(message.ToEmail);

            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception ex)
        {
            // Never surface delivery failures to the caller; just record them.
            logger.LogError(ex, "Failed to send email to {Email} (subject: {Subject}).",
                message.ToEmail, message.Subject);
        }
    }
}
