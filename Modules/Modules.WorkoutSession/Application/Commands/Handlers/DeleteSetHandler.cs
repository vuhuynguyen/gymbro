using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class DeleteSetHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IPerformedSetRepository setRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<DeleteSetCommand, Result>
{
    public async Task<Result> Handle(DeleteSetCommand request, CancellationToken cancellationToken)
    {
        var load = await SessionGuard.LoadOwnedEditableAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken);
        if (load.IsFailure)
            return Result.Failure(load.Error);
        var session = load.Value!;

        var exercise = await exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result.Failure(NotFound("NotFound", "Exercise not found in this session."));

        var set = await setRepository.GetByIdAsync(request.SetId, cancellationToken);
        if (set == null || set.PerformedExerciseId != exercise.Id)
            return Result.Failure(NotFound("NotFound", "Set not found in this exercise."));

        setRepository.Remove(set);
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
