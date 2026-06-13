using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Plans;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Authorization;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>
/// Publishes the plan's draft head. This is the only action that advances the published version trainees and
/// assignments see — ordinary edits keep replacing the draft in place. Mirrors the workout publish handler.
/// </summary>
public sealed class PublishNutritionPlanHandler(
    INutritionPlanRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<PublishNutritionPlanCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PublishNutritionPlanCommand request, CancellationToken cancellationToken)
    {
        var current = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (current == null)
            return Result<int>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var head = await repository.GetLatestVersionInTemplateAsync(current.TemplateId, cancellationToken) ?? current;

        var guard = PlanLifecycle.CanPublish(current, head, () => NutritionPlanAuthorPolicy.EnsureCanMutate(head, currentUser));
        if (guard.IsFailure)
            return Result<int>.Failure(guard.Error);

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
