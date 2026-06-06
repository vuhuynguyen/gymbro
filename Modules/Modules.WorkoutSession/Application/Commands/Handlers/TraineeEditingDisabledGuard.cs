using MediatR;
using Modules.WorkoutPlanModule.Application.Queries;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

/// <summary>
/// Shared check for the <c>DisableTraineeEditing</c> assignment flag. A session whose originating
/// assignment locks editing forbids structural changes (adding/skipping/substituting exercises).
/// Ad-hoc sessions (no assignment) and assignments that no longer exist impose no restriction.
/// </summary>
internal static class TraineeEditingDisabledGuard
{
    public static async Task<bool> IsDisabledAsync(
        IMediator mediator,
        Guid? planAssignmentId,
        CancellationToken cancellationToken)
    {
        if (planAssignmentId is not { } assignmentId)
            return false;

        var assignmentResult = await mediator.Send(
            new GetPlanAssignmentByIdQuery(assignmentId), cancellationToken);

        return assignmentResult.IsSuccess && assignmentResult.Value!.DisableTraineeEditing;
    }
}
