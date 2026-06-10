using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Authorization;
using Xunit;

namespace Gymbro.Tests.Authorization;

/// <summary>
/// Pure unit tests (no DB) locking in the permission matrix via <see cref="PermissionService"/>:
/// Owner has every permission; Client has exactly the trainee set
/// (PlanView/ClientView/WorkoutLogCreate/WorkoutLogViewOwn + NutritionLogCreate/NutritionLogViewOwn) and
/// NOT the owner-only plan/admin/view-all permissions; an unknown (non-member) role has none. The Nutrition
/// log permissions mirror the Workout log family (Create/ViewOwn for trainees, ViewAll for coaches).
/// </summary>
public sealed class PermissionServiceTests
{
    private readonly PermissionService _sut = new();

    [Fact]
    public void Owner_has_all_permissions()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.True(
                _sut.HasPermission(TenantRole.Owner, permission),
                $"Owner should have {permission}");
        }

        Assert.Equal(19, Enum.GetValues<Permission>().Length);
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
    [InlineData(Permission.NutritionPlanCreate)]
    [InlineData(Permission.NutritionPlanUpdate)]
    [InlineData(Permission.NutritionPlanDelete)]
    [InlineData(Permission.NutritionPlanAssign)]
    [InlineData(Permission.NutritionLogViewAll)]
    public void Owner_has_owner_only_permissions(Permission permission)
    {
        Assert.True(_sut.HasPermission(TenantRole.Owner, permission));
    }

    [Theory]
    [InlineData(Permission.PlanView)]
    [InlineData(Permission.ClientView)]
    [InlineData(Permission.WorkoutLogCreate)]
    [InlineData(Permission.WorkoutLogViewOwn)]
    [InlineData(Permission.NutritionLogCreate)]
    [InlineData(Permission.NutritionLogViewOwn)]
    public void Client_has_its_allowed_permissions(Permission permission)
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
    [InlineData(Permission.NutritionPlanCreate)]
    [InlineData(Permission.NutritionPlanUpdate)]
    [InlineData(Permission.NutritionPlanDelete)]
    [InlineData(Permission.NutritionPlanAssign)]
    [InlineData(Permission.NutritionLogViewAll)]
    public void Client_is_denied_owner_only_permissions(Permission permission)
    {
        Assert.False(_sut.HasPermission(TenantRole.Client, permission));
    }

    [Fact]
    public void Client_has_exactly_its_trainee_permissions_total()
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
                Permission.NutritionLogCreate,
                Permission.NutritionLogViewOwn,
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
