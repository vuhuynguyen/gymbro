using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class UpdatePerformedExerciseHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IMediator mediator)
    : IRequestHandler<UpdatePerformedExerciseCommand, Result>
{
    public async Task<Result> Handle(UpdatePerformedExerciseCommand request, CancellationToken cancellationToken)
    {
        var load = await SessionGuard.LoadOwnedInProgressAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken);
        if (load.IsFailure)
            return Result.Failure(load.Error);
        var session = load.Value!;

        // DisableTraineeEditing: a locked assignment forbids skipping or substituting planned
        // exercises. Ad-hoc sessions and deleted assignments impose no restriction.
        if (await TraineeEditingDisabledGuard.IsDisabledAsync(mediator, session.PlanAssignmentId, cancellationToken))
            return Result.Failure(
                Forbidden("Forbidden", "Editing the planned workout is disabled for this assignment."));

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

            // Capture the substitute's name and tracking mode now for durable history.
            var namesResult = await mediator.Send(
                new ResolveExerciseNamesQuery(new[] { request.SubstituteExerciseId.Value }), cancellationToken);
            var substituteName = namesResult.IsSuccess
                ? namesResult.Value!.GetValueOrDefault(request.SubstituteExerciseId.Value)
                : null;

            var trackingResult = await mediator.Send(
                new ResolveExerciseTrackingTypesQuery(new[] { request.SubstituteExerciseId.Value }), cancellationToken);
            var substituteTrackingType = trackingResult.IsSuccess
                ? trackingResult.Value!.GetValueOrDefault(request.SubstituteExerciseId.Value, ExerciseTrackingType.Strength)
                : ExerciseTrackingType.Strength;

            exercise.Substitute(request.SubstituteExerciseId.Value, substituteName, request.Notes, substituteTrackingType);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
