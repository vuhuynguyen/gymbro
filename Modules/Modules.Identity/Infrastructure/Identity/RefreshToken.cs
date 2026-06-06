namespace Modules.IdentityModule.Infrastructure.Identity;

/// <summary>
/// A rotating refresh token. The raw value is never stored — only its SHA-256 hash.
/// Tokens are chained by <see cref="FamilyId"/>: every rotation issues a new token in the
/// same family and marks the old one replaced. Presenting an already-replaced token is treated
/// as theft and revokes the whole family (reuse detection).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }

    /// <summary>The AppUser (Identity) id this token belongs to — matches the JWT <c>sub</c> claim.</summary>
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hash (base64) of the raw token. The raw value only ever exists in the cookie.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>Rotation lineage. All tokens descended from one login share a family.</summary>
    public Guid FamilyId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>Set when this token is rotated; its presence marks the token as "spent".</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public string? CreatedByIp { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTime nowUtc,
        TimeSpan lifetime,
        string? createdByIp)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(lifetime),
            CreatedByIp = createdByIp
        };

    public bool IsActive(DateTime nowUtc)
        => RevokedAtUtc is null && ReplacedByTokenId is null && nowUtc < ExpiresAtUtc;

    /// <summary>True once the token has been rotated — the signal used for reuse detection.</summary>
    public bool IsSpent => ReplacedByTokenId is not null;

    public void MarkReplacedBy(Guid newTokenId, DateTime nowUtc)
    {
        ReplacedByTokenId = newTokenId;
        RevokedAtUtc = nowUtc;
    }

    public void Revoke(DateTime nowUtc)
    {
        if (RevokedAtUtc is null)
            RevokedAtUtc = nowUtc;
    }
}
