using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Commands;

public record RemoveMemberCommand(Guid TenantId, Guid UserId) : IRequest<Result>;
