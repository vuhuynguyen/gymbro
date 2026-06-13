using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Plans;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class SetPlanAssignmentActiveHandler(
    IPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetPlanAssignmentActiveCommand, Result>
{
    public Task<Result> Handle(SetPlanAssignmentActiveCommand request, CancellationToken cancellationToken) =>
        PlanAssignmentLifecycle.SetActiveAsync<PlanAssignment>(
            assignmentRepository.GetByIdAsync, unitOfWork, request.AssignmentId, request.Active,
            "Plan assignment not found.", cancellationToken);
}
