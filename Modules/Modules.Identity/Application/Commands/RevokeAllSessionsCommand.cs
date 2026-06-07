using BuildingBlocks.Shared.Results;
using MediatR;

namespace Modules.IdentityModule.Application.Commands;

/// <summary>
/// "Log out everywhere" for the current user: revoke all refresh tokens and rotate the SecurityStamp
/// so every live access token is rejected on its next request (Tier 2 revocation).
/// </summary>
public record RevokeAllSessionsCommand : IRequest<Result>;
