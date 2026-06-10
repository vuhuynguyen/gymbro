using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.DTOs;

namespace Modules.NutritionModule.Application.Queries;

// All three are SELF-SCOPED (no tenant context) — the caller's own nutrition across every gym. Classified
// ImperativeGuarded in TenantAuthorizationExemptions; handlers scope strictly to currentUser.UserId.

/// <summary>
/// Today's nutrition (snapshot-on-touch): returns the caller's log for the date, lazily creating + seeding
/// it from the active assignment on first access, and closing any stale prior open days. <c>Date</c> is the
/// caller's local date (defaults to UTC today when omitted).
/// </summary>
public sealed record GetMyNutritionTodayQuery(DateOnly? Date, string? Timezone)
    : IRequest<Result<DailyNutritionLogDto>>;

public sealed record GetMyNutritionDayQuery(DateOnly Date) : IRequest<Result<DailyNutritionLogDto>>;

public sealed record GetMyNutritionHistoryQuery(DateOnly? From, DateOnly? To, int Page, int PageSize)
    : IRequest<Result<DailyNutritionLogListDto>>;
