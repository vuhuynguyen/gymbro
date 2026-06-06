using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Admin.Commands.Handlers;

public class AdminRemoveMemberHandler(
    IUserTenantRoleRepository roleRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdminRemoveMemberCommand, Result>
{
    public async Task<Result> Handle(AdminRemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var membership = await roleRepository.GetByUserAndTenantAsync(
            request.UserId, request.TenantId, cancellationToken);

        if (membership == null)
            return Result.Failure(Error.NotFound("Membership not found."));

        roleRepository.Remove(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
