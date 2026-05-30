using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

public sealed class GetActiveSessionHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<GetActiveSessionQuery, Result<ActiveSessionDto?>>
{
    public async Task<Result<ActiveSessionDto?>> Handle(
        GetActiveSessionQuery request,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetActiveForTraineeAsync(currentUser.UserId, cancellationToken);
        if (session == null)
            return Result<ActiveSessionDto?>.Success(null);

        var snapshotDto = SessionMapping.DeserializeSnapshot(session.SnapshotJson);
        var exerciseIds = SessionMapping.CollectExerciseIds(session.Exercises);
        var namesResult = await mediator.Send(new ResolveExerciseNamesQuery(exerciseIds), cancellationToken);
        if (namesResult.IsFailure)
            return Result<ActiveSessionDto?>.Failure(namesResult.Error);

        return Result<ActiveSessionDto?>.Success(
            SessionMapping.ToActiveSessionDto(session, snapshotDto, namesResult.Value!));
    }
}
