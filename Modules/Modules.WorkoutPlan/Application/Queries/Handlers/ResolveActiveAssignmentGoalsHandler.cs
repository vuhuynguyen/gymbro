using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

/// <summary>
/// Resolves the in-gym active-assignment weekly goal per trainee for the coach roster. Reads the
/// <see cref="IPlanAssignmentRepository"/> with the EF tenant filter ON (no <c>IgnoreQueryFilters</c>), so it
/// returns only assignments in the caller's active gym — the coach's own-gym adherence denominator, never a
/// cross-gym goal. When a trainee has more than one active in-gym assignment, the most-recent by
/// <c>StartDate</c> wins (mirrors the D1 tie-break). A trainee with no active in-gym assignment is absent from
/// the result (the roster then surfaces a null goal + null adherence).
/// </summary>
public sealed class ResolveActiveAssignmentGoalsHandler(IPlanAssignmentRepository assignmentRepository)
    : IRequestHandler<ResolveActiveAssignmentGoalsQuery, Result<IReadOnlyDictionary<Guid, int>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, int>>> Handle(
        ResolveActiveAssignmentGoalsQuery request,
        CancellationToken cancellationToken)
    {
        var traineeIds = request.TraineeIds.Distinct().Where(id => id != Guid.Empty).ToList();
        if (traineeIds.Count == 0)
            return Result<IReadOnlyDictionary<Guid, int>>.Success(new Dictionary<Guid, int>());

        // Tenant-filtered (filter ON) active assignments for these trainees in the active gym.
        var rows = await assignmentRepository.Query()
            .AsNoTracking()
            .Where(a => traineeIds.Contains(a.TraineeId) && a.IsActive)
            .Select(a => new { a.TraineeId, a.FrequencyDaysPerWeek, a.StartDate })
            .ToListAsync(cancellationToken);

        var goals = rows
            .GroupBy(r => r.TraineeId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.StartDate).First().FrequencyDaysPerWeek);

        return Result<IReadOnlyDictionary<Guid, int>>.Success(goals);
    }
}
