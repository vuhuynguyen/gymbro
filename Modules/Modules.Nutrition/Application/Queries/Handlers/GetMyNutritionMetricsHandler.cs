using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>The caller's own check-in metrics for a date (self-scoped to currentUser.UserId), newest first.</summary>
public sealed class GetMyNutritionMetricsHandler(
    IMetricEntryRepository metricRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyNutritionMetricsQuery, Result<MetricEntryListDto>>
{
    public async Task<Result<MetricEntryListDto>> Handle(GetMyNutritionMetricsQuery request, CancellationToken cancellationToken)
    {
        var localDate = request.Date ?? LocalDayResolver.LocalDateOf(DateTimeOffset.UtcNow, currentUser.TimeZoneId);

        var entries = await metricRepository.GetOwnForDateAsync(currentUser.UserId, localDate, cancellationToken);

        // Defensive re-order: the wire contract is newest-first regardless of the repository's ordering.
        var items = entries
            .OrderByDescending(e => e.LoggedAtUtc)
            .Select(NutritionMapping.ToMetricDto)
            .ToList();

        return Result<MetricEntryListDto>.Success(new MetricEntryListDto(items));
    }
}
