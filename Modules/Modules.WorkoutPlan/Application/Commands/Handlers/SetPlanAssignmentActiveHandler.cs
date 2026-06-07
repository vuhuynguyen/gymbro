using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class SetPlanAssignmentActiveHandler(
    IPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetPlanAssignmentActiveCommand, Result>
{
    public async Task<Result> Handle(SetPlanAssignmentActiveCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result.Failure(NotFound("NotFound", "Plan assignment not found."));

        assignment.SetActive(request.Active);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
