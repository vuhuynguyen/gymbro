using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// Unified personal progress/analytics across every gym the caller trains in. Self-scoped via
/// <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/> (<c>currentUser.UserId</c>). Per-session
/// aggregates are computed in SQL; weeks are grouped Monday-anchored in memory.
/// </summary>
public sealed class GetMyProgressHandler(
    IWorkoutSessionRepository sessionRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyProgressQuery, Result<ProgressDto>>
{
    public async Task<Result<ProgressDto>> Handle(
        GetMyProgressQuery request,
        CancellationToken cancellationToken)
    {
        var sessions = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .Select(s => new
            {
                s.StartedAt,
                s.Status,
                // Drop/rest-pause stages roll up into their lead set — count only parentless rows.
                TotalSets = s.Exercises.SelectMany(e => e.Sets).Count(set => set.ParentSetId == null),
                Volume = s.Exercises
                    .SelectMany(e => e.Sets)
                    .Where(set => set.SetType == PerformedSetType.Working
                        && set.WeightKg != null && set.Reps != null)
                    .Sum(set => (decimal?)(set.WeightKg!.Value * set.Reps!.Value)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var totalVolume = sessions.Sum(s => s.Volume);
        var totalSets = sessions.Sum(s => s.TotalSets);
        var completed = sessions.Count(s => s.Status == SessionStatus.Completed);

        var weeks = sessions
            .GroupBy(s => WeekStart(s.StartedAt))
            .OrderByDescending(g => g.Key)
            .Select(g => new ProgressWeekDto(
                g.Key,
                g.Count(),
                g.Sum(x => x.TotalSets),
                g.Sum(x => x.Volume)))
            .ToList();

        return Result<ProgressDto>.Success(
            new ProgressDto(sessions.Count, completed, totalVolume, totalSets, weeks));
    }

    // Monday-anchored start of the week containing the given instant (UTC).
    private static DateOnly WeekStart(DateTimeOffset startedAt)
    {
        var date = DateOnly.FromDateTime(startedAt.UtcDateTime);
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offsetFromMonday);
    }
}
