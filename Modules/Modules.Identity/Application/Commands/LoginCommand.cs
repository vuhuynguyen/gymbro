using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<string>>;
