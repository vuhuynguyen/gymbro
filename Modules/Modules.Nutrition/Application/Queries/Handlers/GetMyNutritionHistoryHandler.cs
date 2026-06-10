using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>
/// The caller's nutrition history across every gym (self-scoped, via QueryOwnAcrossGyms). Mirrors
/// GetMyWorkoutHistory.
/// </summary>
public sealed class GetMyNutritionHistoryHandler(
    IDailyNutritionLogRepository logRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyNutritionHistoryQuery, Result<DailyNutritionLogListDto>>
{
    public async Task<Result<DailyNutritionLogListDto>> Handle(GetMyNutritionHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 30 : Math.Min(request.PageSize, 100);

        var query = logRepository.QueryOwnAcrossGyms(currentUser.UserId).AsNoTracking();

        if (request.From.HasValue)
            query = query.Where(l => l.LocalDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(l => l.LocalDate <= request.To.Value);

        var total = await query.CountAsync(cancellationToken);

        // Counts computed in SQL — neither the item rows nor the jsonb snapshot are loaded for a list.
        var rows = await query
            .OrderByDescending(l => l.LocalDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(NutritionMapping.SummaryRowProjection)
            .ToListAsync(cancellationToken);

        var items = rows.Select(NutritionMapping.ToSummaryDto).ToList();
        return Result<DailyNutritionLogListDto>.Success(new DailyNutritionLogListDto(items, page, pageSize, total));
    }
}
