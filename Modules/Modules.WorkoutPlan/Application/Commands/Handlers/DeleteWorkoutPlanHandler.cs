using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class DeleteWorkoutPlanHandler(
    IWorkoutPlanRepository repository,
    IPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<DeleteWorkoutPlanCommand, Result>
{
    public async Task<Result> Handle(DeleteWorkoutPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        
        if (plan == null)
            return Result.Failure(NotFound("NotFound", "Plan not found."));
        
        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(plan, currentUser);
        if (authorCheck.IsFailure)
            return authorCheck;

        // Do not orphan live assignments: a plan version pinned to a trainee may not be deleted until
        // the assignment is revoked. Past sessions keep their own snapshot, so a plan with only
        // historical sessions (and no live assignment) stays deletable.
        var hasLiveAssignment = await assignmentRepository.Query()
            .AnyAsync(a => a.PlanId == plan.Id, cancellationToken);
        if (hasLiveAssignment)
            return Result.Failure(Conflict(
                "Conflict", "This plan is assigned to a trainee. Revoke the assignment before deleting the plan."));

        // DB6: hard-delete the plan's child structure (workouts → exercises → sets) before soft-deleting
        // the header. The header is ISoftDelete so its delete becomes an UPDATE — the DB-level cascade
        // never fires — which would otherwise orphan the structure rows. ClearPlanStructureAsync issues
        // bulk DELETEs (DB cascade reaches the prescribed sets) and is idempotent, so a retry is safe.
        await repository.ClearPlanStructureAsync(plan.Id, cancellationToken);

        plan.MarkDeleted();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
