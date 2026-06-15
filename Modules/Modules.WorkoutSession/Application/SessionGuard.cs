using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.Results;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Entities;
using static BuildingBlocks.Shared.Errors.Error;

namespace Modules.WorkoutSessionModule.Application;

/// <summary>
/// The row-level access + state guard shared by every in-session mutation handler. Centralises the
/// load → 404-if-missing → 403-if-not-owner → 409-if-not-in-progress contract — a defense-in-depth
/// row-level check (see PERMISSIONS.md) that was previously copy-pasted across ~9 command handlers and
/// could drift. Keeping it in one place means a change to the ownership/state posture is made once.
/// (Audit finding 9.)
/// </summary>
public static class SessionGuard
{
    public const string NotInProgressMessage = "Session is not in progress.";
    public const string TerminalStateMessage = "Session is already completed or abandoned.";

    /// <summary>
    /// Loads the caller's own in-progress session, or returns the standard failure: 404 if missing,
    /// 403 if it belongs to another user, 409 if it is not in progress.
    /// </summary>
    public static async Task<Result<WorkoutSession>> LoadOwnedInProgressAsync(
        IWorkoutSessionRepository sessionRepository,
        ICurrentUser currentUser,
        Guid sessionId,
        CancellationToken ct,
        string notInProgressMessage = NotInProgressMessage)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<WorkoutSession>.Failure(NotFound("NotFound", "Session not found."));
        if (session.TraineeId != currentUser.UserId)
            return Result<WorkoutSession>.Failure(Unauthorized("Unauthorized", "This session does not belong to you."));
        if (session.Status != SessionStatus.InProgress)
            return Result<WorkoutSession>.Failure(Conflict("Conflict", notInProgressMessage));
        return Result<WorkoutSession>.Success(session);
    }
}
