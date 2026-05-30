using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;
