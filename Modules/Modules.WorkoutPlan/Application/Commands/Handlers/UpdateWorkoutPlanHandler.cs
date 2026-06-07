using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class UpdateWorkoutPlanHandler(
    IWorkoutPlanRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateWorkoutPlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpdateWorkoutPlanCommand request, CancellationToken cancellationToken)
    {
        var current = await repository.GetForUpdateAsync(request.Id, cancellationToken);
        if (current == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Plan not found."));

        if (current.IsArchived)
            return Result<Guid>.Failure(Conflict("Conflict", "Unarchive the plan before editing it."));

        var latest = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;

        // Edits must target the latest version; editing an older version would silently fork off the
        // newest one and discard the caller's intent. Make that explicit instead.
        if (current.Id != latest.Id)
            return Result<Guid>.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and edit the latest version."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(latest, currentUser);
        if (authorCheck.IsFailure)
            return Result<Guid>.Failure(authorCheck.Error);

        var next = WorkoutPlan.CreateNewVersion(
            latest,
            currentUser.UserId,
            request.Name,
            request.Description,
            request.DurationWeeks,
            request.WorkoutsPerWeek);

        await repository.AddAsync(next, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent edit created the same version; caller should reload and retry.
            return Result<Guid>.Failure(Conflict("Conflict", "This plan was modified concurrently. Refresh and try again."));
        }

        // Return the new version's id so the caller can re-point to the latest version for its next edit.
        return Result<Guid>.Success(next.Id);
    }
}
