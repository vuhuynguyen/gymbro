using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Authorization;
using Xunit;

namespace Gymbro.Tests.Authorization;

/// <summary>
/// Pure unit tests (no DB) locking in the S1 permission matrix via <see cref="PermissionService"/>:
/// Owner has all 12 permissions; Client has exactly PlanView/ClientView/WorkoutLogCreate/WorkoutLogViewOwn
/// and NOT ClientRemove/PlanCreate/PlanViewAll/WorkoutLogViewAll; an unknown (non-member) role has none.
/// </summary>
public sealed class PermissionServiceTests
{
    private readonly PermissionService _sut = new();

    [Fact]
    public void Owner_has_all_twelve_permissions()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.True(
                _sut.HasPermission(TenantRole.Owner, permission),
                $"Owner should have {permission}");
        }

        Assert.Equal(12, Enum.GetValues<Permission>().Length);
    }

    [Theory]
    [InlineData(Permission.ClientRemove)]
    [InlineData(Permission.PlanCreate)]
    [InlineData(Permission.PlanUpdate)]
    [InlineData(Permission.PlanDelete)]
    [InlineData(Permission.PlanAssign)]
    [InlineData(Permission.PlanViewAll)]
    [InlineData(Permission.InviteCreate)]
    [InlineData(Permission.WorkoutLogViewAll)]
    public void Owner_has_owner_only_permissions(Permission permission)
    {
        Assert.True(_sut.HasPermission(TenantRole.Owner, permission));
    }

    [Theory]
    [InlineData(Permission.PlanView)]
    [InlineData(Permission.ClientView)]
    [InlineData(Permission.WorkoutLogCreate)]
    [InlineData(Permission.WorkoutLogViewOwn)]
    public void Client_has_its_four_allowed_permissions(Permission permission)
    {
        Assert.True(_sut.HasPermission(TenantRole.Client, permission));
    }

    [Theory]
    [InlineData(Permission.ClientRemove)]
    [InlineData(Permission.PlanCreate)]
    [InlineData(Permission.PlanUpdate)]
    [InlineData(Permission.PlanDelete)]
    [InlineData(Permission.PlanAssign)]
    [InlineData(Permission.PlanViewAll)]
    [InlineData(Permission.InviteCreate)]
    [InlineData(Permission.WorkoutLogViewAll)]
    public void Client_is_denied_owner_only_permissions(Permission permission)
    {
        Assert.False(_sut.HasPermission(TenantRole.Client, permission));
    }

    [Fact]
    public void Client_has_exactly_four_permissions_total()
    {
        var granted = Enum.GetValues<Permission>()
            .Where(p => _sut.HasPermission(TenantRole.Client, p))
            .ToHashSet();

        Assert.Equal(
            new HashSet<Permission>
            {
                Permission.PlanView,
                Permission.ClientView,
                Permission.WorkoutLogCreate,
                Permission.WorkoutLogViewOwn,
            },
            granted);
    }

    [Fact]
    public void Unknown_non_member_role_has_no_permissions()
    {
        var unknown = (TenantRole)999;

        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.False(
                _sut.HasPermission(unknown, permission),
                $"Non-member should NOT have {permission}");
        }
    }
}
