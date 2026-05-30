using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.UserModule.Application.Admin.Commands;

public record AdminDeleteUserCommand(Guid UserId) : IRequest<Result>;
