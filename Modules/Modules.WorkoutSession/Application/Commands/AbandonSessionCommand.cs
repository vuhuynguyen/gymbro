using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.WorkoutSessionModule.Application.Commands;

public sealed record AbandonSessionCommand(
    Guid SessionId,
    string? Notes) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogCreate;
}
