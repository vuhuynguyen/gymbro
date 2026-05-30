using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record DeleteSetCommand(
    Guid SessionId,
    Guid ExerciseId,
    Guid SetId) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
