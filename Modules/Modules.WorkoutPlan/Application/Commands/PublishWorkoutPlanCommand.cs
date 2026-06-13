using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.WorkoutPlanModule.Application.Commands;

/// <summary>
/// Publishes the plan's draft head, turning it into the immutable version trainees and assignments see. This is
/// the only action that advances the published version — plain edits keep replacing the draft in place. Returns
/// the newly published version number.
/// </summary>
public sealed record PublishWorkoutPlanCommand(Guid Id) : IRequest<Result<int>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.PlanUpdate;
}
