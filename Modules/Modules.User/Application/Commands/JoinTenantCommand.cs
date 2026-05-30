using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Commands;

public record JoinTenantCommand(string Code) : IRequest<Result<Guid>>;
