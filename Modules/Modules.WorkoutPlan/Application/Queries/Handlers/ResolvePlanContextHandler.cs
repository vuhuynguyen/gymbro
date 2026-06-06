using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

public sealed class ResolvePlanContextHandler(
    IPlanAssignmentRepository assignmentRepository,
    IWorkoutPlanRepository planRepository)
    : IRequestHandler<ResolvePlanContextQuery, Result<IReadOnlyDictionary<Guid, PlanContextDto>>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, PlanContextDto>>> Handle(
        ResolvePlanContextQuery request,
        CancellationToken cancellationToken)
    {
        var ids = request.PlanAssignmentIds.Distinct().Where(id => id != Guid.Empty).ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyDictionary<Guid, PlanContextDto>>.Success(
                new Dictionary<Guid, PlanContextDto>());

        // Join assignment → plan (PlanAssignment.PlanId references the specific WorkoutPlan version row).
        var rows = await (
                from a in assignmentRepository.Query().AsNoTracking()
                where ids.Contains(a.Id)
                join p in planRepository.Query().AsNoTracking() on a.PlanId equals p.Id
                select new
                {
                    a.Id,
                    p.Name,
                    a.StartDate,
                    a.FrequencyDaysPerWeek,
                    p.DurationWeeks
                })
            .ToListAsync(cancellationToken);

        var map = rows.ToDictionary(
            r => r.Id,
            r => new PlanContextDto(r.Name, r.StartDate, r.FrequencyDaysPerWeek, r.DurationWeeks));

        return Result<IReadOnlyDictionary<Guid, PlanContextDto>>.Success(map);
    }
}
