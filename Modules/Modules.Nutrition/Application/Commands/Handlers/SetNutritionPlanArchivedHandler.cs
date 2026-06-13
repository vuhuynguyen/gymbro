using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Authorization;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>
/// Archive/unarchive a nutrition plan template. Mirrors SetWorkoutPlanArchivedHandler: the EF tenant filter
/// scopes the load to the gym, and the row-level author policy restricts mutation to the plan's author (or a
/// platform admin), matching the workout module.
/// </summary>
public sealed class SetNutritionPlanArchivedHandler(
    INutritionPlanRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<SetNutritionPlanArchivedCommand, Result>
{
    public async Task<Result> Handle(SetNutritionPlanArchivedCommand request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (plan == null)
            return Result.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var authorCheck = NutritionPlanAuthorPolicy.EnsureCanMutate(plan, currentUser);
        if (authorCheck.IsFailure)
            return authorCheck;

        plan.SetArchived(request.Archived);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
