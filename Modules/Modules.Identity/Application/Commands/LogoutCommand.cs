using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

/// <summary>Revoke the presented refresh token (and its family). Idempotent — unknown tokens succeed quietly.</summary>
public record LogoutCommand(string? RawToken) : IRequest<Result>;
