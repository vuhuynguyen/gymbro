using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.UserModule.Application.Commands;

public record RevokeInviteCommand(string Code) : IRequest<Result>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.InviteCreate;
}
