using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

public sealed record GetActiveSessionQuery : IRequest<Result<ActiveSessionDto?>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WorkoutLogViewOwn;
}
