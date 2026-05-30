using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Admin.Commands;

public record AdminDeleteTenantCommand(Guid TenantId) : IRequest<Result>;
