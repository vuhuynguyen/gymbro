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
