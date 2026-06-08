using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.ExerciseModule.Application.Queries;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.DTOs;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Queries.Handlers;

/// <summary>
/// Lifetime personal records: the best estimated-1RM working set per lift across every gym the caller
/// trains in. Self-scoped via <see cref="IWorkoutSessionRepository.QueryOwnAcrossGyms"/>
/// (<c>currentUser.UserId</c>); never another trainee's data. e1RM is only populated for working sets.
/// </summary>
public sealed class GetMyPersonalRecordsHandler(
    IWorkoutSessionRepository sessionRepository,
    IMediator mediator,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyPersonalRecordsQuery, Result<PersonalRecordListDto>>
{
    public async Task<Result<PersonalRecordListDto>> Handle(
        GetMyPersonalRecordsQuery request,
        CancellationToken cancellationToken)
    {
        var workingSets = await sessionRepository.QueryOwnAcrossGyms(currentUser.UserId)
            .SelectMany(s => s.Exercises)
            .SelectMany(e => e.Sets.Select(set => new
            {
                e.ExerciseId,
                set.WeightKg,
                set.Reps,
                set.EstimatedOneRepMaxKg,
                set.LoggedAt
            }))
            .Where(x => x.EstimatedOneRepMaxKg != null && x.WeightKg != null && x.Reps != null)
            .ToListAsync(cancellationToken);

        if (workingSets.Count == 0)
            return Result<PersonalRecordListDto>.Success(new PersonalRecordListDto([]));

        // Best set per lift = highest e1RM (ties broken by most recent).
        var best = workingSets
            .GroupBy(x => x.ExerciseId)
            .Select(g => g
                .OrderByDescending(x => x.EstimatedOneRepMaxKg!.Value)
                .ThenByDescending(x => x.LoggedAt)
                .First())
            .ToList();

        var namesResult = await mediator.Send(
            new ResolveExerciseNamesQuery(best.Select(b => b.ExerciseId).Distinct().ToList()),
            cancellationToken);
        if (namesResult.IsFailure)
            return Result<PersonalRecordListDto>.Failure(namesResult.Error);

        var names = namesResult.Value!;

        var records = best
            .OrderByDescending(b => b.EstimatedOneRepMaxKg!.Value)
            .Select(b => new PersonalRecordDto(
                b.ExerciseId,
                names.GetValueOrDefault(b.ExerciseId),
                b.WeightKg!.Value,
                b.Reps!.Value,
                b.EstimatedOneRepMaxKg!.Value,
                b.LoggedAt))
            .ToList();

        return Result<PersonalRecordListDto>.Success(new PersonalRecordListDto(records));
    }
}
