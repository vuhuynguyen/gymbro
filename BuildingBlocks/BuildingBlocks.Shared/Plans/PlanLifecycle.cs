using BuildingBlocks.Shared.DomainPrimitives;
using BuildingBlocks.Shared.Results;
using static BuildingBlocks.Shared.Errors.Error;

namespace BuildingBlocks.Shared.Plans;

/// <summary>
/// Template-lifecycle guards shared by the workout and nutrition modules — single-sourced here over the
/// <see cref="IVersionedPlan"/> abstraction so a rule change applies to both. Module-specific steps (the
/// row-level author policy) are injected as delegates, preserving each handler's exact guard order and messages.
/// </summary>
public static class PlanLifecycle
{
    /// <summary>
    /// Publish guards in canonical order: archived → not-the-latest-version → author → no-unpublished-changes.
    /// <paramref name="authorCheck"/> is the module's row-level author policy, run at its established position.
    /// Success means the caller may <c>head.Publish()</c>.
    /// </summary>
    public static Result CanPublish(IVersionedPlan current, IVersionedPlan head, Func<Result> authorCheck)
    {
        if (current.IsArchived)
            return Result.Failure(Conflict("Conflict", "Unarchive the plan before publishing it."));

        if (current.Id != head.Id)
            return Result.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and publish the latest version."));

        var author = authorCheck();
        if (author.IsFailure)
            return author;

        if (!head.IsDraft)
            return Result.Failure(Conflict("Conflict", "There are no unpublished changes to publish."));

        return Result.Success();
    }
}
