using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class EditSetHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IPerformedSetRepository setRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<EditSetCommand, Result>
{
    public async Task<Result> Handle(EditSetCommand request, CancellationToken cancellationToken)
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

        // Re-run the mode-aware required-metric rule against the POST-edit state (a null field in the
        // request means "keep existing"), matching LogSet so an edit can't leave a set mode-incoherent.
        // (Audit finding 18.)
        if (!ExerciseTrackingRules.HasRequiredMetric(
                exercise.TrackingType,
                request.Reps ?? set.Reps,
                request.WeightKg ?? set.WeightKg,
                request.DurationSeconds ?? set.DurationSeconds,
                request.DistanceM ?? set.DistanceM,
                request.Rounds ?? set.Rounds,
                request.IsCompleted ?? set.IsCompleted))
            return Result.Failure(
                Validation("Validation", ExerciseTrackingRules.RequiredMetricMessage(exercise.TrackingType)));

        set.Edit(
            request.Reps,
            request.WeightKg,
            request.DurationSeconds,
            request.DistanceM,
            request.Rpe,
            request.RestSeconds,
            request.IsCompleted,
            request.SetType,
            request.Calories,
            request.AvgHeartRate,
            request.Rounds);

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
