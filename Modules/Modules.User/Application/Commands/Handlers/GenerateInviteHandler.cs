using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Commands.Handlers;

public class GenerateInviteHandler(
    IInviteRepository inviteRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GenerateInviteCommand, Result<string>>
{
    public async Task<Result<string>> Handle(GenerateInviteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var invite = Invite.CreateForTenant(tenantId, DateTimeOffset.UtcNow.AddDays(7));

        await inviteRepository.AddAsync(invite, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(invite.Code);
    }
}
