using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>
/// Archive/unarchive a nutrition plan template. Mirrors SetWorkoutPlanArchivedHandler; the row-level tenant
/// check is the EF tenant filter applied through the repository (as the other nutrition plan handlers do —
/// nutrition has no per-row author policy).
/// </summary>
public sealed class SetNutritionPlanArchivedHandler(
    INutritionPlanRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetNutritionPlanArchivedCommand, Result>
{
    public async Task<Result> Handle(SetNutritionPlanArchivedCommand request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (plan == null)
            return Result.Failure(NotFound("NotFound", "Nutrition plan not found."));

        plan.SetArchived(request.Archived);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
