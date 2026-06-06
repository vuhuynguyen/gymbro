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
        // The caller is always the performing trainee; redact the start response when their assignment
        // hides sets/reps so the prescription never reaches them (the stored snapshot stays full).
        var hideSetsRepsForCaller = false;
        var plannedExercises = new List<(Guid ExerciseId, Guid? PlanWorkoutExerciseId, int Order, string? ExerciseName)>();

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

            hideSetsRepsForCaller =
                assignment.VisibilityMode == PlanVisibilityMode.Guided && assignment.HideSetsReps;

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
                    // Store the full snapshot; the trainee's view is redacted on read (filter-on-read).
                    var snapshot = SessionMapping.BuildSnapshot(workoutSnapshot);
                    snapshotJson = JsonSerializer.Serialize(snapshot);

                    // Pre-populate the session with the plan's exercises so the trainee sees the
                    // workout to perform immediately (planned sets are resolved from the snapshot).
                    plannedExercises = workoutSnapshot.Exercises
                        .Select(e => (e.ExerciseId, (Guid?)e.Id, e.Order, e.ExerciseName))
                        .ToList();
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

        if (plannedExercises.Count > 0)
            session.SeedPlannedExercises(plannedExercises);

        await sessionRepository.AddAsync(session, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var snapshotDto = SessionMapping.DeserializeSnapshot(snapshotJson);
        if (snapshotDto != null && hideSetsRepsForCaller)
            snapshotDto = SessionMapping.RedactSnapshotTargets(snapshotDto);

        return Result<SessionStartResultDto>.Success(
            SessionMapping.ToSessionStartResultDto(session, snapshotDto));
    }
}
