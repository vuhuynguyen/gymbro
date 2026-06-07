using BuildingBlocks.Shared.Results;
using MediatR;
using BuildingBlocks.Application.Authorization;

namespace Modules.UserModule.Application.Commands;

public record GenerateInviteCommand : IRequest<Result<string>>, ITenantAuthorizedRequest
{
    public Permission RequiredPermission => Permission.InviteCreate;
}
