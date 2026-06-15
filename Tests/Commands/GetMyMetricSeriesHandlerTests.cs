using System.Reflection;
using BuildingBlocks.Shared.Abstractions;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Queries;
using Modules.NutritionModule.Application.Queries.Handlers;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// The body-metric trend (api/me/progress/metrics/series, Phase 2), fully mocked — no database. Pins the
/// frozen rules from API-CONTRACTS §3: latest-per-local-day; case-insensitive type matching (the handler
/// normalizes before the repository call); 200 + empty Points for an empty range; and self-scoping (the
/// repository is only ever asked for the caller's own id). Dates are time-relative to UtcNow (no calendar
/// time-bomb). The repository contract (GetOwnSeriesAsync returns LocalDate asc, LoggedAtUtc asc) is
/// honoured here so the latest-per-day reduction is exercised exactly as in production.
/// </summary>
public sealed class GetMyMetricSeriesHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    private static GetMyMetricSeriesHandler CreateSut(IMetricEntryRepository repo, Guid userId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");
        return new GetMyMetricSeriesHandler(repo, currentUser);
    }

    // Build a MetricEntry with a controlled LocalDate + LoggedAtUtc (factory stamps UtcNow ⇒ reflection).
    private static MetricEntry Entry(Guid userId, string type, decimal value, string? unit, DateOnly day, DateTimeOffset loggedAt)
    {
        var entry = MetricEntry.Log(userId, type, value, unit, day);
        typeof(MetricEntry).GetProperty(nameof(MetricEntry.LoggedAtUtc))!.SetValue(entry, loggedAt);
        return entry;
    }

    // ── latest-per-day ──

    [Fact]
    public async Task Returns_the_latest_value_per_local_day()
    {
        var userId = Guid.NewGuid();
        var d1 = Today.AddDays(-2);
        var d2 = Today.AddDays(-1);
        var baseInstant = DateTimeOffset.UtcNow.AddDays(-3);

        // Two entries on d1 (the later one — 81.0 — wins) and one on d2. Repository returns LocalDate asc,
        // LoggedAtUtc asc, per its contract.
        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnSeriesAsync(userId, "weight", Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MetricEntry>
            {
                Entry(userId, "weight", 82.0m, "kg", d1, baseInstant),
                Entry(userId, "weight", 81.0m, "kg", d1, baseInstant.AddHours(6)),  // later on d1 ⇒ wins
                Entry(userId, "weight", 80.5m, "kg", d2, baseInstant.AddDays(1)),
            });

        var sut = CreateSut(repo, userId);
        var result = await sut.Handle(new GetMyMetricSeriesQuery("weight", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("weight", dto.Type);
        Assert.Equal("kg", dto.Unit);
        Assert.Equal(2, dto.Points.Count);
        // Points ordered by day asc; d1 carries the LATEST (81.0), not the earlier 82.0.
        Assert.Equal(d1, dto.Points[0].LocalDate);
        Assert.Equal(81.0m, dto.Points[0].Value);
        Assert.Equal(d2, dto.Points[1].LocalDate);
        Assert.Equal(80.5m, dto.Points[1].Value);
    }

    // ── case-insensitive type ──

    [Fact]
    public async Task Type_is_matched_case_insensitively_and_normalized_before_the_repository_call()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnSeriesAsync(userId, "weight", Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MetricEntry>
            {
                Entry(userId, "weight", 80m, "kg", Today, DateTimeOffset.UtcNow),
            });

        var sut = CreateSut(repo, userId);
        // Caller sends "  WeIgHt  " — the handler must normalize to "weight" before querying.
        var result = await sut.Handle(new GetMyMetricSeriesQuery("  WeIgHt  ", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("weight", result.Value!.Type);          // echoed normalized
        Assert.Single(result.Value!.Points);
        // The repository was asked for the normalized type, never the raw casing/whitespace.
        await repo.Received(1).GetOwnSeriesAsync(
            userId, "weight", Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // ── empty range ──

    [Fact]
    public async Task Empty_range_returns_200_with_empty_points_and_null_unit()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnSeriesAsync(userId, "weight", Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MetricEntry>());

        var sut = CreateSut(repo, userId);
        var result = await sut.Handle(new GetMyMetricSeriesQuery("weight", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Points);
        Assert.Null(result.Value!.Unit);
        Assert.Equal("weight", result.Value!.Type);
    }

    // ── self-scope / IDOR ──

    [Fact]
    public async Task Read_only_queries_the_callers_own_series()
    {
        var userA = Guid.NewGuid();
        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnSeriesAsync(userA, "weight", Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MetricEntry> { Entry(userA, "weight", 80m, "kg", Today, DateTimeOffset.UtcNow) });

        var sut = CreateSut(repo, userA);
        var result = await sut.Handle(new GetMyMetricSeriesQuery("weight", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The repository is only ever asked for the caller's own id — user B's series is unreachable.
        await repo.Received(1).GetOwnSeriesAsync(
            userA, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().GetOwnSeriesAsync(
            Arg.Is<Guid>(id => id != userA), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
            Arg.Any<CancellationToken>());
    }

    // ── default window ──

    [Fact]
    public async Task Default_window_is_the_trailing_twelve_weeks_ending_today()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnSeriesAsync(userId, "weight", Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MetricEntry>());

        var sut = CreateSut(repo, userId);
        await sut.Handle(new GetMyMetricSeriesQuery("weight", null, null), CancellationToken.None);

        // 12 weeks = 84 days inclusive ⇒ from = today − 83.
        var expectedFrom = Today.AddDays(-7 * 12 + 1);
        await repo.Received(1).GetOwnSeriesAsync(
            userId, "weight", expectedFrom, Today, Arg.Any<CancellationToken>());
    }
}
