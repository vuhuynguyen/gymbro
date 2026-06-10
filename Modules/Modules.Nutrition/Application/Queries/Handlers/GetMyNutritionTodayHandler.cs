using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.FoodModule.Application.Queries;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>
/// Snapshot-on-touch for the caller's nutrition day. Reads only the caller's own data
/// (<c>currentUser.UserId</c>) across every gym. On first access of a date it (1) closes any stale prior
/// open days — marking still-Planned items Missed and finalizing adherence via the outbox — then (2) returns
/// the existing day, or (3) lazily creates + seeds it from the active assignment's snapshot. A trainee with
/// no active nutrition assignment gets an empty, non-persisted day (MVP: logging requires an assignment).
/// </summary>
public sealed class GetMyNutritionTodayHandler(
    IDailyNutritionLogRepository logRepository,
    INutritionPlanAssignmentRepository assignmentRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyNutritionTodayQuery, Result<DailyNutritionLogDto>>
{
    public async Task<Result<DailyNutritionLogDto>> Handle(GetMyNutritionTodayQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var localDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // (1) Lazy local-midnight close of stale open days (tracked, so Close() persists).
        var staleOpen = await logRepository.QueryOwnAcrossGyms(userId)
            .Where(l => l.Status == DailyLogStatus.Open && l.LocalDate < localDate)
            .Include(l => l.Items)
            .ToListAsync(cancellationToken);
        foreach (var day in staleOpen)
            day.Close();
        var hasPendingClose = staleOpen.Count > 0;

        // (2) Existing day for the date?
        var existing = await logRepository.GetOwnByDateAsync(userId, localDate, cancellationToken);
        if (existing != null)
        {
            if (hasPendingClose)
                await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<DailyNutritionLogDto>.Success(NutritionMapping.ToDayDto(existing));
        }

        // (3) Create + seed from the active assignment governing this date.
        var assignment = await assignmentRepository.QueryOwnAcrossGyms(userId)
            .Where(a => a.IsActive && a.StartDate <= localDate && (a.EndDate == null || a.EndDate >= localDate))
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment == null)
        {
            if (hasPendingClose)
                await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<DailyNutritionLogDto>.Success(NutritionMapping.EmptyDay(userId, localDate));
        }

        var log = DailyNutritionLog.Open(
            userId, assignment.TenantId!.Value, localDate, request.Timezone,
            NutritionSource.FromAssignment, assignment.Id, assignment.SnapshotJson);

        var snapshot = NutritionMapping.DeserializeSnapshot(assignment.SnapshotJson);
        if (snapshot != null)
        {
            // Resolve each planned food's kind once (so supplements/beverages tag in the checklist); the
            // kind is then denormalized onto the seeded items and reads never re-touch the catalog.
            var kinds = await ResolveKindsAsync(snapshot, cancellationToken);
            log.SeedPlannedItems(NutritionMapping.ToSeedItems(snapshot, kinds));
        }

        await logRepository.AddAsync(log, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Race: a concurrent first-touch created the day (unique TraineeId+LocalDate). Return that one.
            var raced = await logRepository.GetOwnByDateAsync(userId, localDate, cancellationToken);
            if (raced != null)
                return Result<DailyNutritionLogDto>.Success(NutritionMapping.ToDayDto(raced));
            throw;
        }

        return Result<DailyNutritionLogDto>.Success(NutritionMapping.ToDayDto(log));
    }

    /// <summary>Best-effort map of the snapshot's foods to their catalog kind name. On any resolve failure
    /// the items just default to "Food" — kind is cosmetic and never blocks seeding.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ResolveKindsAsync(
        NutritionPlanSnapshot snapshot, CancellationToken ct)
    {
        var foodIds = snapshot.Meals.SelectMany(m => m.Items.Select(i => i.FoodId)).Distinct().ToList();
        if (foodIds.Count == 0) return new Dictionary<Guid, string>();

        var result = await mediator.Send(new ResolveFoodSummariesQuery(foodIds), ct);
        var kinds = new Dictionary<Guid, string>();
        if (result.IsSuccess)
            foreach (var (id, food) in result.Value!)
                kinds[id] = food.Kind;
        return kinds;
    }
}
