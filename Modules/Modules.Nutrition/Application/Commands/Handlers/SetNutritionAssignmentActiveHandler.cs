using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Plans;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>Pause (deactivate) or resume (reactivate) an assignment. Shares <see cref="PlanAssignmentLifecycle"/>
/// with the workout module.</summary>
public sealed class SetNutritionAssignmentActiveHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetNutritionAssignmentActiveCommand, Result>
{
    public Task<Result> Handle(SetNutritionAssignmentActiveCommand request, CancellationToken cancellationToken) =>
        PlanAssignmentLifecycle.SetActiveAsync<NutritionPlanAssignment>(
            assignmentRepository.GetByIdAsync, unitOfWork, request.AssignmentId, request.Active,
            "Nutrition plan assignment not found.", cancellationToken);
}
