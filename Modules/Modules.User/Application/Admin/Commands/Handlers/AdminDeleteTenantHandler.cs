using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Authorization;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Admin.Commands.Handlers;

public class AdminDeleteTenantHandler(
    ITenantRepository tenantRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdminDeleteTenantCommand, Result>
{
    public async Task<Result> Handle(AdminDeleteTenantCommand request, CancellationToken cancellationToken)
    {
        if (AdminPolicy.Deny(currentUser) is { } denied)
            return denied;

        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
            return Result.Failure(Error.NotFound("Tenant not found."));

        tenantRepository.Remove(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
