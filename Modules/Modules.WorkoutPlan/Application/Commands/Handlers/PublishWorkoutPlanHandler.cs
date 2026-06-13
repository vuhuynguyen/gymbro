using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.Authorization;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

/// <summary>
/// Publishes the plan's draft head. This is the only action that advances the published version trainees and
/// assignments see — ordinary edits keep replacing the draft in place.
/// </summary>
public sealed class PublishWorkoutPlanHandler(
    IWorkoutPlanRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<PublishWorkoutPlanCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PublishWorkoutPlanCommand request, CancellationToken cancellationToken)
    {
        var current = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (current == null)
            return Result<int>.Failure(NotFound("NotFound", "Plan not found."));

        if (current.IsArchived)
            return Result<int>.Failure(Conflict("Conflict", "Unarchive the plan before publishing it."));

        var head = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;
        if (current.Id != head.Id)
            return Result<int>.Failure(Conflict(
                "Conflict", "This is not the latest version of the plan. Refresh and publish the latest version."));

        var authorCheck = PlanAuthorPolicy.EnsureCanMutate(head, currentUser);
        if (authorCheck.IsFailure)
            return Result<int>.Failure(authorCheck.Error);

        if (!head.IsDraft)
            return Result<int>.Failure(Conflict("Conflict", "There are no unpublished changes to publish."));

        head.Publish();

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent publish already took this version number (the published-only unique index rejected it).
            return Result<int>.Failure(Conflict("Conflict", "This plan was modified concurrently. Refresh and try again."));
        }

        return Result<int>.Success(head.Version);
    }
}
