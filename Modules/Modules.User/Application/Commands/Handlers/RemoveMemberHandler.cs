using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Modules.UserModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.UserModule.Application.Commands.Handlers;

public class RemoveMemberHandler(
    ITenantAuthorizationService tenantAuth,
    IUserTenantRoleRepository roleRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveMemberCommand, Result>
{
    public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        if (!await tenantAuth.HasPermissionAsync(request.TenantId, Permission.ClientRemove, cancellationToken))
            return Result.Failure(Unauthorized("Forbidden", "You do not have permission to remove members from this tenant."));

        var membership = await roleRepository.GetByUserAndTenantAsync(
            request.UserId, request.TenantId, cancellationToken);

        if (membership == null)
            return Result.Failure(Error.NotFound("Member not found in this tenant."));

        if (membership.Role == TenantRole.Owner)
            return Result.Failure(Error.Validation("Cannot remove an Owner. Transfer ownership first."));

        if (membership.UserId == currentUser.UserId)
            return Result.Failure(Error.Validation("Use the leave endpoint to remove yourself."));

        roleRepository.Remove(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
