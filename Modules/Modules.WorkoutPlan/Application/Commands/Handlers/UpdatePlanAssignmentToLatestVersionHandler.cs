using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class UpdatePlanAssignmentToLatestVersionHandler(
    IPlanAssignmentRepository assignmentRepository,
    IWorkoutPlanRepository workoutPlanRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePlanAssignmentToLatestVersionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdatePlanAssignmentToLatestVersionCommand request,
        CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result<bool>.Failure(NotFound("NotFound", "Plan assignment not found."));

        var currentPlan = await workoutPlanRepository.GetByIdAsync(assignment.PlanId, cancellationToken);
        if (currentPlan == null)
            return Result<bool>.Failure(NotFound("NotFound", "Workout plan not found."));

        // Advance only to the latest PUBLISHED version — an in-progress draft head is never applied to a trainee.
        var latest = await workoutPlanRepository.GetLatestPublishedVersionInTemplateAsync(currentPlan.TemplateId, cancellationToken);
        if (latest == null)
            return Result<bool>.Failure(NotFound("NotFound", "Workout plan not found."));

        // An archived plan cannot be assigned, and apply-latest must not advance an assignment onto an
        // archived version (consistent with CreatePlanAssignment).
        if (latest.IsArchived)
            return Result<bool>.Failure(
                Conflict("Conflict", "The plan's latest version is archived and cannot be applied."));

        if (assignment.PlanVersion >= latest.Version)
            return Result<bool>.Success(false);

        // Never let apply-latest blank an existing snapshot: if the caller does not supply a fresh
        // snapshot, preserve the assignment's current one rather than nulling it.
        var snapshotJson = string.IsNullOrWhiteSpace(request.SnapshotJson)
            ? assignment.SnapshotJson
            : request.SnapshotJson;

        assignment.ApplyNewVersion(latest.Id, latest.Version, snapshotJson);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Re-pointing to the latest version would collide with an existing live assignment of the same
            // template at that version (the (TenantId,TraineeId,PlanId) unique index). Surface a clean 409
            // instead of a 500, consistent with create/publish. (Audit finding 7.)
            return Result<bool>.Failure(
                Conflict("Conflict", "The trainee already has a live assignment of this plan version."));
        }

        return Result<bool>.Success(true);
    }
}
