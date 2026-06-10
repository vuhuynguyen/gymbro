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

        // Load the plan with structure (tenant-filtered) so we can snapshot it at assign time.
        var plan = await planRepository.GetForUpdateAsync(request.PlanId, cancellationToken);
        if (plan == null)
            return Result<Guid>.Failure(NotFound("NotFound", "Nutrition plan not found."));

        // A plan may only be assigned to a member of this tenant (any member, so an Owner self-assign works).
        var traineeRole = await roleResolver.GetRoleAsync(request.TraineeId, tenantId, cancellationToken);
        if (traineeRole == null)
            return Result<Guid>.Failure(
                Validation("NutritionAssignment.TraineeNotMember", "Trainee is not a member of this tenant."));

        // No duplicate live assignment of the same plan to the same trainee (mirrors the unique partial index).
        var alreadyAssigned = await assignmentRepository.Query()
            .AnyAsync(a => a.TraineeId == request.TraineeId && a.PlanId == request.PlanId, cancellationToken);
        if (alreadyAssigned)
            return Result<Guid>.Failure(Conflict("Conflict", "This plan is already assigned to this trainee."));

        var snapshotJson = NutritionMapping.SerializeSnapshot(NutritionMapping.BuildSnapshot(plan));

        var assignment = NutritionPlanAssignment.Create(
            tenantId,
            currentUser.UserId,
            request.TraineeId,
            request.PlanId,
            plan.Version,
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
