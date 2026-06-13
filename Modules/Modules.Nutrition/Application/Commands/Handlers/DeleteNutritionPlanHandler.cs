using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Authorization;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class DeleteNutritionPlanHandler(
    INutritionPlanRepository repository,
    INutritionPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<DeleteNutritionPlanCommand, Result>
{
    public async Task<Result> Handle(DeleteNutritionPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (plan == null)
            return Result.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var authorCheck = NutritionPlanAuthorPolicy.EnsureCanMutate(plan, currentUser);
        if (authorCheck.IsFailure)
            return authorCheck;

        // Don't orphan a live assignment pinned to this version (mirrors the workout-plan delete guard).
        var hasLiveAssignment = await assignmentRepository.Query()
            .AnyAsync(a => a.PlanId == plan.Id, cancellationToken);
        if (hasLiveAssignment)
            return Result.Failure(Conflict(
                "Conflict", "This plan is assigned to a trainee. Revoke the assignment before deleting the plan."));

        // Hard-delete the child structure before soft-deleting the header (the header's soft-delete is an
        // UPDATE, so the DB cascade never fires).
        await repository.ClearPlanStructureAsync(plan.Id, cancellationToken);
        plan.MarkDeleted();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
