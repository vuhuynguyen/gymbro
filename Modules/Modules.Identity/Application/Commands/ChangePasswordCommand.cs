using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;
