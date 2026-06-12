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
