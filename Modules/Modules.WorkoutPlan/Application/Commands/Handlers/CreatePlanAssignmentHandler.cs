using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class CreatePlanAssignmentHandler(
    IPlanAssignmentRepository assignmentRepository,
    IWorkoutPlanRepository workoutPlanRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<CreatePlanAssignmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlanAssignmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var plan = await workoutPlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Workout plan not found."));

        var assignment = PlanAssignment.Create(
            tenantId,
            currentUser.UserId,
            request.TraineeId,
            request.PlanId,
            plan.Version,
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
