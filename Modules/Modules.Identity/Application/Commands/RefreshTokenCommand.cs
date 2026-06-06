using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.IdentityModule.Application.Models;

namespace Modules.IdentityModule.Application.Commands;

/// <summary>
/// Exchange a valid refresh token (read from the httpOnly cookie by the controller) for a new
/// access token and a rotated refresh token. <see cref="RawToken"/> is never accepted from the body.
/// </summary>
public record RefreshTokenCommand(string RawToken) : IRequest<Result<TokenPair>>
{
    public string? Ip { get; init; }
}
