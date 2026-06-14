using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.IdentityModule.Application.Commands;
using Modules.IdentityModule.Application.Models;
using Modules.IdentityModule.Infrastructure.Identity;
using Modules.UserModule.Application.Admin.Commands;
using Modules.UserModule.Entities;
using Xunit;

namespace Gymbro.Tests.Integration;

/// <summary>
/// End-to-end proof that the two cross-store writes are atomic against a real PostgreSQL instance:
/// registration (AppUser + domain User/Tenant/role) and admin delete (domain soft-delete + AppUser
/// hard-delete) each commit or roll back together. This covers the part the mocked handler unit tests
/// cannot — the genuine shared-connection transaction across <c>AppDbContext</c> and <c>IdentityDbContext</c>.
/// Skipped automatically when no Docker engine is available.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CrossStoreTransactionTests
{
    private const string Password = "P@ssw0rd123!";
    private readonly PostgresFixture _fixture;

    public CrossStoreTransactionTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _fixture.Toggle.Reset();
        // These handlers are resolved directly (bypassing the pipeline); act as platform admin so the
        // domain delete's GetByIdAsync isn't hidden by EF tenant filters and provisioning isn't stamped.
        _fixture.Principal.Become(Guid.Empty, Guid.Empty, isAdmin: true);
    }

    [SkippableFact]
    public async Task Registration_commits_AppUser_and_domain_provisioning_together()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason!);
        var email = UniqueEmail();

        var result = await Register(email);

        Assert.True(result.IsSuccess);

        var appUser = await GetAppUser(email);
        Assert.NotNull(appUser);
        Assert.NotEqual(Guid.Empty, appUser!.DomainUserId);

        await _fixture.InScopeAsync(async sp =>
        {
            var appDb = sp.GetRequiredService<AppDbContext>();
            var domainUserId = appUser.DomainUserId;

            Assert.True(await appDb.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Id == domainUserId));
            Assert.True(await appDb.Set<Tenant>().IgnoreQueryFilters().AnyAsync(t => t.OwnerUserId == domainUserId));
            Assert.True(await appDb.Set<UserTenantRole>().IgnoreQueryFilters().AnyAsync(r => r.UserId == domainUserId));
        });
    }

    [SkippableFact]
    public async Task Registration_provisioning_failure_rolls_back_the_AppUser()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason!);
        var email = UniqueEmail();
        var (appUsers, users, tenants) = await Counts();

        _fixture.Toggle.FailProvisioning = true;
        await Assert.ThrowsAnyAsync<Exception>(() => Register(email));

        // Nothing leaked: the AppUser created by UserManager.CreateAsync was inside the same transaction
        // as the (failed) provisioning, so every row count is exactly where it started.
        Assert.Null(await GetAppUser(email));
        Assert.Equal((appUsers, users, tenants), await Counts());
    }

    [SkippableFact]
    public async Task Admin_delete_removes_both_stores()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason!);
        var email = UniqueEmail();
        Assert.True((await Register(email)).IsSuccess);
        var domainUserId = (await GetAppUser(email))!.DomainUserId;

        var result = await Delete(domainUserId);

        Assert.True(result.IsSuccess);
        Assert.Null(await GetAppUser(email));   // Identity AppUser hard-deleted.

        await _fixture.InScopeAsync(async sp =>
        {
            var appDb = sp.GetRequiredService<AppDbContext>();
            var user = await appDb.Set<User>().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == domainUserId);
            Assert.NotNull(user);          // soft-delete keeps the row...
            Assert.True(user!.IsDeleted);  // ...flagged deleted.
        });
    }

    [SkippableFact]
    public async Task Admin_delete_identity_failure_rolls_back_the_domain_delete()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason!);
        var email = UniqueEmail();
        Assert.True((await Register(email)).IsSuccess);
        var domainUserId = (await GetAppUser(email))!.DomainUserId;

        _fixture.Toggle.FailIdentityCleanup = true;

        await Assert.ThrowsAnyAsync<Exception>(() => Delete(domainUserId));

        // Both stores reverted: domain user still live, AppUser still present — no half-deleted user.
        Assert.NotNull(await GetAppUser(email));
        await _fixture.InScopeAsync(async sp =>
        {
            var appDb = sp.GetRequiredService<AppDbContext>();
            var user = await appDb.Set<User>().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == domainUserId);
            Assert.NotNull(user);
            Assert.False(user!.IsDeleted);
        });
    }

    private Task<Result<TokenPair>> Register(string email) =>
        _fixture.InScopeAsync(sp =>
            sp.GetRequiredService<IRequestHandler<RegisterCommand, Result<TokenPair>>>()
                .Handle(new RegisterCommand(email, Password, "Jane Doe"), CancellationToken.None));

    private Task<Result> Delete(Guid domainUserId) =>
        _fixture.InScopeAsync(sp =>
            sp.GetRequiredService<IRequestHandler<AdminDeleteUserCommand, Result>>()
                .Handle(new AdminDeleteUserCommand(domainUserId), CancellationToken.None));

    private Task<(int appUsers, int users, int tenants)> Counts() =>
        _fixture.InScopeAsync(async sp =>
        {
            var identityDb = sp.GetRequiredService<IdentityDbContext>();
            var appDb = sp.GetRequiredService<AppDbContext>();
            return (
                await identityDb.Users.CountAsync(),
                await appDb.Set<User>().IgnoreQueryFilters().CountAsync(),
                await appDb.Set<Tenant>().IgnoreQueryFilters().CountAsync());
        });

    private Task<AppUser?> GetAppUser(string email) =>
        _fixture.InScopeAsync(sp =>
            sp.GetRequiredService<IdentityDbContext>().Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email));

    private static string UniqueEmail() => $"jane-{Guid.NewGuid():N}@example.com";
}
