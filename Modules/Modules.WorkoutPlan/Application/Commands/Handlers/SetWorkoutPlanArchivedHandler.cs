using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class SetWorkoutPlanArchivedHandler(
    IWorkoutPlanRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<SetWorkoutPlanArchivedCommand, Result>
{
    public async Task<Result> Handle(SetWorkoutPlanArchivedCommand request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (plan == null)
            return Result.Failure(NotFound("NotFound", "Plan not found."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(plan, currentUser);
        if (authorCheck.IsFailure)
            return authorCheck;

        plan.SetArchived(request.Archived);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
