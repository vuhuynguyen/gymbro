using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

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

        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<PerformedSetDto>.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result<PerformedSetDto>.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result<PerformedSetDto>.Failure(Conflict("Conflict", "Session is not in progress."));

        var exercise = await exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise == null || exercise.SessionId != session.Id)
            return Result<PerformedSetDto>.Failure(NotFound("NotFound", "Exercise not found in this session."));

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
            request.IsCompleted);

        await setRepository.AddAsync(set, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PerformedSetDto>.Success(SessionMapping.ToPerformedSetDto(set));
    }
}
