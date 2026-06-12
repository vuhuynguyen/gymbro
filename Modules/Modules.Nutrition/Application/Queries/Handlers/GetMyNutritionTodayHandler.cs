using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
/// no active assignment gets an empty, non-persisted day on READ; the persisted plan-less self-logged day is
/// created only by a WRITE (see <c>NutritionDayProvisioner</c> / <c>AddAdhocNutritionItemHandler</c>).
/// </summary>
public sealed class GetMyNutritionTodayHandler(
    IDailyNutritionLogRepository logRepository,
    INutritionDayProvisioner provisioner,
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

        // (2) Existing day, or lazily create+seed from the active assignment. READS never create a plan-less
        // self-logged row — only WRITES do — so the no-assignment case falls through to a non-persisted
        // EmptyDay below.
        var log = await provisioner.GetOrCreateFromAssignmentAsync(userId, localDate, request.Timezone, cancellationToken);

        if (log == null)
        {
            if (hasPendingClose)
                await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<DailyNutritionLogDto>.Success(NutritionMapping.EmptyDay(userId, localDate));
        }

        // Persist a freshly-created+seeded day (and any pending stale close). A no-op when the day already
        // existed and nothing changed; EF tracks no changes so SaveChanges does nothing.
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
}
