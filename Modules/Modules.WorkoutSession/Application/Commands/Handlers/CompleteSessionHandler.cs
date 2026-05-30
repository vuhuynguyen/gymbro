using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class CompleteSessionHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CompleteSessionCommand, Result<CompleteSessionResultDto>>
{
    public async Task<Result<CompleteSessionResultDto>> Handle(
        CompleteSessionCommand request,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<CompleteSessionResultDto>.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result<CompleteSessionResultDto>.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result<CompleteSessionResultDto>.Failure(Conflict("Conflict", "Session is already completed or abandoned."));

        var exercises = await exerciseRepository.Query()
            .Include(e => e.Sets)
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        foreach (var ex in exercises)
            ex.MarkCompleted();

        session.Complete(request.RpeOverall, request.Notes, request.CompletedAt);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var totalSets = exercises.Sum(e => e.Sets.Count);
        var totalExercises = exercises.Count;

        return Result<CompleteSessionResultDto>.Success(
            SessionMapping.ToCompleteSessionResultDto(session, totalSets, totalExercises));
    }
}
