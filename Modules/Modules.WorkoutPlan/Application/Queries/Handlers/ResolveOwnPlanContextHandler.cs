using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

/// <summary>
/// Resolves program context for the caller's OWN plan assignments across all gyms. Mirrors
/// <see cref="ResolvePlanContextHandler"/> but deliberately bypasses the EF tenant filter
/// (<c>IgnoreQueryFilters</c>) and re-applies soft-delete, scoped to assignments where
/// <c>TraineeId == request.TraineeId</c> — so it only reveals programs assigned to the caller. Resolved
/// in two id-keyed lookups (assignments, then their plans) rather than a cross-source join, so each
/// query is single-source and the filter bypass is unambiguous.
/// </summary>
public sealed class ResolveOwnPlanContextHandler(
    IPlanAssignmentRepository assignmentRepository,
    IWorkoutPlanRepository planRepository)
    : IRequestHandler<ResolveOwnPlanContextQuery, Result<IReadOnlyDictionary<Guid, PlanContextDto>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, PlanContextDto>>> Handle(
        ResolveOwnPlanContextQuery request,
        CancellationToken cancellationToken)
    {
        var ids = request.PlanAssignmentIds.Distinct().Where(id => id != Guid.Empty).ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyDictionary<Guid, PlanContextDto>>.Success(
                new Dictionary<Guid, PlanContextDto>());

        // Only the caller's own assignments, across every gym (tenant filter off, soft-delete re-applied).
        var assignments = await assignmentRepository.Query()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.TraineeId == request.TraineeId && !a.IsDeleted)
            .Select(a => new { a.Id, a.PlanId, a.StartDate, a.FrequencyDaysPerWeek })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return Result<IReadOnlyDictionary<Guid, PlanContextDto>>.Success(
                new Dictionary<Guid, PlanContextDto>());

        var planIds = assignments.Select(a => a.PlanId).Distinct().ToList();
        var plans = await planRepository.Query()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => planIds.Contains(p.Id) && !p.IsDeleted)
            .Select(p => new { p.Id, p.Name, p.DurationWeeks })
            .ToListAsync(cancellationToken);

        var planById = plans.ToDictionary(p => p.Id);

        var map = new Dictionary<Guid, PlanContextDto>();
        foreach (var a in assignments)
            if (planById.TryGetValue(a.PlanId, out var p))
                map[a.Id] = new PlanContextDto(p.Name, a.StartDate, a.FrequencyDaysPerWeek, p.DurationWeeks);

        return Result<IReadOnlyDictionary<Guid, PlanContextDto>>.Success(map);
    }
}
