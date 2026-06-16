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
        var load = await SessionGuard.LoadOwnedEditableAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken);
        if (load.IsFailure)
            return Result.Failure(load.Error);
        var session = load.Value!;

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

        // Editing a finished workout in place: refresh its cached PR count (no-op for in-progress).
        if (session.Status == SessionStatus.Completed)
        {
            await SessionStatsRecalculator.RecomputeAfterEditAsync(
                sessionRepository, exerciseRepository, session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
