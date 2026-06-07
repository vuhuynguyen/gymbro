using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;
using Modules.UserModule.Application.DTOs;

namespace Modules.UserModule.Application.Queries;

public record GetTenantInvitesQuery : IRequest<Result<List<InviteCodeDto>>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.InviteCreate;
}
