using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>Edits an assignment's configuration in place, keeping the pinned version + snapshot.
/// Mirrors UpdatePlanAssignmentHandler (tenant-filtered repo lookup).</summary>
public sealed class UpdateNutritionAssignmentHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateNutritionAssignmentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateNutritionAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result<bool>.Failure(NotFound("NotFound", "Nutrition plan assignment not found."));

        assignment.UpdateConfiguration(
            request.StartDate,
            request.EndDate,
            request.VisibilityMode,
            request.HideMacroTargets,
            request.DisableTraineeEditing);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
