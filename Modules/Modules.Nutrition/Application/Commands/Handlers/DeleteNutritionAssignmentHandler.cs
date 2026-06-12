using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>Revokes (soft-deletes) an assignment. Mirrors DeletePlanAssignmentHandler.</summary>
public sealed class DeleteNutritionAssignmentHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteNutritionAssignmentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteNutritionAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result<bool>.Failure(NotFound("NotFound", "Nutrition plan assignment not found."));

        assignmentRepository.Remove(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
