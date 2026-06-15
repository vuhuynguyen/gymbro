using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries;

/// <summary>
/// SELF-SCOPED (no tenant context): the caller's own nutrition-plan adherence over a local-date range
/// (default: trailing 4 weeks). Reads only the caller's own planned daily logs across every gym via
/// <c>QueryOwnAcrossGyms(currentUser.UserId)</c>. Classified ImperativeGuarded in
/// TenantAuthorizationExemptions; the handler scopes strictly to currentUser.UserId. No planned day ever
/// logged ⇒ 200 with <c>HasPlan=false</c>, empty Days, null avg — never 404.
/// </summary>
public sealed record GetMyNutritionAdherenceQuery(DateOnly? From, DateOnly? To)
    : IRequest<Result<NutritionAdherenceDto>>;
