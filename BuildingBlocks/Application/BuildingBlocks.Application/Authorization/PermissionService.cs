using BuildingBlocks.Shared.Authorization;

namespace BuildingBlocks.Application.Authorization;

public class PermissionService : IPermissionService
{
    public bool HasPermission(TenantRole role, Permission permission)
    {
        return role switch
        {
            TenantRole.Owner => permission switch
            {
                Permission.PlanCreate        => true,
                Permission.PlanUpdate        => true,
                Permission.PlanDelete        => true,
                Permission.PlanAssign        => true,
                Permission.PlanView          => true,
                Permission.PlanViewAll       => true,
                Permission.ClientView        => true,
                Permission.ClientRemove      => true,
                Permission.InviteCreate      => true,
                Permission.WorkoutLogCreate  => true,
                Permission.WorkoutLogViewOwn => true,
                Permission.WorkoutLogViewAll => true,
                Permission.NutritionPlanCreate => true,
                Permission.NutritionPlanUpdate => true,
                Permission.NutritionPlanDelete => true,
                Permission.NutritionPlanAssign => true,
                Permission.NutritionLogCreate  => true,
                Permission.NutritionLogViewOwn => true,
                Permission.NutritionLogViewAll => true,
                _                            => false
            },

            TenantRole.Client => permission switch
            {
                Permission.PlanView          => true,
                Permission.ClientView        => true,
                Permission.WorkoutLogCreate  => true,
                Permission.WorkoutLogViewOwn => true,
                Permission.NutritionLogCreate  => true,
                Permission.NutritionLogViewOwn => true,
                _                            => false
            },

            _ => false
        };
    }
}
