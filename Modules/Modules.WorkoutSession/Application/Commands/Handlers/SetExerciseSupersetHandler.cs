using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class SetExerciseSupersetHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IMediator mediator)
    : IRequestHandler<SetExerciseSupersetCommand, Result>
{
    public async Task<Result> Handle(SetExerciseSupersetCommand request, CancellationToken cancellationToken)
    {
        var load = await SessionGuard.LoadOwnedEditableAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken);
        if (load.IsFailure)
            return Result.Failure(load.Error);
        var session = load.Value!;

        // Same lock rule as add/skip/substitute: a locked assignment forbids restructuring the workout.
        if (await TraineeEditingDisabledGuard.IsDisabledAsync(mediator, session.PlanAssignmentId, cancellationToken))
            return Result.Failure(
                Forbidden("Forbidden", "Editing the planned workout is disabled for this assignment."));

        var exercise = await exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result.Failure(NotFound("NotFound", "Exercise not found in this session."));

        if (request.PeerExerciseId == null)
        {
            // Leave the superset. A peer left alone in the old group is a harmless singleton (the clients
            // only treat a 2+ member group as a real superset), so there's nothing else to clean up.
            exercise.SetSupersetGroup(null);
        }
        else
        {
            if (request.PeerExerciseId == request.ExerciseId)
                return Result.Failure(Validation("PeerExerciseId", "An exercise can't be supersetted with itself."));

            var peer = await exerciseRepository.GetByIdAsync(request.PeerExerciseId.Value, cancellationToken);
            if (peer == null || peer.SessionId != session.Id)
                return Result.Failure(NotFound("NotFound", "The exercise to superset with was not found in this session."));

            // Join the peer's existing group, or mint a fresh one shared by both.
            var groupId = peer.SupersetGroupId ?? Guid.NewGuid();
            if (peer.SupersetGroupId == null)
                peer.SetSupersetGroup(groupId);
            exercise.SetSupersetGroup(groupId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
