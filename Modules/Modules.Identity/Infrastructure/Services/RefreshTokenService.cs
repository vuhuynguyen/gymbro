using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Modules.IdentityModule.Infrastructure.Identity;

namespace Modules.IdentityModule.Infrastructure.Services;

/// <summary>A freshly issued refresh token. The raw value is returned exactly once, to be sent as a cookie.</summary>
public sealed record IssuedRefreshToken(string Raw, DateTime ExpiresAtUtc);

/// <summary>
/// Owns the lifecycle of opaque rotating refresh tokens: issue, validate (with reuse detection),
/// rotate, and revoke. Only hashes are persisted — the raw token is never stored.
/// </summary>
public class RefreshTokenService(IdentityDbContext db, IConfiguration configuration)
{
    private TimeSpan Lifetime =>
        TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenDays", 14));

    /// <summary>Issue a brand-new refresh token at the head of a fresh family (called on login/register).</summary>
    public async Task<IssuedRefreshToken> IssueAsync(Guid appUserId, string? ip, CancellationToken ct)
    {
        var raw = GenerateRaw();
        var entity = RefreshToken.Issue(appUserId, Hash(raw), Guid.NewGuid(), DateTime.UtcNow, Lifetime, ip);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return new IssuedRefreshToken(raw, entity.ExpiresAtUtc);
    }

    /// <summary>
    /// Validate a presented raw token. Returns the active row on success. If the token is unknown,
    /// expired, revoked, or already-spent (reuse), validation fails — and on reuse the entire family
    /// is revoked, logging out a thief and the victim together.
    /// </summary>
    public async Task<Result<RefreshToken>> ValidateAsync(string raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result<RefreshToken>.Failure(Error.Validation("Missing refresh token."));

        var hash = Hash(raw);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = DateTime.UtcNow;

        if (token is null)
            return Result<RefreshToken>.Failure(Error.Validation("Invalid refresh token."));

        // Reuse detection: a token that has already been rotated is being replayed. Burn the family.
        if (token.IsSpent || token.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, now, ct);
            return Result<RefreshToken>.Failure(Error.Validation("Refresh token reuse detected."));
        }

        if (!token.IsActive(now))
            return Result<RefreshToken>.Failure(Error.Validation("Expired refresh token."));

        return Result<RefreshToken>.Success(token);
    }

    /// <summary>Rotate an active token: mark it spent and issue its successor in the same family.</summary>
    public async Task<IssuedRefreshToken> RotateAsync(RefreshToken current, string? ip, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var raw = GenerateRaw();
        var next = RefreshToken.Issue(current.UserId, Hash(raw), current.FamilyId, now, Lifetime, ip);

        current.MarkReplacedBy(next.Id, now);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(raw, next.ExpiresAtUtc);
    }

    /// <summary>Revoke a single presented token (logout). No-op if it is unknown.</summary>
    public async Task RevokeAsync(string raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var hash = Hash(raw);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
            return;

        await RevokeFamilyAsync(token.FamilyId, DateTime.UtcNow, ct);
    }

    /// <summary>Revoke every active refresh token for a user ("log out everywhere").</summary>
    public async Task RevokeAllForUserAsync(Guid appUserId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == appUserId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var t in tokens)
            t.Revoke(now);

        await db.SaveChangesAsync(ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct)
    {
        var family = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var t in family)
            t.Revoke(now);

        await db.SaveChangesAsync(ct);
    }

    private static string GenerateRaw()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string raw)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
