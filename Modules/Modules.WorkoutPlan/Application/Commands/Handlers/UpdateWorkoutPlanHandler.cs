using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class UpdateWorkoutPlanHandler(
    IWorkoutPlanRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateWorkoutPlanCommand, Result>
{
    public async Task<Result> Handle(UpdateWorkoutPlanCommand request, CancellationToken cancellationToken)
    {
        var current = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (current == null)
            return Result.Failure(NotFound("NotFound", "Plan not found."));

        var latest = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;
        var next = WorkoutPlan.CreateNewVersion(
            latest,
            currentUser.UserId,
            request.Name,
            request.Description,
            request.DurationWeeks,
            request.WorkoutsPerWeek);

        await repository.AddAsync(next, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
