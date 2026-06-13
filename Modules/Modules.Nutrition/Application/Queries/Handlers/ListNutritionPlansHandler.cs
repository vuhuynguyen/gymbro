using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Queries.Handlers;

public sealed class ListNutritionPlansHandler(INutritionPlanRepository repository)
    : IRequestHandler<ListNutritionPlansQuery, Result<NutritionPlanListDto>>
{
    public async Task<Result<NutritionPlanListDto>> Handle(ListNutritionPlansQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);
        var search = request.Search?.Trim();

        // Latest version per template (the EF tenant filter already scopes to the caller's gym).
        var latestPerTemplate = repository.Query()
            .GroupBy(p => p.TemplateId)
            .Select(g => new { TemplateId = g.Key, Version = g.Max(x => x.Version) });

        var query = repository.Query()
            .Join(latestPerTemplate,
                p => new { p.TemplateId, p.Version },
                latest => new { latest.TemplateId, latest.Version },
                (p, _) => p)
            // Archived plans drop out of the default list; pass Archived=true to view the retired ones
            // (mirrors ListWorkoutPlansQuery's default behavior).
            .Where(p => p.IsArchived == request.Archived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(normalized) ||
                (p.Description != null && p.Description.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(NutritionMapping.PlanSummaryProjection) // meal count via subquery — no meal rows loaded
            .ToListAsync(cancellationToken);

        // Patch the latest PUBLISHED version per template so the list (and assign picker) can show publish state.
        var templateIds = items.Select(r => r.TemplateId).Distinct().ToList();
        var latestPublishedByTemplate = await repository.Query()
            .Where(p => templateIds.Contains(p.TemplateId) && !p.IsDraft)
            .GroupBy(p => p.TemplateId)
            .Select(g => new { TemplateId = g.Key, Version = g.Max(x => x.Version) })
            .ToDictionaryAsync(x => x.TemplateId, x => (int?)x.Version, cancellationToken);

        var patched = items
            .Select(r => latestPublishedByTemplate.TryGetValue(r.TemplateId, out var v)
                ? r with { LatestPublishedVersion = v }
                : r)
            .ToList();

        return Result<NutritionPlanListDto>.Success(new NutritionPlanListDto(patched, page, pageSize, totalCount));
    }
}
