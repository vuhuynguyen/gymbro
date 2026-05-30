using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class AbandonSessionHandler(
    IWorkoutSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AbandonSessionCommand, Result>
{
    public async Task<Result> Handle(AbandonSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result.Failure(NotFound("NotFound", "Session not found."));

        if (session.TraineeId != currentUser.UserId)
            return Result.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure(Conflict("Conflict", "Session is already completed or abandoned."));

        session.Abandon(request.Notes);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
