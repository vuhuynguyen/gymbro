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

        var head = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;

        // Edits must target the latest version; editing an older version would silently fork off the
        // newest one and discard the caller's intent. Make that explicit instead.
        if (current.Id != head.Id)
            return Result<Guid>.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and edit the latest version."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(head, currentUser);
        if (authorCheck.IsFailure)
            return Result<Guid>.Failure(authorCheck.Error);

        // Edits never bump the published version: they land on the single draft head. Replacing an existing
        // draft keeps its version number (and drops the old draft row); the first edit after a publish forks a
        // new draft one above the latest published version. Structure is deep-copied so a metadata-only edit
        // preserves the workouts.
        var draftVersion = head.IsDraft ? head.Version : head.Version + 1;
        var next = WorkoutPlan.CreateDraft(
            head,
            currentUser.UserId,
            draftVersion,
            request.Name,
            request.Description,
            request.DurationWeeks,
            request.WorkoutsPerWeek);

        if (head.IsDraft)
            repository.Remove(head);
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

        // Return the draft head's id so the caller can re-point to the latest version for its next edit.
        return Result<Guid>.Success(next.Id);
    }
}
