using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.NutritionModule.Application.Commands;

/// <summary>
/// SELF-SCOPED (no tenant context) append to the caller's own body-metric series (daily check-in:
/// weight/sleep/…). Classified ImperativeGuarded in TenantAuthorizationExemptions; the handler stamps
/// owner = currentUser.UserId. <c>LocalDate</c> is the trainee's local date and defaults to UTC today
/// when omitted (mirrors GetMyNutritionTodayQuery).
/// </summary>
public sealed record LogMetricEntryCommand(string Type, decimal Value, string? Unit, DateOnly? LocalDate)
    : IRequest<Result>;
