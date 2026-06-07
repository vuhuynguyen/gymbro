using MediatR;
using Microsoft.Extensions.Logging;
using Modules.WorkoutSessionModule.Entities;

namespace Modules.WorkoutSessionModule.Application.Notifications;

/// <summary>
/// Hooks session completion for logging and future analytics. Keep side effects lightweight.
/// </summary>
public sealed class SessionCompletedEventHandler(ILogger<SessionCompletedEventHandler> logger)
    : INotificationHandler<SessionCompletedEvent>
{
    public Task Handle(SessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Session completed: SessionId={SessionId} TraineeId={TraineeId} TenantId={TenantId} At={OccurredOnUtc}",
            notification.SessionId,
            notification.TraineeId,
            notification.TenantId,
            notification.OccurredOnUtc);
        return Task.CompletedTask;
    }
}
