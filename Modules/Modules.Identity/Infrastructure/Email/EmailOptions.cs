namespace Modules.IdentityModule.Infrastructure.Email;

/// <summary>
/// Bound from the <c>Email</c> configuration section. When <see cref="SmtpHost"/> is empty the
/// composition root falls back to the dev logging sender, so local runs need no SMTP server.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@gymbro.local";
    public string FromDisplayName { get; set; } = "GymBro";

    /// <summary>Base URL of the portal's reset-password page; the token + email are appended as query params.</summary>
    public string? ResetPasswordUrl { get; set; }

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}
