using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class AddPerformedExerciseHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IMediator mediator)
    : IRequestHandler<AddPerformedExerciseCommand, Result<PerformedExerciseDto>>
{
    public async Task<Result<PerformedExerciseDto>> Handle(
        AddPerformedExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var load = await SessionGuard.LoadOwnedEditableAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken);
        if (load.IsFailure)
            return Result<PerformedExerciseDto>.Failure(load.Error);
        var session = load.Value!;

        // DisableTraineeEditing: a locked assignment forbids adding ad-hoc exercises. Ad-hoc
        // sessions (no assignment) and deleted assignments impose no restriction.
        if (await TraineeEditingDisabledGuard.IsDisabledAsync(mediator, session.PlanAssignmentId, cancellationToken))
            return Result<PerformedExerciseDto>.Failure(
                Forbidden("Forbidden", "Editing the planned workout is disabled for this assignment."));

        // Capture the exercise name and tracking mode now so the log survives a later rename/delete of the
        // exercise and the loggers/per-mode validation know how this exercise is tracked.
        var namesResult = await mediator.Send(
            new ResolveExerciseNamesQuery(new[] { request.ExerciseId }), cancellationToken);
        var exerciseName = namesResult.IsSuccess
            ? namesResult.Value!.GetValueOrDefault(request.ExerciseId)
            : null;

        var trackingResult = await mediator.Send(
            new ResolveExerciseTrackingTypesQuery(new[] { request.ExerciseId }), cancellationToken);
        var trackingType = trackingResult.IsSuccess
            ? trackingResult.Value!.GetValueOrDefault(request.ExerciseId, ExerciseTrackingType.Strength)
            : ExerciseTrackingType.Strength;

        var exercise = PerformedExercise.Create(
            session.Id,
            tenantId,
            request.ExerciseId,
            request.PlanWorkoutExerciseId,
            request.Order,
            exerciseName,
            trackingType,
            request.SupersetGroupId);

        // Adding an exercise to an already-finished workout (edit-in-place): mark it completed so the
        // history breakdown doesn't show a stray "in progress" exercise on a completed session.
        if (session.Status == SessionStatus.Completed)
            exercise.MarkCompleted();

        await exerciseRepository.AddAsync(exercise, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PerformedExerciseDto>.Success(SessionMapping.ToPerformedExerciseDto(exercise));
    }
}
