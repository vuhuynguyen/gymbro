using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Entities;

namespace Modules.NutritionModule.Application.Commands.Handlers;

/// <summary>Appends one entry to the caller's own metric series (owner = currentUser.UserId, never a
/// client-supplied trainee id). Append-only: a re-log on the same day is a newer entry, not an update.</summary>
public sealed class LogMetricEntryHandler(
    IMetricEntryRepository metricRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<LogMetricEntryCommand, Result>
{
    public async Task<Result> Handle(LogMetricEntryCommand request, CancellationToken cancellationToken)
    {
        var localDate = request.LocalDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var entry = MetricEntry.Log(currentUser.UserId, request.Type, request.Value, request.Unit, localDate);

        await metricRepository.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
