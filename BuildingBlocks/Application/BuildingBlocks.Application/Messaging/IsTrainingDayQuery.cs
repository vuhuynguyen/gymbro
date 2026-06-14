using MediatR;

namespace BuildingBlocks.Application.Messaging;

/// <summary>
/// Cross-module read: did the user train on the given local date? A date is a "training day" if they have a
/// workout session on it — the WorkoutSession module owns the answer. Used by nutrition recurrence
/// (<c>NutritionScheduleRules</c>) to decide which training/rest-day meals apply when a daily log opens.
/// Graceful default <c>false</c> (treat as a rest day) when there is no signal. <c>Timezone</c> is the trainee's
/// IANA zone for resolving the local-day window.
/// </summary>
public sealed record IsTrainingDayQuery(Guid UserId, DateOnly LocalDate, string? Timezone) : IRequest<bool>;
