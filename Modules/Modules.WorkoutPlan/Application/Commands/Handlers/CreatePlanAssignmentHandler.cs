using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class CreatePlanAssignmentHandler(
    IPlanAssignmentRepository assignmentRepository,
    IWorkoutPlanRepository workoutPlanRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantRoleResolver roleResolver)
    : IRequestHandler<CreatePlanAssignmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlanAssignmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var plan = await workoutPlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Workout plan not found."));

        // Always pin the latest PUBLISHED version, never a draft head. The picker may pass a draft id (a plan
        // mid-edit), but trainees only ever receive published versions.
        var published = await workoutPlanRepository.GetLatestPublishedVersionInTemplateAsync(plan.TemplateId, cancellationToken);
        if (published == null)
            return Result<Guid>.Failure(Conflict("Conflict", "Publish the plan before assigning it."));

        if (published.IsArchived)
            return Result<Guid>.Failure(Conflict("Conflict", "This plan is archived and cannot be assigned."));

        // A plan may only be assigned to a member of this tenant; without this check an Owner could
        // assign to any GUID (a non-member, a nonexistent user, or another tenant's user). Any member
        // is allowed (not just Client) so self-assignment by an Owner still works.
        var traineeRole = await roleResolver.GetRoleAsync(request.TraineeId, tenantId, cancellationToken);
        if (traineeRole == null)
            return Result<Guid>.Failure(
                Validation("PlanAssignment.TraineeNotMember", "Trainee is not a member of this tenant."));

        // Refuse a duplicate live assignment of the same published version to the same trainee (mirrors the
        // unique partial index) so the caller gets a clean 409 instead of a database constraint error.
        var alreadyAssigned = await assignmentRepository.Query()
            .AnyAsync(a => a.TraineeId == request.TraineeId && a.PlanId == published.Id, cancellationToken);
        if (alreadyAssigned)
            return Result<Guid>.Failure(
                Conflict("Conflict", "This plan is already assigned to this trainee."));

        var assignment = PlanAssignment.Create(
            tenantId,
            currentUser.UserId,
            request.TraineeId,
            published.Id,
            published.Version,
            request.StartDate,
            request.FrequencyDaysPerWeek,
            request.VisibilityMode,
            request.HideExercises,
            request.HideSetsReps,
            request.HideFutureWorkouts,
            request.DisableTraineeEditing,
            request.SnapshotJson);

        await assignmentRepository.AddAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(assignment.Id);
    }
}
