using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Authorization;

/// <summary>
/// Phase 0 hardening: <see cref="TenantAuthorizationService.CanAccessResourceAsync"/> must bound a
/// ViewAll (coach/owner) caller to their OWN gym via the resource's tenant, instead of relying solely on
/// the EF tenant filter to keep cross-gym rows out of reach. The own-resource path stays tenant-agnostic
/// (a user's own data is theirs in any gym), which is what the unified personal experience depends on.
/// </summary>
public sealed class CanAccessResourceTenantScopingTests
{
    private static readonly Guid Caller = Guid.NewGuid();
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static TenantAuthorizationService Build(TenantRole role)
    {
        var roleResolver = Substitute.For<ITenantRoleResolver>();
        roleResolver.GetRoleAsync(Caller, TenantA, Arg.Any<CancellationToken>())
            .Returns((TenantRole?)role);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Caller);

        return new TenantAuthorizationService(roleResolver, new PermissionService(), currentUser);
    }

    private static Task<bool> AccessAsync(
        TenantAuthorizationService svc, Guid resourceUserId, Guid? resourceTenantId) =>
        svc.CanAccessResourceAsync(
            TenantA,
            Permission.WorkoutLogViewOwn,
            Permission.WorkoutLogViewAll,
            resourceUserId,
            resourceTenantId);

    [Fact]
    public async Task ViewAll_caller_is_denied_a_resource_in_another_gym()
    {
        var svc = Build(TenantRole.Owner); // Owner holds WorkoutLogViewAll
        Assert.False(await AccessAsync(svc, resourceUserId: Guid.NewGuid(), resourceTenantId: TenantB));
    }

    [Fact]
    public async Task ViewAll_caller_is_allowed_a_resource_in_their_own_gym()
    {
        var svc = Build(TenantRole.Owner);
        Assert.True(await AccessAsync(svc, resourceUserId: Guid.NewGuid(), resourceTenantId: TenantA));
    }

    [Fact]
    public async Task ViewAll_caller_with_unknown_resource_tenant_keeps_legacy_behavior()
    {
        // Null resource tenant = caller didn't supply it; the EF tenant filter still scopes the load.
        var svc = Build(TenantRole.Owner);
        Assert.True(await AccessAsync(svc, resourceUserId: Guid.NewGuid(), resourceTenantId: null));
    }

    [Fact]
    public async Task Own_resource_is_allowed_regardless_of_resource_tenant()
    {
        var svc = Build(TenantRole.Client); // Client holds WorkoutLogViewOwn only
        Assert.True(await AccessAsync(svc, resourceUserId: Caller, resourceTenantId: TenantB));
    }

    [Fact]
    public async Task Another_users_resource_is_denied_for_an_own_only_caller()
    {
        var svc = Build(TenantRole.Client);
        Assert.False(await AccessAsync(svc, resourceUserId: Guid.NewGuid(), resourceTenantId: TenantA));
    }
}
