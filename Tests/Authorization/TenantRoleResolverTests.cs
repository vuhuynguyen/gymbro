using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Authorization;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Application.Authorization;
using Modules.UserModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Authorization;

/// <summary>
/// F9: a single request resolves the caller's tenant role on several paths (middleware membership
/// validation, AuthorizationBehavior, handler-level CanAccessResourceAsync). The scoped
/// <see cref="RequestRoleCache"/> must collapse those to one DB lookup — including caching a confirmed
/// non-membership (null) so repeated checks for a non-member never re-query.
/// </summary>
public sealed class TenantRoleResolverTests
{
    [Fact]
    public async Task Repeated_lookups_hit_the_repository_once()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var repo = Substitute.For<IUserTenantRoleRepository>();
        repo.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns(UserTenantRole.Create(userId, tenantId, TenantRole.Owner));

        var sut = new TenantRoleResolver(repo, new RequestRoleCache());

        var first = await sut.GetRoleAsync(userId, tenantId);
        var second = await sut.GetRoleAsync(userId, tenantId);
        var third = await sut.GetRoleAsync(userId, tenantId);

        Assert.Equal(TenantRole.Owner, first);
        Assert.Equal(TenantRole.Owner, second);
        Assert.Equal(TenantRole.Owner, third);
        await repo.Received(1).GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_membership_is_cached_and_not_requeried()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var repo = Substitute.For<IUserTenantRoleRepository>();
        repo.GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>())
            .Returns((UserTenantRole?)null);

        var sut = new TenantRoleResolver(repo, new RequestRoleCache());

        var first = await sut.GetRoleAsync(userId, tenantId);
        var second = await sut.GetRoleAsync(userId, tenantId);

        Assert.Null(first);
        Assert.Null(second);
        await repo.Received(1).GetByUserAndTenantAsync(userId, tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_seeded_cache_entry_is_reused_without_any_repository_call()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var repo = Substitute.For<IUserTenantRoleRepository>();
        var cache = new RequestRoleCache();
        // Simulate TenantResolutionMiddleware having already resolved + seeded the role.
        cache.Set(userId, tenantId, TenantRole.Client);

        var sut = new TenantRoleResolver(repo, cache);

        var role = await sut.GetRoleAsync(userId, tenantId);

        Assert.Equal(TenantRole.Client, role);
        await repo.DidNotReceive().GetByUserAndTenantAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
