using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class UpdatePlanAssignmentHandler(
    IPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePlanAssignmentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdatePlanAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result<bool>.Failure(NotFound("NotFound", "Plan assignment not found."));

        assignment.UpdateConfiguration(
            request.StartDate,
            request.FrequencyDaysPerWeek,
            request.VisibilityMode,
            request.HideExercises,
            request.HideSetsReps,
            request.HideFutureWorkouts,
            request.DisableTraineeEditing);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
