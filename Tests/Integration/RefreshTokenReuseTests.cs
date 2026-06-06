using Microsoft.Extensions.DependencyInjection;
using Modules.IdentityModule.Infrastructure.Services;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// Integration coverage for the rotating-refresh-token security path (no prior tests existed for it).
/// Exercises <see cref="RefreshTokenService"/> against a real Postgres: issue → validate → rotate, then
/// the critical reuse-detection invariant — replaying an already-rotated ("spent") token must fail AND
/// revoke the whole family, so the legitimate successor is invalidated too (logs out a token thief and
/// the victim together). Each step runs in its own DI scope, mirroring separate HTTP refresh requests.
/// Skips automatically when no Docker daemon is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RefreshTokenReuseTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Replaying_a_rotated_token_is_rejected_and_burns_the_family()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);
        var userId = Guid.NewGuid();   // RefreshToken.UserId is a plain id (no FK), so no AppUser needed.

        // Request 1: login issues the first token in a fresh family.
        var first = await fixture.InScopeAsync(sp =>
            sp.GetRequiredService<RefreshTokenService>().IssueAsync(userId, ip: null, CancellationToken.None));

        // Request 2: a normal refresh validates the presented token and rotates it to a successor.
        var second = await fixture.InScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<RefreshTokenService>();
            var validated = await svc.ValidateAsync(first.Raw, CancellationToken.None);
            Assert.True(validated.IsSuccess);
            return await svc.RotateAsync(validated.Value!, ip: null, CancellationToken.None);
        });

        // Request 3: the successor is valid; replaying the spent original trips reuse detection and burns
        // the family; the successor is then revoked too.
        await fixture.InScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<RefreshTokenService>();

            Assert.True((await svc.ValidateAsync(second.Raw, CancellationToken.None)).IsSuccess);
            Assert.True((await svc.ValidateAsync(first.Raw, CancellationToken.None)).IsFailure);  // reuse
            Assert.True((await svc.ValidateAsync(second.Raw, CancellationToken.None)).IsFailure); // family burned
        });
    }

    [SkippableFact]
    public async Task An_unknown_token_fails_validation()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason!);

        await fixture.InScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<RefreshTokenService>();
            var result = await svc.ValidateAsync("not-a-real-token", CancellationToken.None);
            Assert.True(result.IsFailure);
        });
    }
}
