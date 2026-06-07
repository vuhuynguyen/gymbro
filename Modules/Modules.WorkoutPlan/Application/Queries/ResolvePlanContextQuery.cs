using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

/// <summary>
/// Resolves program context (name, start date, frequency) for a set of plan assignments, keyed by
/// assignment id. Used by the workout-session module to label sessions with their program and week.
/// </summary>
public sealed record ResolvePlanContextQuery(IReadOnlyList<Guid> PlanAssignmentIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, PlanContextDto>>>;
