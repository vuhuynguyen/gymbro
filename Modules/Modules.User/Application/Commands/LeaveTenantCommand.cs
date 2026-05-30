using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Commands;

public record LeaveTenantCommand(Guid TenantId) : IRequest<Result>;
