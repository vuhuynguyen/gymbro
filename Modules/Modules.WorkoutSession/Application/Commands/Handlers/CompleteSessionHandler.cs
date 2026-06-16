using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Tracking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Commands;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

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
        var load = await SessionGuard.LoadOwnedInProgressAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken,
            SessionGuard.TerminalStateMessage);
        if (load.IsFailure)
            return Result<CompleteSessionResultDto>.Failure(load.Error);
        var session = load.Value!;

        var exercises = await exerciseRepository.Query()
            .Include(e => e.Sets)
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        foreach (var ex in exercises)
            ex.MarkCompleted();

        var prCount = await SessionStatsRecalculator.ComputePrCountAsync(
            sessionRepository, session, exercises, cancellationToken);

        session.Complete(request.RpeOverall, request.Notes, request.CompletedAt, prCount);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Drop/rest-pause stages roll up into their lead set — count only parentless rows.
        var totalSets = exercises.Sum(e => e.Sets.Count(s => s.ParentSetId == null));
        var totalExercises = exercises.Count;

        return Result<CompleteSessionResultDto>.Success(
            SessionMapping.ToCompleteSessionResultDto(session, totalSets, totalExercises));
    }
}
