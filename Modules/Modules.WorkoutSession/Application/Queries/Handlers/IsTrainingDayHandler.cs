using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Shared.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// Answers <see cref="IsTrainingDayQuery"/>: a date is a training day if the user started a workout session whose
/// instant falls within that local calendar day (resolved in the trainee's captured zone, UTC fallback).
/// Self-scoped across every gym. The answer drives nutrition's training/rest-day meal recurrence; a missing
/// signal naturally returns false (rest day).
/// </summary>
public sealed class IsTrainingDayHandler(IWorkoutSessionRepository sessionRepository)
    : IRequestHandler<IsTrainingDayQuery, bool>
{
    public async Task<bool> Handle(IsTrainingDayQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = LocalDayResolver.StartOfLocalDayUtc(request.LocalDate, request.Timezone);
        var toExclusiveUtc = LocalDayResolver.StartOfLocalDayUtc(request.LocalDate.AddDays(1), request.Timezone);

        return await sessionRepository.QueryOwnAcrossGyms(request.UserId)
            .AnyAsync(s => s.StartedAt >= fromUtc && s.StartedAt < toExclusiveUtc, cancellationToken);
    }
}
