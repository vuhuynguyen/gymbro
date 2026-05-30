using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class DeleteWorkoutPlanHandler(
    IWorkoutPlanRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteWorkoutPlanCommand, Result>
{
    public async Task<Result> Handle(DeleteWorkoutPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (plan == null)
            return Result.Failure(NotFound("NotFound", "Plan not found."));

        plan.MarkDeleted();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
