using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries.Handlers;

/// <summary>
/// Resolves the caller's OWN active plan assignments across all gyms. Mirrors
/// <see cref="ResolveOwnPlanContextHandler"/>: it deliberately bypasses the EF tenant filter
/// (<c>IgnoreQueryFilters</c>) and re-applies soft-delete, scoped to <c>TraineeId == request.TraineeId</c>,
/// so it only ever reveals assignments belonging to the caller — never another trainee's. Returns only
/// <c>IsActive</c> assignments (paused ones are excluded from the adherence goal, matching the start-workout
/// picker). The progress overview consumes this to pick the authoritative goal (Decision D1).
/// </summary>
public sealed class GetOwnActiveAssignmentsHandler(IPlanAssignmentRepository assignmentRepository)
    : IRequestHandler<GetOwnActiveAssignmentsQuery, Result<IReadOnlyList<OwnActiveAssignmentDto>>>
{
    public async Task<Result<IReadOnlyList<OwnActiveAssignmentDto>>> Handle(
        GetOwnActiveAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var assignments = await assignmentRepository.Query()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.TraineeId == request.TraineeId && a.IsActive && !a.IsDeleted)
            .Select(a => new OwnActiveAssignmentDto(
                a.Id,
                a.TenantId!.Value,
                a.FrequencyDaysPerWeek,
                a.StartDate))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<OwnActiveAssignmentDto>>.Success(assignments);
    }
}
