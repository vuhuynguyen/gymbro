using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries;

/// <summary>
/// SELF-SCOPED (no tenant context): the caller's own metric entries for one local date (default UTC today),
/// NEWEST FIRST — the client takes the first entry per type as "latest". Classified ImperativeGuarded in
/// TenantAuthorizationExemptions; the handler scopes strictly to currentUser.UserId.
/// </summary>
public sealed record GetMyNutritionMetricsQuery(DateOnly? Date) : IRequest<Result<MetricEntryListDto>>;

/// <summary>
/// SELF-SCOPED (no tenant context): the caller's own body-metric trend (latest-per-local-day) for one
/// <paramref name="Type"/> over a local-date range (default: trailing 12 weeks). <paramref name="Type"/> is
/// matched case-insensitively (it is unvalidated free text). Classified ImperativeGuarded in
/// TenantAuthorizationExemptions; the handler scopes strictly to currentUser.UserId. Empty range ⇒ 200 with
/// empty Points, never 404.
/// </summary>
public sealed record GetMyMetricSeriesQuery(string Type, DateOnly? From, DateOnly? To)
    : IRequest<Result<MetricSeriesDto>>;
