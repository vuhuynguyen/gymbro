using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class GetPlanAssignmentByIdHandler(
    IPlanAssignmentRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<GetPlanAssignmentByIdQuery, Result<PlanAssignmentForSessionDto>>
{
    public async Task<Result<PlanAssignmentForSessionDto>> Handle(
        GetPlanAssignmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var assignment = await repository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (assignment == null)
            return Result<PlanAssignmentForSessionDto>.Failure(NotFound("NotFound", "Plan assignment not found."));

        // Row guard: only the trainee the assignment belongs to (or an admin) may resolve it. The
        // current sole caller (StartSessionHandler) also enforces this, but guarding here keeps the
        // query safe by construction if it is ever exposed directly.
        if (!currentUser.IsAdmin && assignment.TraineeId != currentUser.UserId)
            return Result<PlanAssignmentForSessionDto>.Failure(
                Unauthorized("Unauthorized", "This assignment does not belong to you."));

        return Result<PlanAssignmentForSessionDto>.Success(
            new PlanAssignmentForSessionDto(
                assignment.Id,
                assignment.TraineeId,
                assignment.VisibilityMode,
                assignment.HideExercises,
                assignment.HideSetsReps,
                assignment.HideFutureWorkouts,
                assignment.DisableTraineeEditing));
    }
}
