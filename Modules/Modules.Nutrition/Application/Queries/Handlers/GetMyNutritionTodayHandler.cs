using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using BuildingBlocks.Shared.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.DTOs;
using Modules.NutritionModule.Application.Mapping;

namespace Modules.NutritionModule.Application.Queries.Handlers;

/// <summary>
/// Snapshot-on-touch for the caller's nutrition day. Reads only the caller's own data (<c>currentUser.UserId</c>)
/// across every gym: returns the existing day, or lazily creates + seeds it from the active assignment's snapshot
/// (a trainee with no active assignment gets an empty, non-persisted day on READ; the persisted plan-less
/// self-logged day is created only by a WRITE). This read no longer closes prior days or raises events —
/// <c>NutritionStaleDayCloser</c> closes stale days at local midnight out-of-band, so a day finalizes even if the
/// trainee never reopens the app.
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
        // Day boundaries are the trainee's, not UTC: honour the client-sent date, else derive "today" from the
        // client's captured zone rather than bare UtcNow.
        var localDate = request.Date ?? LocalDayResolver.LocalDateOf(DateTimeOffset.UtcNow, request.Timezone);

        // Existing day, or lazily create + seed from the active assignment. READS never create a plan-less
        // self-logged row — only WRITES do — so the no-assignment case falls through to a non-persisted EmptyDay.
        var log = await provisioner.GetOrCreateFromAssignmentAsync(userId, localDate, request.Timezone, cancellationToken);

        if (log == null)
            return Result<DailyNutritionLogDto>.Success(NutritionMapping.EmptyDay(userId, localDate));

        // Persist a freshly-created+seeded day. A no-op when the day already existed (EF tracks no changes).
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
