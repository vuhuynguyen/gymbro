using BuildingBlocks.Application.Messaging;
using Modules.WorkoutSessionModule.Application.Abstractions;
using Modules.WorkoutSessionModule.Application.Queries.Handlers;
using Modules.WorkoutSessionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// <see cref="IsTrainingDayHandler"/> — the cross-module signal nutrition recurrence reads: a date is a training
/// day iff the user has a workout session within that local calendar day. Self-scoped across gyms; no session ⇒
/// rest day.
/// </summary>
public sealed class IsTrainingDayHandlerTests
{
    [Fact]
    public async Task A_date_with_a_session_is_a_training_day_and_other_dates_are_not()
    {
        var userId = Guid.NewGuid();
        var session = WorkoutSession.Start(
            userId, Guid.NewGuid(), SessionSource.Adhoc, null, null, "Lift", null, "UTC", null);
        var sessionDate = DateOnly.FromDateTime(session.StartedAt.UtcDateTime);

        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.QueryOwnAcrossGyms(userId)
            .Returns(new TestAsyncEnumerable<WorkoutSession>(new[] { session }));
        var sut = new IsTrainingDayHandler(repo);

        Assert.True(await sut.Handle(new IsTrainingDayQuery(userId, sessionDate, "UTC"), CancellationToken.None));
        Assert.False(await sut.Handle(
            new IsTrainingDayQuery(userId, sessionDate.AddDays(5), "UTC"), CancellationToken.None));
    }

    [Fact]
    public async Task No_sessions_means_rest_day()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IWorkoutSessionRepository>();
        repo.QueryOwnAcrossGyms(userId)
            .Returns(new TestAsyncEnumerable<WorkoutSession>(Array.Empty<WorkoutSession>()));
        var sut = new IsTrainingDayHandler(repo);

        Assert.False(await sut.Handle(
            new IsTrainingDayQuery(userId, new DateOnly(2026, 7, 4), "UTC"), CancellationToken.None));
    }
}
