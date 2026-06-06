using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
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

        if (current.IsArchived)
            return Result.Failure(Conflict("Conflict", "Unarchive the plan before editing it."));

        var latest = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;

        // Edits must target the latest version; editing an older version would silently fork off the
        // newest one and discard the caller's intent. Make that explicit instead.
        if (current.Id != latest.Id)
            return Result.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and edit the latest version."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(latest, currentUser);
        if (authorCheck.IsFailure)
            return authorCheck;

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
