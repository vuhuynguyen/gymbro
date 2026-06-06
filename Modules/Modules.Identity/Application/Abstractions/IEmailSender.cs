namespace Modules.IdentityModule.Application.Abstractions;

/// <summary>
/// Outbound transactional email. The concrete provider is chosen at composition time
/// (SMTP when configured, otherwise a dev logger). Implementations must not throw for
/// delivery failures that should stay invisible to the caller — e.g. password-reset, which
/// always returns success to avoid account enumeration.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(string ToEmail, string Subject, string Body);
