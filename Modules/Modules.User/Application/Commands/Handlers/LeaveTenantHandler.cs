using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;

namespace Modules.UserModule.Application.Commands.Handlers;

public class LeaveTenantHandler(
    IUserTenantRoleRepository roleRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LeaveTenantCommand, Result>
{
    public async Task<Result> Handle(LeaveTenantCommand request, CancellationToken cancellationToken)
    {
        var result = Result.Success();

        // The "last Owner cannot leave" rule is a read-modify-write: two Owners leaving at once could both see
        // the other and both delete, orphaning the tenant. A per-tenant advisory lock inside the transaction
        // serialises them — the second caller blocks until the first commits, then re-reads the smaller roster
        // and is correctly rejected.
        await unitOfWork.ExecuteTransactionalAsync(async () =>
        {
            await roleRepository.LockForTenantMembershipChangeAsync(request.TenantId, cancellationToken);

            var membership = await roleRepository.GetByUserAndTenantAsync(
                currentUser.UserId, request.TenantId, cancellationToken);

            if (membership == null)
            {
                result = Result.Failure(Error.NotFound("Membership not found."));
                return;
            }

            if (membership.Role == TenantRole.Owner)
            {
                var allMembers = await roleRepository.GetByTenantAsync(request.TenantId, cancellationToken);
                var otherOwners = allMembers.Any(m => m.UserId != currentUser.UserId && m.Role == TenantRole.Owner);

                if (!otherOwners)
                {
                    result = Result.Failure(Error.Validation(
                        "Cannot leave: you are the only owner. Transfer ownership first."));
                    return;
                }
            }

            roleRepository.Remove(membership);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return result;
    }
}
