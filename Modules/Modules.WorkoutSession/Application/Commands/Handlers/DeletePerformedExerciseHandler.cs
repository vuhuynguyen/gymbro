using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class DeletePerformedExerciseHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IMediator mediator)
    : IRequestHandler<DeletePerformedExerciseCommand, Result>
{
    public async Task<Result> Handle(DeletePerformedExerciseCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure(Conflict("Conflict", "Session is not in progress."));

        // DisableTraineeEditing: a locked assignment forbids removing planned exercises (same rule as
        // skip/substitute). Ad-hoc sessions and deleted assignments impose no restriction.
        if (await TraineeEditingDisabledGuard.IsDisabledAsync(mediator, session.PlanAssignmentId, cancellationToken))
            return Result.Failure(
                Forbidden("Forbidden", "Editing the planned workout is disabled for this assignment."));

        var exercise = await exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result.Failure(NotFound("NotFound", "Exercise not found in this session."));

        // The PerformedExercise → Sets FK is ON DELETE CASCADE, so removing the exercise also deletes
        // its logged sets in the same transaction — no need to walk and delete them first.
        exerciseRepository.Remove(exercise);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
