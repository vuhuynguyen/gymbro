using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

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

        if (assignment.IsCustomized)
            return Result<bool>.Failure(Validation("PlanAssignment.Customized", "Customized assignments cannot be auto-updated."));

        var currentPlan = await workoutPlanRepository.GetByIdAsync(assignment.PlanId, cancellationToken);
        if (currentPlan == null)
            return Result<bool>.Failure(NotFound("NotFound", "Workout plan not found."));

        var latest = await workoutPlanRepository.GetLatestVersionInTemplateAsync(currentPlan.TemplateId, cancellationToken);
        if (latest == null)
            return Result<bool>.Failure(NotFound("NotFound", "Workout plan not found."));

        if (assignment.PlanVersion >= latest.Version)
            return Result<bool>.Success(false);

        assignment.ApplyNewVersion(latest.Id, latest.Version, request.SnapshotJson);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
