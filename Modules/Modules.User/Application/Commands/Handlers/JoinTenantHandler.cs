using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Commands.Handlers;

public class JoinTenantHandler(
    IInviteRepository inviteRepository,
    IUserTenantRoleRepository roleRepository,
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<JoinTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(JoinTenantCommand request, CancellationToken cancellationToken)
    {
        var invite = await inviteRepository.GetActiveByCodeAsync(request.Code, cancellationToken);

        if (invite == null)
            return Result<Guid>.Failure(Error.NotFound("Invite not found or has expired."));

        var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken);
        if (user == null)
            return Result<Guid>.Failure(Error.NotFound("User profile not found."));

        var tenantId = invite.TenantId!.Value;

        var alreadyMember = await roleRepository.GetByUserAndTenantAsync(
            currentUser.UserId, tenantId, cancellationToken);

        if (alreadyMember != null)
            return Result<Guid>.Failure(Error.Conflict("User is already a member of this tenant."));

        invite.MarkUsed();

        var role = UserTenantRole.Create(currentUser.UserId, tenantId, invite.Role);
        await roleRepository.AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(tenantId);
    }
}
