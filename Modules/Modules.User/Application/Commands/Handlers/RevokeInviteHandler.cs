using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Commands.Handlers;

public class RevokeInviteHandler(
    IInviteRepository inviteRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeInviteCommand, Result>
{
    public async Task<Result> Handle(RevokeInviteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var invite = await inviteRepository.GetByCodeAndTenantAsync(request.Code, tenantId, cancellationToken);

        if (invite == null)
            return Result.Failure(Error.NotFound("Invite not found."));

        if (invite.IsUsed)
            return Result.Failure(Error.Validation("Invite is already used or revoked."));

        invite.MarkUsed();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
