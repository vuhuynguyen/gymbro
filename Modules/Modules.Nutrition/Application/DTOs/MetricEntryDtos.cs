namespace Modules.NutritionModule.Application.DTOs;

/// <summary>Wire shape for one check-in metric entry (camelCase on the wire; localDate = "YYYY-MM-DD").</summary>
public sealed record MetricEntryDto(
    string Type,
    decimal Value,
    string? Unit,
    DateOnly LocalDate,
    DateTimeOffset LoggedAtUtc);

/// <summary>The day's entries, NEWEST FIRST — the client takes the first entry per type as "latest".</summary>
public sealed record MetricEntryListDto(IReadOnlyList<MetricEntryDto> Items);

// ── Progress page — body-metric trend series (api/me/progress/metrics/series, Phase 2) ──

/// <summary>One latest-per-local-day point on a metric trend (e.g. a day's final bodyweight check-in).</summary>
public sealed record MetricSeriesPointDto(
    DateOnly LocalDate,
    decimal Value);

/// <summary>
/// A body-metric trend over a local-date range (latest-per-day). <see cref="Type"/> echoes the requested,
/// normalized kind; <see cref="Unit"/> is the most-recent non-null unit seen in range (null if none).
/// <see cref="Points"/> is empty (200, never 404) when the caller logged nothing of that type in range.
/// </summary>
public sealed record MetricSeriesDto(
    string Type,
    string? Unit,
    IReadOnlyList<MetricSeriesPointDto> Points);
