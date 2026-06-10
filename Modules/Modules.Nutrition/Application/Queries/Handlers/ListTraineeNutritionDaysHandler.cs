using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;

namespace Modules.NutritionModule.Application.Queries.Handlers;

public sealed class ListTraineeNutritionDaysHandler(IDailyNutritionLogRepository logRepository)
    : IRequestHandler<ListTraineeNutritionDaysQuery, Result<DailyNutritionLogListDto>>
{
    public async Task<Result<DailyNutritionLogListDto>> Handle(ListTraineeNutritionDaysQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 30 : Math.Min(request.PageSize, 100);

        // Tenant-filtered (coach's own gym); scoped to the requested trainee.
        var query = logRepository.Query().Where(l => l.TraineeId == request.TraineeId);
        if (request.From.HasValue)
            query = query.Where(l => l.LocalDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(l => l.LocalDate <= request.To.Value);

        var total = await query.CountAsync(cancellationToken);

        // Counts computed in SQL — no item rows / jsonb snapshot loaded for the coach adherence list.
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
