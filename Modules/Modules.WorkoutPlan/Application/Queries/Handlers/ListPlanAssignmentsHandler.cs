using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class ListPlanAssignmentsHandler(
    IPlanAssignmentRepository assignmentRepository,
    IWorkoutPlanRepository workoutPlanRepository,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<ListPlanAssignmentsQuery, Result<PlanAssignmentListDto>>
{
    public async Task<Result<PlanAssignmentListDto>> Handle(
        ListPlanAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var canViewAllAssignments =
            await tenantAuth.HasPermissionAsync(tenantId, Permission.WorkoutLogViewAll, cancellationToken);

        if (!canViewAllAssignments)
        {
            if (request.TraineeId.HasValue && request.TraineeId.Value != currentUser.UserId)
            {
                return Result<PlanAssignmentListDto>.Failure(
                    Unauthorized("Unauthorized", "You can only view your own plan assignments."));
            }
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        var latestVersionByPlan = workoutPlanRepository.Query()
            .GroupBy(p => p.TemplateId)
            .Select(g => new
            {
                TemplateId = g.Key,
                LatestVersion = g.Max(x => x.Version)
            });

        var assignmentsWithTemplate = assignmentRepository.Query()
            .Join(
                workoutPlanRepository.Query(),
                assignment => assignment.PlanId,
                plan => plan.Id,
                (assignment, plan) => new
                {
                    Assignment = assignment,
                    plan.TemplateId
                });

        if (request.TraineeId.HasValue)
            assignmentsWithTemplate = assignmentsWithTemplate.Where(x => x.Assignment.TraineeId == request.TraineeId.Value);
        else if (!canViewAllAssignments)
            assignmentsWithTemplate = assignmentsWithTemplate.Where(x => x.Assignment.TraineeId == currentUser.UserId);

        var totalCount = await assignmentsWithTemplate.CountAsync(cancellationToken);
        var pageRows = await assignmentsWithTemplate
            .OrderByDescending(x => x.Assignment.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                latestVersionByPlan,
                x => x.TemplateId,
                latest => latest.TemplateId,
                (x, latest) => new { x.Assignment, latest })
            .SelectMany(
                x => x.latest.DefaultIfEmpty(),
                (x, latest) => new
                {
                    x.Assignment,
                    LatestVersion = latest != null ? (int?)latest.LatestVersion : null
                })
            .ToListAsync(cancellationToken);

        var rows = pageRows
            .Select(x => WorkoutPlanMapping.ToPlanAssignmentSummaryDto(x.Assignment, x.LatestVersion))
            .ToList();

        return Result<PlanAssignmentListDto>.Success(
            WorkoutPlanMapping.ToPlanAssignmentListDto(rows, page, pageSize, totalCount));
    }
}
