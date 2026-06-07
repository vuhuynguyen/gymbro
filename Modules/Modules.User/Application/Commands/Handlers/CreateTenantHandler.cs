using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;
using Modules.UserModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.UserModule.Application.Commands.Handlers;

public class CreateTenantHandler(
    ITenantRepository tenantRepository,
    IUserTenantRoleRepository roleRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == Guid.Empty)
            return Result<Guid>.Failure(Unauthorized("Unauthorized", "User is not authenticated."));

        var tenant = Tenant.Create(request.Name, currentUser.UserId);

        await tenantRepository.AddAsync(tenant, cancellationToken);

        var ownerRole = UserTenantRole.Create(currentUser.UserId, tenant.Id, TenantRole.Owner);
        await roleRepository.AddAsync(ownerRole, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(tenant.Id);
    }
}
