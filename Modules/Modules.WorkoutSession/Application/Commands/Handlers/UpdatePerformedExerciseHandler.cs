using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class UpdatePerformedExerciseHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<UpdatePerformedExerciseCommand, Result>
{
    public async Task<Result> Handle(UpdatePerformedExerciseCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure(Conflict("Conflict", "Session is not in progress."));

        var exercise = await exerciseRepository.GetByIdWithSetsAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result.Failure(NotFound("NotFound", "Exercise not found in this session."));

        if (request.Action == ExerciseUpdateAction.Skip)
        {
            if (exercise.Sets.Count > 0)
                return Result.Failure(Conflict("Conflict", "Cannot skip an exercise that already has logged sets. Remove the sets first."));

            exercise.Skip(request.Notes);
        }
        else
        {
            if (request.SubstituteExerciseId == null)
                return Result.Failure(Validation("SubstituteExerciseId", "SubstituteExerciseId is required for substitution."));

            exercise.Substitute(request.SubstituteExerciseId.Value, request.Notes);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
