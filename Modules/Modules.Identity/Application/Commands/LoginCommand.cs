using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.IdentityModule.Application.Models;

namespace Modules.IdentityModule.Application.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<TokenPair>>
{
    /// <summary>Caller IP, set by the controller for refresh-token audit. Not part of the request body.</summary>
    public string? Ip { get; init; }
}
