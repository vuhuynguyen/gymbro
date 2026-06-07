using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Commands;

public record CreateTenantCommand(string Name) : IRequest<Result<Guid>>;
