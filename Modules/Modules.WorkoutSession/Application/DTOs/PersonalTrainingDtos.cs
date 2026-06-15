namespace Modules.WorkoutSessionModule.Application.DTOs;

/// <summary>A lifetime personal record for one lift: the best estimated-1RM working set across all gyms.</summary>
public sealed record PersonalRecordDto(
    Guid ExerciseId,
    string? ExerciseName,
    decimal WeightKg,
    int Reps,
    decimal EstimatedOneRepMaxKg,
    DateTimeOffset AchievedAt);

public sealed record PersonalRecordListDto(
    IReadOnlyList<PersonalRecordDto> Records);

/// <summary>One Monday-anchored training week of the caller's unified history.</summary>
public sealed record ProgressWeekDto(
    DateOnly WeekStart,
    int Sessions,
    int TotalSets,
    decimal TotalVolumeKg);

/// <summary>Unified personal training analytics across every gym the caller participates in.</summary>
public sealed record ProgressDto(
    int TotalSessions,
    int TotalCompletedSessions,
    decimal TotalVolumeKg,
    int TotalSets,
    IReadOnlyList<ProgressWeekDto> Weeks);

// ── Progress page — the single-call trainee "glance" overview (api/me/progress/overview, Phase 1) ──

/// <summary>Which way a lift's strength is trending versus its trailing-4-week baseline.</summary>
public enum LiftTrendDirection
{
    Up,
    Flat,
    Down
}

/// <summary>
/// Current-week adherence for the trainee, completed-sessions only, against the authoritative active-plan
/// goal (Decision D1). <see cref="Goal"/> is null and <see cref="HasActivePlan"/> false when the trainee has
/// no active assignment — the client then hides the ring and shows the raw <see cref="CompletedSessions"/>.
/// </summary>
public sealed record WeekAdherenceDto(
    DateOnly WeekStart,
    int CompletedSessions,
    int? Goal,
    bool HasActivePlan);

/// <summary>One local day in the consistency window that carried at least one completed session.</summary>
public sealed record ConsistencyDayDto(
    DateOnly Date,
    int SessionCount);

/// <summary>
/// 12-week training consistency. <see cref="Days"/> lists only days with ≥1 completed session (the client
/// fills the rest of the grid). <see cref="ConsistencyPct"/> and <see cref="CurrentStreakWeeks"/> are
/// null/0 when the trainee has no goal to measure adherence against.
/// </summary>
public sealed record ConsistencyDto(
    int WindowWeeks,
    IReadOnlyList<ConsistencyDayDto> Days,
    int? ConsistencyPct,
    int CurrentStreakWeeks);

/// <summary>
/// Per-lift strength direction over the 12-week window, honesty-gated (working sets, e1RM present,
/// reps ≤ 12, strength/bodyweight tracking). One e1RM point per session = the max qualifying working-set
/// e1RM in that session.
/// </summary>
public sealed record LiftDirectionDto(
    Guid ExerciseId,
    string? ExerciseName,
    decimal CurrentE1rmKg,
    decimal DeltaKgVsTrailing4w,
    LiftTrendDirection Direction,
    bool Stalled,
    int StallSessions,
    IReadOnlyList<decimal> SparkE1rmKg);

/// <summary>
/// The trainee Progress home in one payload: this-week adherence, 12-week consistency, top-lift strength
/// direction, and a PR teaser. Always returned (empty-but-valid for a brand-new user) — never a 204.
/// </summary>
public sealed record ProgressOverviewDto(
    WeekAdherenceDto ThisWeek,
    ConsistencyDto Consistency,
    IReadOnlyList<LiftDirectionDto> TopLifts,
    IReadOnlyList<PersonalRecordDto> RecentPrs,
    DateTimeOffset GeneratedAtUtc);

// ── Progress page — per-lift e1RM series (api/me/exercises/{id}/e1rm-series, Phase 2) ──

/// <summary>
/// One session-best e1RM point on a lift's series. <see cref="SessionBestE1rmKg"/> is the MAX qualifying
/// working-set e1RM for that session; <see cref="TopSetWeightKg"/>/<see cref="TopSetReps"/> capture the set
/// that produced it (the chart's tooltip). <see cref="IsPr"/> is true when this session's best strictly
/// exceeded the running max so far — PR markers are derived HERE from the series, never from /api/me/records.
/// </summary>
public sealed record E1rmSeriesPointDto(
    DateOnly Date,
    decimal SessionBestE1rmKg,
    decimal TopSetWeightKg,
    int TopSetReps,
    bool IsPr);

/// <summary>
/// The full per-lift e1RM drill-down (the chart behind the home sparkline) over the requested window, plus
/// the same Current/Delta/Direction/Stall summary the overview shows — both computed via the shared
/// <c>E1rmSeriesCalculator</c>. <see cref="Points"/> is empty (200, never 404) for an unknown or never-trained
/// lift; honesty-gated identically to the overview (Working ∧ e1RM ∧ reps ≤ 12 ∧ Strength/Bodyweight).
/// </summary>
public sealed record ExerciseE1rmSeriesDto(
    Guid ExerciseId,
    string? ExerciseName,
    string TrackingType,
    IReadOnlyList<E1rmSeriesPointDto> Points,
    decimal CurrentE1rmKg,
    decimal DeltaKgVsTrailing4w,
    LiftTrendDirection Direction,
    bool Stalled,
    int StallSessions);

