using Modules.WorkoutSessionModule.Entities;
using BuildingBlocks.Shared.DomainPrimitives;
using Xunit;

namespace Gymbro.Tests.Domain;

public sealed class WorkoutSessionTests
{
    private static WorkoutSession CreateInProgress(Guid? traineeId = null, Guid? tenantId = null) =>
        WorkoutSession.Start(
            traineeId: traineeId ?? Guid.NewGuid(),
            tenantId: tenantId ?? Guid.NewGuid(),
            source: SessionSource.Adhoc,
            planAssignmentId: null,
            plannedWorkoutId: null,
            workoutNameSnapshot: null,
            snapshotJson: null,
            clientTimezone: null,
            bodyweightKg: null);

    // ── Start ──────────────────────────────────────────────────────────────

    [Fact]
    public void Start_creates_InProgress_session()
    {
        var session = CreateInProgress();

        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Equal(0, session.PrCount);
        Assert.True(session.StartedAt <= DateTimeOffset.UtcNow);
        Assert.False(session.IsDeleted);
    }

    [Fact]
    public void Start_throws_when_traineeId_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            WorkoutSession.Start(Guid.Empty, Guid.NewGuid(), SessionSource.Adhoc,
                null, null, null, null, null, null));
    }

    [Fact]
    public void Start_throws_when_tenantId_is_empty()
    {
        Assert.Throws<DomainException>(() =>
            WorkoutSession.Start(Guid.NewGuid(), Guid.Empty, SessionSource.Adhoc,
                null, null, null, null, null, null));
    }

    [Fact]
    public void Start_preserves_source_and_optional_fields()
    {
        var assignmentId = Guid.NewGuid();
        var workoutId = Guid.NewGuid();

        var session = WorkoutSession.Start(
            Guid.NewGuid(), Guid.NewGuid(), SessionSource.FromAssignment,
            planAssignmentId: assignmentId,
            plannedWorkoutId: workoutId,
            workoutNameSnapshot: "Leg Day",
            snapshotJson: null,
            clientTimezone: "Europe/London",
            bodyweightKg: 75m);

        Assert.Equal(SessionSource.FromAssignment, session.Source);
        Assert.Equal(assignmentId, session.PlanAssignmentId);
        Assert.Equal(workoutId, session.PlannedWorkoutId);
        Assert.Equal("Leg Day", session.WorkoutNameSnapshot);
        Assert.Equal(75m, session.BodyweightKg);
    }

    // ── Complete ───────────────────────────────────────────────────────────

    [Fact]
    public void Complete_transitions_to_Completed()
    {
        var session = CreateInProgress();

        session.Complete(rpeOverall: 7, notes: "Felt strong", completedAt: null, prCount: 2);

        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(7, session.RpeOverall);
        Assert.Equal("Felt strong", session.Notes);
        Assert.Equal(2, session.PrCount);
        Assert.NotNull(session.CompletedAt);
        Assert.NotNull(session.DurationSeconds);
    }

    [Fact]
    public void Complete_calculates_duration_from_StartedAt()
    {
        var session = CreateInProgress();
        var completedAt = session.StartedAt.AddMinutes(30);

        session.Complete(null, null, completedAt, prCount: 0);

        Assert.Equal(1800, session.DurationSeconds);
    }

    [Fact]
    public void Complete_raises_SessionCompletedEvent()
    {
        var session = CreateInProgress();

        session.Complete(null, null, null, prCount: 0);

        var evt = Assert.Single(session.DomainEvents);
        Assert.IsType<SessionCompletedEvent>(evt);
    }

    [Fact]
    public void Complete_throws_when_already_Completed()
    {
        var session = CreateInProgress();
        session.Complete(null, null, null, prCount: 0);

        Assert.Throws<DomainException>(() =>
            session.Complete(null, null, null, prCount: 0));
    }

    [Fact]
    public void Complete_throws_when_prCount_is_negative()
    {
        var session = CreateInProgress();

        Assert.Throws<DomainException>(() =>
            session.Complete(null, null, null, prCount: -1));
    }

    // ── Abandon ────────────────────────────────────────────────────────────

    [Fact]
    public void Abandon_transitions_to_Abandoned()
    {
        var session = CreateInProgress();

        session.Abandon("Lost motivation");

        Assert.Equal(SessionStatus.Abandoned, session.Status);
        Assert.Equal("Lost motivation", session.Notes);
        Assert.NotNull(session.CompletedAt);
        // Elapsed time is recorded on abandon too (parity with Complete).
        Assert.NotNull(session.DurationSeconds);
    }

    [Fact]
    public void Abandon_throws_when_already_Abandoned()
    {
        var session = CreateInProgress();
        session.Abandon(null);

        Assert.Throws<DomainException>(() => session.Abandon(null));
    }

    // ── State machine cross-checks ─────────────────────────────────────────

    [Fact]
    public void Completed_session_cannot_be_abandoned()
    {
        var session = CreateInProgress();
        session.Complete(null, null, null, prCount: 0);

        Assert.Throws<DomainException>(() => session.Abandon(null));
    }

    [Fact]
    public void Abandoned_session_cannot_be_completed()
    {
        var session = CreateInProgress();
        session.Abandon(null);

        Assert.Throws<DomainException>(() =>
            session.Complete(null, null, null, prCount: 0));
    }

    [Fact]
    public void PrCount_is_zero_while_InProgress()
    {
        var session = CreateInProgress();

        Assert.Equal(0, session.PrCount);
    }

    [Fact]
    public void PrCount_is_finalized_on_Complete()
    {
        var session = CreateInProgress();
        session.Complete(null, null, null, prCount: 5);

        Assert.Equal(5, session.PrCount);
    }
}
