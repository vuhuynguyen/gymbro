using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>Pause (deactivate) or resume (reactivate) an assignment. Mirrors SetPlanAssignmentActiveHandler.</summary>
public sealed class SetNutritionAssignmentActiveHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetNutritionAssignmentActiveCommand, Result>
{
    public async Task<Result> Handle(SetNutritionAssignmentActiveCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result.Failure(NotFound("NotFound", "Nutrition plan assignment not found."));

        assignment.SetActive(request.Active);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
