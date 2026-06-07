using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Admin.Commands;

public record AdminRemoveMemberCommand(Guid TenantId, Guid UserId) : IRequest<Result>, IPlatformAdminRequest;
