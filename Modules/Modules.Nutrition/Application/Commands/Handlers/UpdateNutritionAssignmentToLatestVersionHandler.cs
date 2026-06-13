using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>
/// Re-points a nutrition-plan assignment to the plan's latest PUBLISHED version, rebuilding the pinned snapshot
/// from that version's structure. Mirrors UpdatePlanAssignmentToLatestVersionHandler; nutrition rebuilds the
/// snapshot server-side rather than accepting a client-supplied one.
/// </summary>
public sealed class UpdateNutritionAssignmentToLatestVersionHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    INutritionPlanRepository planRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateNutritionAssignmentToLatestVersionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateNutritionAssignmentToLatestVersionCommand request,
        CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result<bool>.Failure(NotFound("NotFound", "Nutrition plan assignment not found."));

        var currentPlan = await planRepository.GetByIdAsync(assignment.PlanId, cancellationToken);
        if (currentPlan == null)
            return Result<bool>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var latest = await planRepository.GetLatestPublishedVersionInTemplateAsync(currentPlan.TemplateId, cancellationToken);
        if (latest == null)
            return Result<bool>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        if (latest.IsArchived)
            return Result<bool>.Failure(
                Conflict("Conflict", "The plan's latest version is archived and cannot be applied."));

        if (assignment.PlanVersion >= latest.Version)
            return Result<bool>.Success(false);

        // Rebuild the pinned snapshot from the new published version's structure.
        var latestWithStructure = await planRepository.GetForUpdateAsync(latest.Id, cancellationToken);
        if (latestWithStructure == null)
            return Result<bool>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var snapshotJson = NutritionMapping.SerializeSnapshot(NutritionMapping.BuildSnapshot(latestWithStructure));

        assignment.ApplyNewVersion(latest.Id, latest.Version, snapshotJson);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
