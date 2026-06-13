using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class CreateNutritionAssignmentHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    INutritionPlanRepository planRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantRoleResolver roleResolver)
    : IRequestHandler<CreateNutritionAssignmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateNutritionAssignmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var plan = await planRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        // Always pin the latest PUBLISHED version, never a draft head. Trainees only ever receive published versions.
        var published = await planRepository.GetLatestPublishedVersionInTemplateAsync(plan.TemplateId, cancellationToken);
        if (published == null)
            return Result<Guid>.Failure(Conflict("Conflict", "Publish the plan before assigning it."));

        if (published.IsArchived)
            return Result<Guid>.Failure(Conflict("Conflict", "This plan is archived and cannot be assigned."));

        // A plan may only be assigned to a member of this tenant (any member, so an Owner self-assign works).
        var traineeRole = await roleResolver.GetRoleAsync(request.TraineeId, tenantId, cancellationToken);
        if (traineeRole == null)
            return Result<Guid>.Failure(
                Validation("NutritionAssignment.TraineeNotMember", "Trainee is not a member of this tenant."));

        // No duplicate live assignment of the same published version to the same trainee (mirrors the unique index).
        var alreadyAssigned = await assignmentRepository.Query()
            .AnyAsync(a => a.TraineeId == request.TraineeId && a.PlanId == published.Id, cancellationToken);
        if (alreadyAssigned)
            return Result<Guid>.Failure(Conflict("Conflict", "This plan is already assigned to this trainee."));

        // Load the published version with its structure (tenant-filtered) to snapshot it at assign time.
        var publishedWithStructure = await planRepository.GetForUpdateAsync(published.Id, cancellationToken);
        if (publishedWithStructure == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        var snapshotJson = NutritionMapping.SerializeSnapshot(NutritionMapping.BuildSnapshot(publishedWithStructure));

        var assignment = NutritionPlanAssignment.Create(
            tenantId,
            currentUser.UserId,
            request.TraineeId,
            published.Id,
            published.Version,
            request.StartDate,
            request.EndDate,
            request.VisibilityMode,
            request.HideMacroTargets,
            request.DisableTraineeEditing,
            snapshotJson);

        await assignmentRepository.AddAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(assignment.Id);
    }
}
