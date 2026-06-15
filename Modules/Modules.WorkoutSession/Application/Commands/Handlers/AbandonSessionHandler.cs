using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.Abstractions;

namespace Modules.WorkoutSessionModule.Application.Commands.Handlers;

public sealed class AbandonSessionHandler(
    IWorkoutSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AbandonSessionCommand, Result>
{
    public async Task<Result> Handle(AbandonSessionCommand request, CancellationToken cancellationToken)
    {
        var load = await SessionGuard.LoadOwnedInProgressAsync(
            sessionRepository, currentUser, request.SessionId, cancellationToken,
            SessionGuard.TerminalStateMessage);
        if (load.IsFailure)
            return Result.Failure(load.Error);

        load.Value!.Abandon(request.Notes);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
