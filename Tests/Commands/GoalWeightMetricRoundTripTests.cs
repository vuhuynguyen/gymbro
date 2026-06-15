using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Shared.Abstractions;
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
/// Goal-weight needs NO backend change (Decision D12): it rides the existing free-text MetricEntry as
/// Type="goal_weight", written via LogMetricEntryCommand and read via GetMyMetricSeriesQuery — no new
/// endpoint, no migration. This pins that the write path genuinely accepts the free-text type (the validator
/// has no whitelist, only NotEmpty + max-50) and that the value round-trips through the real LogMetricEntry
/// and GetMyMetricSeries handlers, wired over one shared in-memory repository fake that honours the
/// GetOwnSeriesAsync contract (case-insensitive type, LocalDate asc / LoggedAtUtc asc).
/// </summary>
public sealed class GoalWeightMetricRoundTripTests
{
    private const string GoalWeightType = "goal_weight";
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    // ── validator: the free-text type genuinely passes (no whitelist) ──

    [Fact]
    public void Validator_accepts_a_goal_weight_free_text_type()
    {
        var validator = new LogMetricEntryCommandValidator();

        var result = validator.Validate(
            new LogMetricEntryCommand(GoalWeightType, 75m, "kg", Today));

        Assert.True(result.IsValid); // 11 chars ≤ 50, non-empty ⇒ accepted with NO migration / no whitelist
    }

    // ── full write → read round-trip through the real handlers ──

    [Fact]
    public async Task A_goal_weight_entry_round_trips_via_LogMetricEntry_then_GetMyMetricSeries()
    {
        var userId = Guid.NewGuid();
        var repo = new InMemoryMetricRepository();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        var writeHandler = new LogMetricEntryHandler(repo, Substitute.For<IUnitOfWork>(), currentUser);
        var readHandler = new GetMyMetricSeriesHandler(repo, currentUser);

        // Write a goal-weight check-in for the caller (free-text type, the same command the body-weight UI uses).
        var write = await writeHandler.Handle(
            new LogMetricEntryCommand(GoalWeightType, 75m, "kg", Today), CancellationToken.None);
        Assert.True(write.IsSuccess);

        // Read it back as a metric series — the frontend reuses the Phase-2a series endpoint, no new read.
        var read = await readHandler.Handle(
            new GetMyMetricSeriesQuery(GoalWeightType, null, null), CancellationToken.None);

        Assert.True(read.IsSuccess);
        Assert.Equal(GoalWeightType, read.Value!.Type);
        Assert.Equal("kg", read.Value.Unit);
        var point = Assert.Single(read.Value.Points);
        Assert.Equal(Today, point.LocalDate);
        Assert.Equal(75m, point.Value);
    }

    [Fact]
    public async Task The_latest_goal_weight_per_day_wins_so_a_re_set_goal_replaces_the_old_one()
    {
        var userId = Guid.NewGuid();
        var repo = new InMemoryMetricRepository();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.TimeZoneId.Returns("UTC");

        var writeHandler = new LogMetricEntryHandler(repo, Substitute.For<IUnitOfWork>(), currentUser);
        var readHandler = new GetMyMetricSeriesHandler(repo, currentUser);

        // The user revises their goal the same day — append-only, the later entry is the current goal.
        Assert.True((await writeHandler.Handle(
            new LogMetricEntryCommand(GoalWeightType, 78m, "kg", Today), CancellationToken.None)).IsSuccess);
        repo.AdvanceClock();
        Assert.True((await writeHandler.Handle(
            new LogMetricEntryCommand(GoalWeightType, 74m, "kg", Today), CancellationToken.None)).IsSuccess);

        var read = await readHandler.Handle(
            new GetMyMetricSeriesQuery(GoalWeightType, null, null), CancellationToken.None);

        Assert.True(read.IsSuccess);
        var point = Assert.Single(read.Value!.Points);
        Assert.Equal(74m, point.Value); // latest-per-day: the revised goal wins
    }

    /// <summary>
    /// A minimal in-memory MetricEntry store that honours the two contract methods the round-trip uses:
    /// AddAsync appends; GetOwnSeriesAsync filters to the owner + normalized type + range and returns
    /// LocalDate asc, LoggedAtUtc asc (the order the latest-per-day reduction depends on). A monotonic clock
    /// makes two same-day writes deterministically ordered without sleeping.
    /// </summary>
    private sealed class InMemoryMetricRepository : IMetricEntryRepository
    {
        private readonly List<MetricEntry> _entries = new();
        private DateTimeOffset _clock = DateTimeOffset.UtcNow;

        public void AdvanceClock() => _clock = _clock.AddHours(1);

        public Task AddAsync(MetricEntry entry, CancellationToken cancellationToken = default)
        {
            // Stamp a deterministic LoggedAtUtc so same-day ordering is stable (the factory stamps UtcNow).
            typeof(MetricEntry).GetProperty(nameof(MetricEntry.LoggedAtUtc))!.SetValue(entry, _clock);
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MetricEntry>> GetOwnForDateAsync(
            Guid traineeId, DateOnly localDate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetricEntry>>(
                _entries
                    .Where(e => e.TraineeId == traineeId && e.LocalDate == localDate)
                    .OrderByDescending(e => e.LoggedAtUtc)
                    .ToList());

        public Task<IReadOnlyList<MetricEntry>> GetOwnSeriesAsync(
            Guid traineeId, string type, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
            var result = _entries
                .Where(e => e.TraineeId == traineeId
                    && e.Type.ToLowerInvariant() == normalized
                    && e.LocalDate >= from
                    && e.LocalDate <= to)
                .OrderBy(e => e.LocalDate)
                .ThenBy(e => e.LoggedAtUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<MetricEntry>>(result);
        }
    }
}
