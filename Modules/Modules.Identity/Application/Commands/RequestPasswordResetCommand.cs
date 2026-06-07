using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

public sealed record RequestPasswordResetCommand(string Email) : IRequest<Result>;
