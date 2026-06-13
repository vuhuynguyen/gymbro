using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.DomainPrimitives;
using BuildingBlocks.Shared.Results;
using static BuildingBlocks.Shared.Errors.Error;

namespace BuildingBlocks.Application.Plans;

/// <summary>
/// Assignment-lifecycle operations shared by the workout and nutrition modules, single-sourced so a rule change
/// applies to both without coupling the modules to each other. The module's repository load is passed as a
/// delegate (each module has its own repository interface), so this stays decoupled from those interfaces.
/// </summary>
public static class PlanAssignmentLifecycle
{
    /// <summary>Pause/resume an assignment: load by id (NotFound if missing), toggle, persist.</summary>
    public static async Task<Result> SetActiveAsync<TAssignment>(
        Func<Guid, CancellationToken, Task<TAssignment?>> getByIdAsync,
        IUnitOfWork unitOfWork,
        Guid assignmentId,
        bool active,
        string notFoundMessage,
        CancellationToken cancellationToken)
        where TAssignment : class, ISettableActive
    {
        var assignment = await getByIdAsync(assignmentId, cancellationToken);
        if (assignment == null)
            return Result.Failure(NotFound("NotFound", notFoundMessage));

        assignment.SetActive(active);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
