using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class ReorderSetsHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<ReorderSetsCommand, Result>
{
    public async Task<Result> Handle(ReorderSetsCommand request, CancellationToken cancellationToken)
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

        // The new order must be a permutation of exactly this exercise's set ids — no missing, extra, or
        // foreign ids. Renumbering a partial list would corrupt the set sequence.
        var existing = exercise.Sets.Select(s => s.Id).ToHashSet();
        if (request.SetIds.Count != existing.Count || !request.SetIds.ToHashSet().SetEquals(existing))
            return Result.Failure(Validation("SetIds", "The provided set ids must match the exercise's sets exactly."));

        var byId = exercise.Sets.ToDictionary(s => s.Id);
        for (var i = 0; i < request.SetIds.Count; i++)
            byId[request.SetIds[i]].Reposition(i + 1);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
