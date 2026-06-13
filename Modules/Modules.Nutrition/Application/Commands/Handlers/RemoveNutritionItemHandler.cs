using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>Removes an ad-hoc item from the caller's own open day. Planned items can't be removed
/// (they're skipped instead, keeping the adherence denominator honest). Self-scoped to currentUser.</summary>
public sealed class RemoveNutritionItemHandler(
    IDailyNutritionLogRepository logRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<RemoveNutritionItemCommand, Result>
{
    public async Task<Result> Handle(RemoveNutritionItemCommand request, CancellationToken cancellationToken)
    {
        var log = await logRepository.GetOwnByDateAsync(currentUser.UserId, request.Date, cancellationToken);
        if (log == null)
            return Result.Failure(NotFound("NotFound", "No nutrition log for that date."));
        if (!log.IsOpen)
            return Result.Failure(Conflict("DailyLog.Closed", "This day is closed and can no longer be edited."));

        var item = log.FindItem(request.ItemId);
        if (item == null)
            return Result.Failure(NotFound("NotFound", "Logged item not found."));
        if (item.IsPlanned)
            return Result.Failure(Validation("ItemId", "Planned items can't be removed — skip them instead."));

        log.RemoveAdhocItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
