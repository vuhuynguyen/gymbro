using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;
using Modules.WorkoutPlanModule.Application.Mapping;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class ListWorkoutPlansHandler(
    IWorkoutPlanRepository repository,
    IPlanAssignmentRepository assignmentRepository,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<ListWorkoutPlansQuery, Result<WorkoutPlanListDto>>
{
    public async Task<Result<WorkoutPlanListDto>> Handle(
        ListWorkoutPlansQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);
        var search = request.Search?.Trim();

        var canViewAllTemplates =
            await tenantAuth.HasPermissionAsync(tenantId, Permission.WorkoutLogViewAll, cancellationToken);

        IQueryable<WorkoutPlan> query;

        if (canViewAllTemplates)
        {
            var latestVersionPerTemplate = repository.Query()
                .GroupBy(p => p.TemplateId)
                .Select(g => new
                {
                    TemplateId = g.Key,
                    Version = g.Max(x => x.Version)
                });

            query = repository.Query()
                .Join(
                    latestVersionPerTemplate,
                    p => new { p.TemplateId, p.Version },
                    latest => new { latest.TemplateId, latest.Version },
                    (p, _) => p);
        }
        else
        {
            var assignedPlanIds = assignmentRepository.Query()
                .Where(a => a.TraineeId == currentUser.UserId)
                .Select(a => a.PlanId);

            query = repository.Query().Where(p => assignedPlanIds.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(normalizedSearch) ||
                (p.Description != null && p.Description.ToLower().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(p => p.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(WorkoutPlanMapping.WorkoutPlanSummaryProjection)
            .ToListAsync(cancellationToken);

        return Result<WorkoutPlanListDto>.Success(
            WorkoutPlanMapping.ToWorkoutPlanListDto(rows, page, pageSize, totalCount));
    }
}
