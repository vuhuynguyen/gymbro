using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class CreateNutritionPlanHandler(
    INutritionPlanRepository repository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<CreateNutritionPlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateNutritionPlanCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var plan = NutritionPlan.Create(tenantId, currentUser.UserId, request.Name, request.Description);

        await repository.AddAsync(plan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(plan.Id);
    }
}
