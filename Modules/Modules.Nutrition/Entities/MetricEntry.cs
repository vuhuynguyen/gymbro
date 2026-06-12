using BuildingBlocks.Shared.DomainPrimitives;

namespace Modules.NutritionModule.Entities;

/// <summary>
/// One point in the trainee's personal body-metric series (weight, sleep, water, …) — the MVP slice of the
/// DOMAIN_MODEL §8 extensibility spine. Append-only and keyed by (trainee, local date); the newest entry per
/// type on a date is "latest". <c>Type</c> is a free-form short string (the wire contract sends
/// "weight"/"sleep"/…), kept stringly here so a new signal is a new value, never a migration.
///
/// Deliberately NOT <see cref="ITenantEntity"/>: writes arrive on the self-scoped <c>/api/me</c> surface with
/// no <c>X-Tenant-Id</c> and no assignment is required to check in, so there is no natural gym to stamp
/// (unlike <see cref="DailyNutritionLog"/>, which is opened from a tenant-stamped assignment). The series is
/// personal and cross-gym; isolation is per-user — every handler scopes strictly to
/// <c>currentUser.UserId</c>, and <see cref="ISoftDelete"/> keeps it under the soft-delete global filter.
/// </summary>
public sealed class MetricEntry : AggregateRoot, ISoftDelete
{
    public const int TypeMaxLength = 50;
    public const int UnitMaxLength = 20;

    public Guid TraineeId { get; private set; }

    /// <summary>Free-form metric kind ("weight", "sleep", …), trimmed, max 50 chars.</summary>
    public string Type { get; private set; } = string.Empty;

    public decimal Value { get; private set; }
    public string? Unit { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public DateTimeOffset LoggedAtUtc { get; private set; }

    private MetricEntry() { }

    public static MetricEntry Log(Guid traineeId, string type, decimal value, string? unit, DateOnly localDate)
    {
        if (traineeId == Guid.Empty) throw new DomainException("TraineeId is required.");
        if (string.IsNullOrWhiteSpace(type)) throw new DomainException("Metric type is required.");

        return new MetricEntry
        {
            Id = Guid.NewGuid(),
            TraineeId = traineeId,
            Type = type.Trim(),
            Value = value,
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            LocalDate = localDate,
            LoggedAtUtc = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
    }
}
