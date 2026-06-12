using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
using BuildingBlocks.Shared.DomainPrimitives;
using Modules.NutritionModule.Application.Abstractions;
using Modules.NutritionModule.Application.Commands;
using Modules.NutritionModule.Application.Commands.Handlers;
using Modules.NutritionModule.Application.Queries;
using Modules.NutritionModule.Application.Queries.Handlers;
using Modules.NutritionModule.Application.Validators;
using Modules.NutritionModule.Entities;
using NSubstitute;
using Xunit;

namespace Gymbro.Tests.Commands;

/// <summary>
/// Pins the self-scoped daily check-in (MetricEntry) contract: writes stamp owner = currentUser.UserId
/// (never a client-supplied id), reads only touch the caller's own series and come back NEWEST FIRST, and
/// the validator rejects malformed input. Fully mocked.
/// </summary>
public sealed class MetricEntryHandlerTests
{
    private static readonly DateOnly Date = new(2026, 6, 11);

    // ── Write: owner stamping ────────────────────────────────────────────

    [Fact]
    public async Task Log_stamps_the_current_user_as_owner_and_saves()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IMetricEntryRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        var sut = new LogMetricEntryHandler(repo, uow, currentUser);

        var result = await sut.Handle(
            new LogMetricEntryCommand("weight", 82.5m, "kg", Date), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await repo.Received(1).AddAsync(
            Arg.Is<MetricEntry>(e =>
                e.TraineeId == userId && e.Type == "weight" && e.Value == 82.5m
                && e.Unit == "kg" && e.LocalDate == Date),
            Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Log_defaults_localDate_to_utc_today_when_omitted()
    {
        var repo = Substitute.For<IMetricEntryRepository>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        var sut = new LogMetricEntryHandler(repo, Substitute.For<IUnitOfWork>(), currentUser);

        var result = await sut.Handle(
            new LogMetricEntryCommand("sleep", 7.5m, "h", LocalDate: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await repo.Received(1).AddAsync(
            Arg.Is<MetricEntry>(e => e.LocalDate == today), Arg.Any<CancellationToken>());
    }

    // ── Read: own-scoping + newest-first ─────────────────────────────────

    [Fact]
    public async Task Read_only_queries_the_callers_own_series()
    {
        var userA = Guid.NewGuid();
        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnForDateAsync(userA, Date, Arg.Any<CancellationToken>())
            .Returns([MetricEntry.Log(userA, "weight", 80m, "kg", Date)]);
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userA);
        var sut = new GetMyNutritionMetricsHandler(repo, currentUser);

        var result = await sut.Handle(new GetMyNutritionMetricsQuery(Date), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        // The repository is only ever asked for the caller's own id — user B's metrics are unreachable.
        await repo.Received(1).GetOwnForDateAsync(userA, Date, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().GetOwnForDateAsync(
            Arg.Is<Guid>(id => id != userA), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Read_returns_entries_newest_first()
    {
        var userId = Guid.NewGuid();
        var older = MetricEntry.Log(userId, "weight", 81m, "kg", Date);
        var newer = MetricEntry.Log(userId, "weight", 80.4m, "kg", Date);
        typeof(MetricEntry).GetProperty(nameof(MetricEntry.LoggedAtUtc))!
            .SetValue(older, DateTimeOffset.UtcNow.AddHours(-5));

        var repo = Substitute.For<IMetricEntryRepository>();
        repo.GetOwnForDateAsync(userId, Date, Arg.Any<CancellationToken>())
            .Returns([older, newer]); // deliberately oldest-first from the repo
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        var sut = new GetMyNutritionMetricsHandler(repo, currentUser);

        var result = await sut.Handle(new GetMyNutritionMetricsQuery(Date), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(80.4m, result.Value.Items[0].Value); // newest first — the client's "latest" per type
        Assert.Equal(81m, result.Value.Items[1].Value);
    }

    // ── Domain factory ───────────────────────────────────────────────────

    [Fact]
    public void Log_requires_a_trainee_and_a_type_and_trims()
    {
        Assert.Throws<DomainException>(() => MetricEntry.Log(Guid.Empty, "weight", 80m, null, Date));
        Assert.Throws<DomainException>(() => MetricEntry.Log(Guid.NewGuid(), "  ", 80m, null, Date));

        var entry = MetricEntry.Log(Guid.NewGuid(), " weight ", 80m, " kg ", Date);
        Assert.Equal("weight", entry.Type);
        Assert.Equal("kg", entry.Unit);
        Assert.True(entry.LoggedAtUtc <= DateTimeOffset.UtcNow);
    }

    // ── Validator ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("", 80, "kg", true)]              // type required
    [InlineData("weight", -1, "kg", true)]        // negative value
    [InlineData("weight", 10_000_000, "kg", true)] // beyond numeric(8,2) bound
    [InlineData("weight", 80.5, "kg", false)]
    [InlineData("sleep", 7.5, null, false)]
    public void Validator_enforces_type_and_value_bounds(string type, double value, string? unit, bool expectError)
    {
        var validator = new LogMetricEntryCommandValidator();
        var result = validator.Validate(new LogMetricEntryCommand(type, (decimal)value, unit, Date));
        Assert.Equal(expectError, !result.IsValid);
    }

    [Fact]
    public void Validator_rejects_overlong_type_and_unit_and_absurd_dates()
    {
        var validator = new LogMetricEntryCommandValidator();

        Assert.False(validator.Validate(
            new LogMetricEntryCommand(new string('x', 51), 1m, null, Date)).IsValid);
        Assert.False(validator.Validate(
            new LogMetricEntryCommand("weight", 1m, new string('u', 21), Date)).IsValid);
        Assert.False(validator.Validate(
            new LogMetricEntryCommand("weight", 1m, null, new DateOnly(1999, 12, 31))).IsValid);
        Assert.True(validator.Validate(
            new LogMetricEntryCommand("weight", 1m, null, LocalDate: null)).IsValid);
    }
}