// ── Progress page — COACH surface (tenant-scoped, OWN GYM ONLY; api/clients/*, Phase 2b) ──
// CRITICAL (FEASIBILITY R2): every coach read here is computed over TENANT-FILTERED sessions
// (sessionRepository.Query(), EF tenant filter ON) — NEVER QueryOwnAcrossGyms. Cross-gym training is
// invisible by design ("this gym only"). See COACH-VS-TRAINEE.md §4.

/// <summary>The coach's needs-attention triage verdict for one client (Decision D4 — three cheap states
/// only; "Stalled" is resolved on client-open, never per roster row).</summary>
public enum RosterStatus
{
    OnTrack,
    Drifting,
    Quiet
}

/// <summary>
/// One client row of the coach roster, computed from TENANT-SCOPED sessions in the active gym only.
/// <see cref="LastActiveAt"/> = MAX(StartedAt) in this gym (null if the member never trained here);
/// <see cref="CompletedThisWeek"/> = completed sessions this Monday-week in this gym;
/// <see cref="WeeklyGoal"/> = the member's in-gym active <c>PlanAssignment.FrequencyDaysPerWeek</c> (null if
/// none); <see cref="AdherencePct"/> = weeks-hitting-goal ÷ weeks-observed over the in-gym window (null
/// without a goal/sessions); <see cref="Status"/> = cheap-signal triage classifier.
/// </summary>
public sealed record ClientStatusDto(
    Guid TraineeId,
    string DisplayName,
    DateTimeOffset? LastActiveAt,
    int CompletedThisWeek,
    int? WeeklyGoal,
    int? AdherencePct,
    RosterStatus Status);

/// <summary>
/// The coach roster: the active gym's members who have at least one session here, sorted at-risk-first
/// (Quiet, then Drifting, then OnTrack). Always returned (empty <see cref="Items"/> when the gym has no
/// members with sessions) — never a 204. The "this gym only" caption is a UI obligation, not a field.
/// </summary>
public sealed record RosterDto(
    IReadOnlyList<ClientStatusDto> Items);

/// <summary>
/// One client's per-lift e1RM trend for the coach detail view, a trimmed top-lift variant of the §2
/// <see cref="ExerciseE1rmSeriesDto"/> shape. Built from TENANT-SCOPED sessions in THIS gym only (filter
/// ON) — the coach never sees the client's cross-gym work. Honesty-gated identically to the trainee series
/// (Working ∧ e1RM ∧ reps ≤ 12 ∧ Strength/Bodyweight); one e1RM point per session = MAX qualifying
/// working-set e1RM; summary via the shared <c>E1rmSeriesCalculator</c>.
/// </summary>
public sealed record LiftTrendDto(
    Guid ExerciseId,
    string? ExerciseName,
    string TrackingType,
    decimal CurrentE1rmKg,
    decimal DeltaKgVsTrailing4w,
    LiftTrendDirection Direction,
    bool Stalled,
    int StallSessions,
    IReadOnlyList<decimal> SparkE1rmKg);

/// <summary>
/// The coach's gentle training-load nudge for one client (acute vs chronic), Phase 4 — TENANT-SCOPED to the
/// active gym (filter ON, never <c>QueryOwnAcrossGyms</c>; a client's cross-gym volume is invisible here).
///
/// <para>Deliberately exposes the TWO RAW VOLUMES, never their ratio (FEASIBILITY R10): an ACWR ratio reads as
/// a clinical injury claim on integer/sparse RPE-free data, so we surface the acute and chronic loads side by
/// side and leave the verdict soft. <see cref="AcuteVolumeKg"/> = Σ working-set volume (Σ weight×reps over
/// Working sets carrying both values — the SAME computation as <c>SessionMapping.ComputeVolumeKg</c>) over the
/// last 7 days in this gym; <see cref="ChronicWeeklyVolumeKg"/> = (Σ working-set volume over the last 28 days)
/// ÷ 4 = the average weekly load. Volume only — RPE/duration are too sparse to weight load (R10). Both zero
/// when there are no in-gym sessions in the window (200, never 204).</para>
/// </summary>
public sealed record AcuteChronicLoadDto(
    decimal AcuteVolumeKg,
    decimal ChronicWeeklyVolumeKg,
    LoadTrend Trend);

/// <summary>
/// A SOFT band over the acute-vs-chronic comparison — a gentle nudge, never a medical/injury claim, and never
/// derived from an exposed ACWR ratio. <c>Ramping</c> when the recent week is well above the chronic average,
/// <c>Detraining</c> when well below, else <c>Steady</c>. Serialized camelCase on the wire.
/// </summary>
public enum LoadTrend
{
    Detraining,
    Steady,
    Ramping
}
