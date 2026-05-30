using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.UserModule.Entities;

public class Invite : AggregateRoot
{
    // Unambiguous chars — no 0/O/1/I confusion
    private static readonly char[] CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public string? Email { get; private set; }
    public string Code { get; private set; } = null!;
    public TenantRole Role { get; private set; }
    public DateTimeOffset ExpiredAt { get; private set; }
    public bool IsUsed { get; private set; }

    // TenantId is inherited from BaseEntity (Guid?) — represents the tenant this invite belongs to

    private Invite() { }

    public static Invite Create(string email, Guid tenantId, TenantRole role, DateTimeOffset expiredAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (expiredAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("ExpiredAt must be in the future.", nameof(expiredAt));

        return new Invite
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            Code = GenerateCode(),
            TenantId = tenantId,
            Role = role,
            ExpiredAt = expiredAt,
            IsUsed = false
        };
    }

    public bool IsValid() => !IsUsed && ExpiredAt > DateTimeOffset.UtcNow;

    public static Invite CreateForTenant(Guid tenantId, DateTimeOffset expiredAt)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (expiredAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("ExpiredAt must be in the future.", nameof(expiredAt));

        return new Invite
        {
            Id = Guid.NewGuid(),
            Code = GenerateCode(),
            TenantId = tenantId,
            Role = TenantRole.Client,
            ExpiredAt = expiredAt,
            IsUsed = false
        };
    }

    public void MarkUsed() => IsUsed = true;

    private static string GenerateCode()
    {
        // Cryptographically secure RNG — invite codes are bearer secrets, so a predictable
        // PRNG (Random.Shared) would make them guessable.
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = CodeChars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(CodeChars.Length)];
        return new string(chars);
    }
}
