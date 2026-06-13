using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;

namespace Modules.NutritionModule.Application.Queries.Handlers;

public sealed class ListNutritionAssignmentsHandler(
    INutritionPlanAssignmentRepository assignmentRepository,
    INutritionPlanRepository planRepository)
    : IRequestHandler<ListNutritionAssignmentsQuery, Result<NutritionAssignmentListDto>>
{
    public async Task<Result<NutritionAssignmentListDto>> Handle(ListNutritionAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

        // Latest PUBLISHED version per template drives the "New vX" badge (a draft head must not light it up).
        var latestPublishedByTemplate = planRepository.Query()
            .Where(p => !p.IsDraft)
            .GroupBy(p => p.TemplateId)
            .Select(g => new { TemplateId = g.Key, LatestVersion = g.Max(x => x.Version) });

        // Tenant-filtered join to the plan for the display name + template (both queries are gym-scoped by the filter).
        var query = assignmentRepository.Query()
            .Join(planRepository.Query(),
                a => a.PlanId,
                p => p.Id,
                (a, p) => new { Assignment = a, p.Name, p.TemplateId });

        if (request.TraineeId.HasValue)
            query = query.Where(x => x.Assignment.TraineeId == request.TraineeId.Value);
        if (request.ActiveOnly)
            query = query.Where(x => x.Assignment.IsActive);

        var totalCount = await query.CountAsync(cancellationToken);

        // Project to a flat row (no jsonb snapshot) and resolve the version-sync flags in memory.
        var pageRows = await query
            .OrderByDescending(x => x.Assignment.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                latestPublishedByTemplate,
                x => x.TemplateId,
                latest => latest.TemplateId,
                (x, latest) => new { x.Assignment, x.Name, latest })
            .SelectMany(
                x => x.latest.DefaultIfEmpty(),
                (x, latest) => new
                {
                    x.Assignment.Id,
                    x.Assignment.TraineeId,
                    x.Assignment.PlanId,
                    x.Assignment.PlanVersion,
                    x.Name,
                    x.Assignment.StartDate,
                    x.Assignment.EndDate,
                    x.Assignment.VisibilityMode,
                    x.Assignment.HideMacroTargets,
                    x.Assignment.DisableTraineeEditing,
                    x.Assignment.IsActive,
                    LatestVersion = latest != null ? (int?)latest.LatestVersion : null
                })
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(r => new NutritionAssignmentSummaryDto(
                r.Id,
                r.TraineeId,
                r.PlanId,
                r.PlanVersion,
                r.LatestVersion ?? r.PlanVersion,
                r.LatestVersion.HasValue && r.PlanVersion < r.LatestVersion.Value,
                r.Name,
                r.StartDate,
                r.EndDate,
                r.VisibilityMode,
                r.HideMacroTargets,
                r.DisableTraineeEditing,
                r.IsActive))
            .ToList();

        return Result<NutritionAssignmentListDto>.Success(new NutritionAssignmentListDto(items, page, pageSize, totalCount));
    }
}
