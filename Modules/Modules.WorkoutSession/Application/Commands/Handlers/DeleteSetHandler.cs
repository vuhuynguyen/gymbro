using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

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
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure(Conflict("Conflict", "Session is not in progress."));

        var exercise = await exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result.Failure(NotFound("NotFound", "Exercise not found in this session."));

        var set = await setRepository.GetByIdAsync(request.SetId, cancellationToken);
        if (set == null || set.PerformedExerciseId != exercise.Id)
            return Result.Failure(NotFound("NotFound", "Set not found in this exercise."));

        setRepository.Remove(set);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
