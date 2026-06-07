using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Mapping;
using Modules.WorkoutPlanModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class GetWorkoutPlanByIdHandler(
    IWorkoutPlanRepository repository,
    IMediator mediator,
    IPlanAssignmentRepository assignmentRepository,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<GetWorkoutPlanByIdQuery, Result<WorkoutPlanDetailDto>>
{
    public async Task<Result<WorkoutPlanDetailDto>> Handle(
        GetWorkoutPlanByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var plan = await repository.Query()
            .AsNoTracking()
            .Include(p => p.Workouts)
            .ThenInclude(w => w.Exercises)
            .ThenInclude(e => e.PrescribedSets)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result<WorkoutPlanDetailDto>.Failure(NotFound("NotFound", "Plan not found."));

        var canViewAllTemplates =
            await tenantAuth.HasPermissionAsync(tenantId, Permission.PlanViewAll, cancellationToken);

        // Clients may only see plans they are assigned, and the assignment's visibility flags govern
        // what the trainee sees (Guided mode). Coaches/admins (canViewAllTemplates) see the full plan.
        PlanAssignment? traineeAssignment = null;
        if (!canViewAllTemplates)
        {
            traineeAssignment = await assignmentRepository.Query()
                .FirstOrDefaultAsync(
                    a => a.TraineeId == currentUser.UserId && a.PlanId == plan.Id,
                    cancellationToken);
            if (traineeAssignment == null)
                return Result<WorkoutPlanDetailDto>.Failure(NotFound("NotFound", "Plan not found."));
        }

        var exerciseIds = plan.Workouts
            .SelectMany(w => w.Exercises)
            .Select(e => e.ExerciseId)
            .Distinct()
            .ToList();

        var namesResult = await mediator.Send(new ResolveExerciseNamesQuery(exerciseIds), cancellationToken);
        if (namesResult.IsFailure)
            return Result<WorkoutPlanDetailDto>.Failure(namesResult.Error);

        var dto = WorkoutPlanMapping.ToWorkoutPlanDetailDto(plan, namesResult.Value!);

        if (traineeAssignment != null)
            dto = WorkoutPlanMapping.RedactForTrainee(dto, traineeAssignment);

        return Result<WorkoutPlanDetailDto>.Success(dto);
    }
}
