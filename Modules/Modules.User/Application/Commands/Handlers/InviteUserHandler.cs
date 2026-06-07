using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.UserModule.Application.Commands.Handlers;

public class InviteUserHandler(
    IInviteRepository inviteRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<InviteUserCommand, Result<string>>
{
    public async Task<Result<string>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var existing = await inviteRepository.GetActiveByEmailAndTenantAsync(
            request.Email, tenantId, cancellationToken);

        if (existing != null)
            return Result<string>.Failure(Conflict("Conflict", "An active invite already exists for this email."));

        var invite = Invite.Create(request.Email, tenantId, TenantRole.Client, DateTimeOffset.UtcNow.AddDays(7));

        await inviteRepository.AddAsync(invite, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(invite.Code);
    }
}
