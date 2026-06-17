using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.NutritionModule.Application.Commands.Handlers;

public sealed class SetNutritionItemStatusHandler(
    IDailyNutritionLogRepository logRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<SetNutritionItemStatusCommand, Result>
{
    public async Task<Result> Handle(SetNutritionItemStatusCommand request, CancellationToken cancellationToken)
    {
        var log = await logRepository.GetOwnByDateAsync(currentUser.UserId, request.Date, cancellationToken);
        if (log == null)
            return Result.Failure(NotFound("NotFound", "No nutrition log for that date."));
        if (!log.IsOpen)
            return Result.Failure(Conflict("DailyLog.Closed", "This day is closed and can no longer be edited."));

        var item = log.FindItem(request.ItemId);
        if (item == null)
            return Result.Failure(NotFound("NotFound", "Logged item not found."));

        switch (request.Status)
        {
            case LoggedItemStatus.Completed:
                item.Complete(request.Note);
                break;
            case LoggedItemStatus.Skipped:
                item.Skip(request.Note);
                break;
            case LoggedItemStatus.Planned:
                item.ResetToPlanned();
                break;
            default:
                return Result.Failure(Validation("Status", "Only Completed or Skipped may be set directly."));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
