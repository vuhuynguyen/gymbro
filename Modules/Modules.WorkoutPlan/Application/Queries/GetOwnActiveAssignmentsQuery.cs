using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.DTOs;

namespace Modules.WorkoutPlanModule.Application.Queries;

/// <summary>
/// Lists the caller's OWN active (non-deleted) plan assignments across every gym, for the self-scoped
/// adherence-goal lookup (Decision D1). Mirrors <see cref="ResolveOwnPlanContextQuery"/>: deliberately
/// bypasses the EF tenant filter but is scoped to <see cref="TraineeId"/>, so it only ever reveals
/// assignments belonging to the caller. Internal lookup — only dispatched in-process by the self-scoped
/// progress read model, never from a controller.
/// </summary>
public sealed record GetOwnActiveAssignmentsQuery(Guid TraineeId)
    : IRequest<Result<IReadOnlyList<OwnActiveAssignmentDto>>>;
