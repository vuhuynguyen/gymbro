using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.IdentityModule.Infrastructure.Services;

namespace Modules.IdentityModule.Application.Commands.Handlers;

public class LogoutHandler(RefreshTokenService refreshTokenService)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RawToken))
            await refreshTokenService.RevokeAsync(request.RawToken, cancellationToken);

        return Result.Success();
    }
}
