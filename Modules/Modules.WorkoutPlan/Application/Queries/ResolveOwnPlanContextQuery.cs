using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

/// <summary>
/// Cross-gym variant of <see cref="ResolvePlanContextQuery"/> for the unified personal training
/// experience: resolves program context (name, start date, frequency) for the caller's OWN plan
/// assignments across every gym, keyed by assignment id. Deliberately bypasses the tenant filter but is
/// scoped to <see cref="TraineeId"/>, so it only ever reveals programs assigned to the caller. Internal
/// lookup — only dispatched in-process by the self-scoped session read models, never from a controller.
/// </summary>
public sealed record ResolveOwnPlanContextQuery(
    IReadOnlyList<Guid> PlanAssignmentIds,
    Guid TraineeId)
    : IRequest<Result<IReadOnlyDictionary<Guid, PlanContextDto>>>;
