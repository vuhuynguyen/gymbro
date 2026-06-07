using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

public record PromoteUserToAdminCommand(string Email, bool IsAdmin) : IRequest<Result>, IPlatformAdminRequest;
