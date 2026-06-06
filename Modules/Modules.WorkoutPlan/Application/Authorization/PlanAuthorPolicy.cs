using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Authorization;

/// <summary>
/// Row-level authorship guard for plan mutation (S7). A plan may only be modified by the user who
/// authored it, or by a platform admin. The flat "any Owner edits any plan" model is intentionally
/// replaced by per-row author ownership — see <c>docs/PERMISSIONS.md</c>.
///
/// Authorship is stable across versions: only the author can produce the next version, and
/// <see cref="WorkoutPlan.CreateNewVersion"/> stamps the editing (author) user as <c>CreatedBy</c>,
/// so every version in a template carries the same author.
/// </summary>
internal static class PlanAuthorPolicy
{
    public static Result EnsureCanMutate(WorkoutPlan plan, ICurrentUser currentUser)
    {
        if (currentUser.IsAdmin)
            return Result.Success();

        if (plan.CreatedBy is { } author && author == currentUser.UserId)
            return Result.Success();

        return Result.Failure(Forbidden(
            "Forbidden", "Only the plan's author can modify this plan."));
    }
}
