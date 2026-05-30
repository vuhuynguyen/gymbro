using System.Text.Json;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Queries;
using Modules.WorkoutPlanModule.Entities;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class StartSessionHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<StartSessionCommand, Result<SessionStartResultDto>>
{
    public async Task<Result<SessionStartResultDto>> Handle(
        StartSessionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var existing = await sessionRepository.GetActiveForTraineeAsync(currentUser.UserId, cancellationToken);
        if (existing != null)
            return Result<SessionStartResultDto>.Failure(Conflict("Conflict", "You already have an in-progress session."));

        string? snapshotJson = null;
        string? workoutName = null;
        Guid? plannedWorkoutId = request.PlannedWorkoutId;

        if (request.Source == SessionSource.FromAssignment)
        {
            if (request.PlanAssignmentId == null)
                return Result<SessionStartResultDto>.Failure(Validation("PlanAssignmentId", "PlanAssignmentId is required when source is FromAssignment."));

            var assignmentResult = await mediator.Send(
                new GetPlanAssignmentByIdQuery(request.PlanAssignmentId.Value),
                cancellationToken);

            if (assignmentResult.IsFailure)
                return Result<SessionStartResultDto>.Failure(assignmentResult.Error);

            var assignment = assignmentResult.Value!;

            if (assignment.TraineeId != currentUser.UserId)
                return Result<SessionStartResultDto>.Failure(Unauthorized("Unauthorized", "This assignment does not belong to you."));

            if (assignment.VisibilityMode != PlanVisibilityMode.Blind && plannedWorkoutId.HasValue)
            {
                var workoutResult = await mediator.Send(
                    new GetWorkoutForSnapshotQuery(plannedWorkoutId.Value),
                    cancellationToken);

                if (workoutResult.IsFailure)
                    return Result<SessionStartResultDto>.Failure(workoutResult.Error);

                var workoutSnapshot = workoutResult.Value;
                if (workoutSnapshot != null)
                {
                    workoutName = workoutSnapshot.Name;
                    var snapshot = SessionMapping.BuildSnapshot(workoutSnapshot);
                    snapshotJson = JsonSerializer.Serialize(snapshot);
                }
            }
        }

        var session = WorkoutSession.Start(
            currentUser.UserId,
            tenantId,
            request.Source,
            request.PlanAssignmentId,
            plannedWorkoutId,
            workoutName,
            snapshotJson,
            request.ClientTimezone,
            request.BodyweightKg);

        await sessionRepository.AddAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var snapshotDto = SessionMapping.DeserializeSnapshot(snapshotJson);

        return Result<SessionStartResultDto>.Success(
            SessionMapping.ToSessionStartResultDto(session, snapshotDto));
    }
}
