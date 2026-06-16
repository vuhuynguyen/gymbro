using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class LogSetHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IPerformedSetRepository setRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<LogSetCommand, Result<PerformedSetDto>>
{
    public async Task<Result<PerformedSetDto>> Handle(LogSetCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var load = await SessionGuard.LoadOwnedEditableAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken);
        if (load.IsFailure)
            return Result<PerformedSetDto>.Failure(load.Error);
        var session = load.Value!;

        var exercise = await exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result<PerformedSetDto>.Failure(NotFound("NotFound", "Exercise not found in this session."));

        // A drop/rest-pause stage links to a lead set: it must belong to the same exercise and the lead must
        // itself be parentless (one level of nesting only).
        if (request.ParentSetId.HasValue)
        {
            var parent = await setRepository.GetByIdAsync(request.ParentSetId.Value, cancellationToken);
            if (parent == null || parent.PerformedExerciseId != exercise.Id)
                return Result<PerformedSetDto>.Failure(NotFound("NotFound", "Parent set not found in this exercise."));
            if (parent.ParentSetId.HasValue)
                return Result<PerformedSetDto>.Failure(Validation("Validation", "A drop stage cannot itself have drop stages."));
        }

        // Mode-aware required-metric rule: a strength set needs reps, a cardio set needs duration/distance,
        // a HIIT set needs rounds/duration, etc. Mobility/Custom accept a metric-less completed set.
        if (!ExerciseTrackingRules.HasRequiredMetric(
                exercise.TrackingType,
                request.Reps,
                request.WeightKg,
                request.DurationSeconds,
                request.DistanceM,
                request.Rounds,
                request.IsCompleted))
            return Result<PerformedSetDto>.Failure(
                Validation("Validation", ExerciseTrackingRules.RequiredMetricMessage(exercise.TrackingType)));

        var set = PerformedSet.Log(
            exercise.Id,
            tenantId,
            request.PlanSetId,
            request.SetNumber,
            request.SetType,
            request.Reps,
            request.WeightKg,
            request.DurationSeconds,
            request.DistanceM,
            request.Rpe,
            request.RestSeconds,
            request.IsCompleted,
            request.Calories,
            request.AvgHeartRate,
            request.Rounds,
            request.InclinePercent,
            request.SpeedKph,
            request.Level,
            request.ParentSetId);

        await setRepository.AddAsync(set, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Editing a finished workout in place: refresh its cached PR count (no-op for in-progress).
        if (session.Status == SessionStatus.Completed)
        {
            await SessionStatsRecalculator.RecomputeAfterEditAsync(
                sessionRepository, exerciseRepository, session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<PerformedSetDto>.Success(SessionMapping.ToPerformedSetDto(set));
    }
}
