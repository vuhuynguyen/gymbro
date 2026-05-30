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

public sealed class AddPerformedExerciseHandler(
    IWorkoutSessionRepository sessionRepository,
    IPerformedExerciseRepository exerciseRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<AddPerformedExerciseCommand, Result<PerformedExerciseDto>>
{
    public async Task<Result<PerformedExerciseDto>> Handle(
        AddPerformedExerciseCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<PerformedExerciseDto>.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result<PerformedExerciseDto>.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result<PerformedExerciseDto>.Failure(Conflict("Conflict", "Session is not in progress."));

        var exercise = PerformedExercise.Create(
            session.Id,
            tenantId,
            request.ExerciseId,
            request.PlanWorkoutExerciseId,
            request.Order);

        await exerciseRepository.AddAsync(exercise, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PerformedExerciseDto>.Success(SessionMapping.ToPerformedExerciseDto(exercise));
    }
}
