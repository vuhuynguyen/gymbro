namespace Modules.IdentityModule.Infrastructure.Email;

/// <summary>
/// Masks an email address for safe logging: keeps the first character and the domain, hiding the rest
/// of the local part (e.g. <c>jane@example.com</c> → <c>j***@example.com</c>). Used so operational logs
/// don't carry full recipient addresses.
/// </summary>
internal static class EmailMasking
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "***";

        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
            return "***";

        return $"{email[0]}***{email[at..]}";
    }
}
