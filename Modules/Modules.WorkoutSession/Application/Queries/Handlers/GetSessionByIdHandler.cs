using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Application.Mapping;
using static BuildingBlocks.Shared.Errors.CommonErrors;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

public sealed class GetSessionByIdHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ITenantAuthorizationService tenantAuth,
    ITenantContext tenantContext)
    : IRequestHandler<GetSessionByIdQuery, Result<SessionDetailDto>>
{
    public async Task<Result<SessionDetailDto>> Handle(
        GetSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == null)
            return Result<SessionDetailDto>.Failure(Validation("TenantId", "X-Tenant-Id header is required."));

        var tenantId = tenantContext.TenantId.Value;

        var session = await sessionRepository.GetWithExercisesAndSetsAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<SessionDetailDto>.Failure(NotFound("NotFound", "Session not found."));

        if (!await ResourceAccessGuard.CanViewTraineeWorkoutLogsAsync(
                tenantAuth, tenantId, session.TraineeId, cancellationToken))
            return Result<SessionDetailDto>.Failure(Unauthorized("Unauthorized", "You cannot view this session."));

        var snapshotDto = SessionMapping.DeserializeSnapshot(session.SnapshotJson);
        var exerciseIds = SessionMapping.CollectExerciseIds(session.Exercises);
        var namesResult = await mediator.Send(new ResolveExerciseNamesQuery(exerciseIds), cancellationToken);
        if (namesResult.IsFailure)
            return Result<SessionDetailDto>.Failure(namesResult.Error);

        return Result<SessionDetailDto>.Success(
            SessionMapping.ToSessionDetailDto(session, snapshotDto, namesResult.Value!));
    }
}
