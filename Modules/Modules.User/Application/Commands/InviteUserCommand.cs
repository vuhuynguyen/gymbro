using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.UserModule.Application.Commands;

public record InviteUserCommand(string Email) : IRequest<Result<string>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.InviteCreate;
}
