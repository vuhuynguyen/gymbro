using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class CreateWorkoutPlanHandler(
    IWorkoutPlanRepository repository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<CreateWorkoutPlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateWorkoutPlanCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;

        var plan = WorkoutPlan.Create(
            tenantId,
            currentUser.UserId,
            request.Name,
            request.Description,
            request.DurationWeeks,
            request.WorkoutsPerWeek);

        await repository.AddAsync(plan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(plan.Id);
    }
}
