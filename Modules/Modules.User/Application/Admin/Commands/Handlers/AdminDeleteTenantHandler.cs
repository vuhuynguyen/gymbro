using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.Abstractions;

namespace Modules.UserModule.Application.Admin.Commands.Handlers;

public class AdminDeleteTenantHandler(
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AdminDeleteTenantCommand, Result>
{
    public async Task<Result> Handle(AdminDeleteTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
            return Result.Failure(Error.NotFound("Tenant not found."));

        tenantRepository.Remove(tenant);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
