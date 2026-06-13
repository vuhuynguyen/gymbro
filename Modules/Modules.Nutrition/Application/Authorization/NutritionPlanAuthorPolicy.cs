using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Authorization;

/// <summary>
/// Row-level authorship guard for nutrition-plan mutation — the nutrition mirror of
/// <c>WorkoutPlanModule.Application.Authorization.PlanAuthorPolicy</c>. A plan may only be modified by the user
/// who authored it, or by a platform admin; the EF tenant filter already scopes the load to the gym. Authorship
/// is stable across versions: <see cref="NutritionPlan.CreateDraft"/> stamps the editing user as <c>CreatedBy</c>,
/// so every version in a template carries the same author. See <c>docs/PERMISSIONS.md</c>.
/// </summary>
internal static class NutritionPlanAuthorPolicy
{
    public static Result EnsureCanMutate(NutritionPlan plan, ICurrentUser currentUser)
    {
        if (currentUser.IsAdmin)
            return Result.Success();

        if (plan.CreatedBy is { } author && author == currentUser.UserId)
            return Result.Success();

        return Result.Failure(Forbidden(
            "Forbidden", "Only the plan's author can modify this plan."));
    }
}
