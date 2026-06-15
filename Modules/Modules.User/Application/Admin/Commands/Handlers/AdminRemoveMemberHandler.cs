using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Admin.Commands.Handlers;

public class AdminRemoveMemberHandler(
    IUserTenantRoleRepository roleRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdminRemoveMemberCommand, Result>
{
    public async Task<Result> Handle(AdminRemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var result = Result.Success();

        // Same orphan-prevention invariant as LeaveTenant: removing the last Owner leaves the tenant
        // unadministrable (its plans/assignments can never be managed again). Serialise membership changes
        // on the per-tenant advisory lock so a concurrent LeaveTenant can't drive the owner count to zero
        // between this read and delete — the invariant must hold on EVERY removal path. (Audit finding 5.)
        await unitOfWork.ExecuteTransactionalAsync(async () =>
        {
            await roleRepository.LockForTenantMembershipChangeAsync(request.TenantId, cancellationToken);

            var membership = await roleRepository.GetByUserAndTenantAsync(
                request.UserId, request.TenantId, cancellationToken);

            if (membership == null)
            {
                result = Result.Failure(Error.NotFound("Membership not found."));
                return;
            }

            if (membership.Role == TenantRole.Owner)
            {
                var allMembers = await roleRepository.GetByTenantAsync(request.TenantId, cancellationToken);
                var otherOwners = allMembers.Any(m => m.UserId != request.UserId && m.Role == TenantRole.Owner);

                if (!otherOwners)
                {
                    result = Result.Failure(Error.Validation(
                        "Cannot remove the last owner of a tenant. Transfer ownership first."));
                    return;
                }
            }

            roleRepository.Remove(membership);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return result;
    }
}
