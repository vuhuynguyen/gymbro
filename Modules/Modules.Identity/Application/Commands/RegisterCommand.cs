using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<Result<string>>;
