using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutPlanModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutPlanModule.Application.Commands.Handlers;

public sealed class DeletePlanAssignmentHandler(
    IPlanAssignmentRepository assignmentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePlanAssignmentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeletePlanAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await assignmentRepository.GetByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment == null)
            return Result<bool>.Failure(NotFound("NotFound", "Plan assignment not found."));

        assignmentRepository.Remove(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
