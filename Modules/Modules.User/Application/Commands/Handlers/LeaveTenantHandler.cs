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
        var membership = await roleRepository.GetByUserAndTenantAsync(
            currentUser.UserId, request.TenantId, cancellationToken);

        if (membership == null)
            return Result.Failure(Error.NotFound("Membership not found."));

        if (membership.Role == TenantRole.Owner)
        {
            var allMembers = await roleRepository.GetByTenantAsync(request.TenantId, cancellationToken);
            var otherOwners = allMembers.Any(m => m.UserId != currentUser.UserId && m.Role == TenantRole.Owner);

            if (!otherOwners)
                return Result.Failure(Error.Validation("Cannot leave: you are the only owner. Transfer ownership first."));
        }

        roleRepository.Remove(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
