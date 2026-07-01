# Progress Page — Frozen API Contracts

> **Status:** **Phase 1 contract is FROZEN** — an engineer can implement it without interpreting the design.
> Phase 2+ endpoints are specified at design level and tagged `Frozen at Phase N`. This is the single authoritative
> source for Progress endpoint shapes; [FEASIBILITY.md](FEASIBILITY.md) keeps the *why* (query strategy, isolation
> risks) and must not redefine a shape that lives here. Verified against the real `MeController` conventions
> (`gymbro/Presentations/WebApi/Controllers/MeController.cs`) — see [IMPLEMENTATION.md](IMPLEMENTATION.md) §Conventions.

**Related:** [PHASE-1.md](PHASE-1.md) · [IMPLEMENTATION.md](IMPLEMENTATION.md) · [FEASIBILITY.md](FEASIBILITY.md) · [../PERMISSIONS.md](../PERMISSIONS.md) · [../ARCHITECTURE.md](../ARCHITECTURE.md)

---

## 0. House conventions (apply to every endpoint below)

These are observed from the existing `/api/me/progress` and `/api/me/records` endpoints — match them exactly; do not invent new patterns.

| Concern | Rule |
|---|---|
| **CQRS** | `sealed record Query : IRequest<Result<TDto>>` → `sealed class QueryHandler` (primary-ctor DI) → thin controller action `mediator.Send`. Handlers return `Result<T>`, never throw for expected outcomes. |
| **Self-scoped (trainee)** | Route prefix `api/me`, class-level `[Authorize]` (any authenticated role), **no `X-Tenant-Id`**. Cross-gym data via `IWorkoutSessionRepository.QueryOwnAcrossGyms(currentUser.UserId)` — **only ever the current user's id**. Add a matching entry to `Tests/Authorization/TenantAuthorizationExemptions.cs`. |
| **Tenant-scoped (coach)** | Route prefix `api/clients` (or existing `api/sessions`), `X-Tenant-Id` required, gated by `WorkoutLogViewAll` + `ResourceAccessGuard`, EF tenant filter **ON**. A **separate handler** from the trainee path — never parameterize `QueryOwnAcrossGyms` by another user's id ([FEASIBILITY R2](FEASIBILITY.md)). |
| **Empty result** | **`200 OK` with an empty-but-valid DTO** (empty lists, null optionals). **Not `204`.** `204` is reserved for void writes on `MeController`; the `/sessions/active` 204 convention is a *different* controller and is **not** copied here. |
| **Errors** | `result.ToFailureResult(this)` → status from `Error.Type` (`Validation`→400, `NotFound`→404, `Unauthorized`/`Forbidden`→**403**, `Conflict`→409), body is the **bare `Error.Message` string**. |
| **Enums on the wire** | Serialized as **camelCase strings** out; case/int-tolerant in. |
| **Versioning** | `X-Api-Version` header, handled by `ApiVersionMiddleware`. **No `/v1` in the path.** New endpoints inherit this for free — add no attribute. |
| **Caching** | **Phase 1: none.** Self-scoped Progress reads are not cached today; compute on read. Caching is a deferred optimization ([FEASIBILITY §4](FEASIBILITY.md)) introduced only if profiling shows need. |
| **DTOs** | `sealed record` with positional params, in `Application/DTOs/PersonalTrainingDtos.cs`. Weight/volume fields suffixed `Kg`; day anchors `DateOnly`; instants `DateTimeOffset`; collections `IReadOnlyList<T>`. |

---

## 1. `GET /api/me/progress/overview` — **FROZEN (Phase 1)**

The entire trainee Progress home is **one call**. Computes adherence + consistency + top-lift strength direction + a PR teaser server-side, so the client never re-derives adherence from the all-status `ProgressWeekDto.Sessions` (which would wrongly count abandoned/in-progress sessions).

**Request**
```http
GET /api/me/progress/overview
Authorization: Bearer <access-jwt>
X-Api-Version: 1.0            # optional; defaults to 1.0
# NO X-Tenant-Id — self-scoped, cross-gym
```
No query parameters. The window is fixed server-side at **12 weeks** for consistency, **trailing 4 weeks** for strength direction.

**Response `200 OK` → `ProgressOverviewDto`** (always; empty-but-valid for new users)

```csharp
// Application/DTOs/PersonalTrainingDtos.cs  (namespace Modules.WorkoutSessionModule.Application.DTOs)

public sealed record ProgressOverviewDto(
    WeekAdherenceDto ThisWeek,
    ConsistencyDto Consistency,
    IReadOnlyList<LiftDirectionDto> TopLifts,      // 0–3 lifts, honesty-gated
    IReadOnlyList<PersonalRecordDto> RecentPrs,    // reuse existing DTO, top 3 by e1RM
    DateTimeOffset GeneratedAtUtc,
    // … v2 window-differentiation fields (Period/StrengthGain/MuscleVolume/Load/Coach) — see WINDOW-DIFFERENTIATION.md …
    bool HasEverTrained);                          // window-INDEPENDENT: ever completed a session (any gym, any time)

public sealed record WeekAdherenceDto(
    DateOnly WeekStart,            // Monday, in trainee's ClientTimezone/user TZ
    int CompletedSessions,         // Status == Completed only
    int? Goal,                     // authoritative FrequencyDaysPerWeek; null if no active plan
    bool HasActivePlan);

public sealed record ConsistencyDto(
    int WindowWeeks,                          // = 12
    IReadOnlyList<ConsistencyDayDto> Days,    // only days with >=1 completed session
    int? ConsistencyPct,                      // weeks hitting goal ÷ weeks observed (D10); null if no goal/sessions
    int CurrentStreakWeeks);                  // consecutive weeks hitting goal; 0 if no goal

public sealed record ConsistencyDayDto(
    DateOnly Date,                 // local day
    int SessionCount);

public sealed record LiftDirectionDto(
    Guid ExerciseId,
    string? ExerciseName,          // durable PerformedExercise snapshot
    decimal CurrentE1rmKg,         // latest session-best working-set e1RM
    decimal DeltaKgVsTrailing4w,   // current − mean(session-best e1RM over prior 4 wk)
    LiftTrendDirection Direction,  // "up" | "flat" | "down" (camelCase on wire)
    bool Stalled,                  // best e1RM not exceeded in last K=3 exposures
    int StallSessions,             // exposures since last new best (0 if not stalled)
    IReadOnlyList<decimal> SparkE1rmKg);  // up to 8 recent session-best points, oldest→newest

public enum LiftTrendDirection { Up, Flat, Down }
```

**Business rules (frozen)**
- **Honesty gate (server-side, in the query):** a set contributes to e1RM only if `SetType == Working`, `EstimatedOneRepMaxKg != null`, `Reps <= 12`, and `PerformedExercise.TrackingType ∈ {Strength, Bodyweight}`. (`EstimatedOneRepMaxKg` is **not** suppressed for high reps in the data — the query must apply `Reps <= 12` itself.)
- **One e1RM point per session per lift** = `MAX(EstimatedOneRepMaxKg)` over that session's qualifying working sets. Drop/AMRAP/failure stages carry null e1RM, so `MAX` is cluster-safe — do **not** filter `ParentSetId`.
- **Top-lift selection:** the up-to-3 lifts with the most qualifying e1RM **sessions** in the 12-week window, **requiring ≥4 sessions** (below that, direction is noise — omit the lift). New users → `TopLifts: []`.
- **Direction:** `Up` if `DeltaKgVsTrailing4w > +0.5 kg`, `Down` if `< −0.5 kg`, else `Flat`.
- **Stall:** `Stalled = true` when the lift's best session e1RM has not set a new max in the last **3** exposures; `StallSessions` = exposures since the last new best.
- **Adherence:** `CompletedSessions` = `Status == Completed` only, current Monday-anchored week in the trainee's zone. `Goal` = `FrequencyDaysPerWeek` of the **authoritative** active assignment (Decision **D1**: active assignment with the most completed sessions this week, tie-broken by latest `StartDate`). No active assignment → `Goal: null`, `HasActivePlan: false` (client hides the ring, shows raw `CompletedSessions`).
- **Consistency:** `Days` lists only local days with ≥1 completed session over 12 weeks; the client renders the full grid and fills gaps. `ConsistencyPct` = weeks-hitting-goal ÷ **weeks observed**, where weeks-observed counts from the **first completed session in the window** through the current week, capped at 12 (**D10** — forgiving for new users, honest for laggards); null when there's no goal or no sessions. `CurrentStreakWeeks` = 0 when no goal. **Forgiving:** prescribed rest is not a miss; never expose a "broken streak" red state.
- **PR teaser:** `RecentPrs` = top 3 of the existing `GetMyPersonalRecordsQuery` result (current best per lift, already e1RM-sorted) via an internal mediator call. **Single source for PRs** (Decision **D2**) — never sum `PrCount`.
- **New-user gate (`HasEverTrained`):** a **window-independent** flag — `true` iff the trainee has **ever** completed a session (any gym, any time), computed OUTSIDE the look-back window. The client shows the brand-new-user first-run hero **only** when this is `false`, so a *returning* trainee whose **selected window is empty** (e.g. the default Week view early in a fresh week, before this week's first session) keeps the normal dashboard instead of "Start your first session". Cheap: the windowed read already proves training whenever it returns a row, so only an empty window pays the extra all-time `EXISTS`. Older clients that don't read the flag fall back to the legacy windowed-emptiness heuristic, so the field is purely additive.

**Sample response**
```json
{
  "thisWeek": { "weekStart": "2026-06-08", "completedSessions": 3, "goal": 4, "hasActivePlan": true },
  "consistency": {
    "windowWeeks": 12,
    "days": [ { "date": "2026-06-09", "sessionCount": 1 }, { "date": "2026-06-11", "sessionCount": 1 } ],
    "consistencyPct": 78, "currentStreakWeeks": 5
  },
  "topLifts": [
    { "exerciseId": "…", "exerciseName": "Barbell Bench Press", "currentE1rmKg": 96.0,
      "deltaKgVsTrailing4w": 2.4, "direction": "up", "stalled": false, "stallSessions": 0,
      "sparkE1rmKg": [90.0, 91.5, 92.0, 93.0, 94.5, 95.0, 96.0] },
    { "exerciseId": "…", "exerciseName": "Deadlift", "currentE1rmKg": 153.0,
      "deltaKgVsTrailing4w": 0.0, "direction": "flat", "stalled": true, "stallSessions": 4,
      "sparkE1rmKg": [151.0, 153.0, 152.0, 153.0, 153.0] }
  ],
  "recentPrs": [
    { "exerciseId": "…", "exerciseName": "Deadlift", "weightKg": 140.0, "reps": 3,
      "estimatedOneRepMaxKg": 153.0, "achievedAt": "2026-06-09T18:22:00+07:00" }
  ],
  "generatedAtUtc": "2026-06-14T03:00:00Z",
  "hasEverTrained": true
}
```

**Authorization** `[Authorize]`, any authenticated role. Self-scoped via `QueryOwnAcrossGyms(currentUser.UserId)`. Add an exemption entry in `Tests/Authorization/TenantAuthorizationExemptions.cs`.

**Pagination / filtering / sorting** None — fixed-window aggregate. `TopLifts` sorted by session-count desc; `RecentPrs` by e1RM desc; `Days` by date asc.

**Performance target** p95 **< 350 ms** server-side. ~4 bounded reads (completed-sessions window scan on `(TraineeId, StartedAt)`+`(TraineeId, Status)`; top-3 e1RM series; one assignment-goal lookup; top-3 records). No N+1. No caching in Phase 1.

**Query strategy** See [FEASIBILITY §3](FEASIBILITY.md) and [PHASE-1.md §Backend](PHASE-1.md). No migration; no new index required for launch (the optional `IX_PerformedSets_Working_E1rm` is added only if profiling demands).

---

## 2. `GET /api/me/exercises/{exerciseId}/e1rm-series` — **FROZEN (Phase 2)**

Full per-lift series for the strength drill-down (the chart behind the home sparkline) + derived PR markers.
Self-scoped via `QueryOwnAcrossGyms(currentUser.UserId)`; honesty-gated and reduced identically to §1 — the
shared `E1rmSeriesCalculator` (Modules.WorkoutSession/Application) owns the Current/Delta/Direction/Stall math,
so the drill-down and the home sparkline agree by construction.

**Request**
```http
GET /api/me/exercises/{exerciseId:guid}/e1rm-series?from=&to=     # self-scoped, cross-gym; NO X-Tenant-Id
```
`from`/`to` are optional **local-day** bounds (the trainee's zone); default = trailing **12 weeks** ending today.

**Response `200 OK` → `ExerciseE1rmSeriesDto`** (always; empty `Points` for an unknown/never-trained lift — never 404)

```csharp
// Application/DTOs/PersonalTrainingDtos.cs  (namespace Modules.WorkoutSessionModule.Application.DTOs)

public sealed record ExerciseE1rmSeriesDto(
    Guid ExerciseId,
    string? ExerciseName,                       // durable PerformedExercise snapshot; null for an unknown lift
    string TrackingType,                        // "Strength" | "Bodyweight" (camelCase on the wire)
    IReadOnlyList<E1rmSeriesPointDto> Points,   // oldest → newest, one per qualifying session
    decimal CurrentE1rmKg,                       // latest session-best (0 when no points)
    decimal DeltaKgVsTrailing4w,                 // current − mean(session-best e1RM over prior 4 wk)
    LiftTrendDirection Direction,                // "up" | "flat" | "down"
    bool Stalled,                                // best e1RM not exceeded in last K=3 exposures
    int StallSessions);                          // exposures since last new best (0 if not stalled)

public sealed record E1rmSeriesPointDto(
    DateOnly Date,                  // the session's local day
    decimal SessionBestE1rmKg,      // MAX qualifying working-set e1RM in that session
    decimal TopSetWeightKg,         // weight of the set that produced SessionBestE1rmKg
    int TopSetReps,                 // reps of that set
    bool IsPr);                     // session-best strictly exceeds the running max so far
```

**Business rules (frozen)**
- **Honesty gate** = §1's exactly: a set contributes only if `SetType == Working`, `EstimatedOneRepMaxKg != null`, `WeightKg != null`, `Reps != null && Reps <= 12`, and `TrackingType ∈ {Strength, Bodyweight}`. Drop/AMRAP/failure stages carry null e1RM, so they neither count nor exclude the session.
- **One point per session** = `MAX(EstimatedOneRepMaxKg)` over that session's qualifying working sets; `TopSetWeightKg`/`TopSetReps` capture the set that produced that MAX (the chart's tooltip).
- **`IsPr`** is derived **here** from the series (running-max walk, oldest→newest): true when the session-best **strictly** exceeds the prior running max (the first qualifying session is always a PR; an exact tie is **not** a PR). Never from `/api/me/records` ([FEASIBILITY R3](FEASIBILITY.md)).
- **Summary** (`Current/Delta/Direction/Stall`) is computed via the shared `E1rmSeriesCalculator` over the same session-best series — same thresholds as §1 (`Up`/`Down` at ±0.5 kg vs the trailing-4-week mean; stall at K=3).
- **Empty:** an unknown/never-trained lift returns `ExerciseName: null`, `TrackingType: "Strength"`, `Points: []`, zeroed summary, `Direction: flat`.

Frontend per-lift trend chart is hand-rolled `CustomPaint` — **no `fl_chart`** (Decision **D11**).

## 3. `GET /api/me/progress/metrics/series` — **FROZEN (Phase 2)**

Bodyweight (and later sleep) trend. Backed by a new `MetricEntryRepository.GetOwnSeriesAsync(traineeId, type, from, to)` on the **Nutrition** module (own-scoped: `IgnoreQueryFilters` + explicit `TraineeId` + `!IsDeleted`) — **no new column, no migration**.

**Request**
```http
GET /api/me/progress/metrics/series?type=weight&from=&to=         # self-scoped, cross-gym; NO X-Tenant-Id
```
`type` is required free text; `from`/`to` are optional **local-day** bounds; default = trailing **12 weeks** ending today.

**Response `200 OK` → `MetricSeriesDto`** (always; empty `Points` if none in range — never 404)

```csharp
// Application/DTOs/MetricEntryDtos.cs  (namespace Modules.NutritionModule.Application.DTOs)

public sealed record MetricSeriesDto(
    string Type,                                 // the normalized (trimmed, lowercased) requested type, echoed
    string? Unit,                                // most-recent non-null unit seen in range; null if none
    IReadOnlyList<MetricSeriesPointDto> Points); // latest-per-local-day, ordered by day asc

public sealed record MetricSeriesPointDto(
    DateOnly LocalDate,
    decimal Value);                              // the day's LATEST check-in value for this type
```

**Business rules (frozen)**
- **Latest-per-day:** one point per local day = the **newest** check-in that day (`GetOwnSeriesAsync` returns `LocalDate asc, LoggedAtUtc asc`; the handler takes the last entry per day).
- **`type` matched case-insensitively / normalized** (trimmed + lowercased) — `MetricEntry.Type` is unvalidated free text ([FEASIBILITY R9](FEASIBILITY.md)). `Type` on the wire echoes the normalized value.
- **`Unit`** = the most-recent non-null unit logged in range (null when none); the client labels the axis from it.

Until the Body section ships, it renders an empty-state invite, never a faked line.

## 4. Coach — **FROZEN (Phase 2b)** (tenant-scoped, own gym only)

Implemented as **two separate tenant-scoped handlers** on a new `ClientsController` (`api/clients`), the coach
counterpart to `MeController`. Every per-client signal is computed over the **tenant-filtered** session query
(EF tenant filter **ON**) — a client's cross-gym training is invisible here by design ([FEASIBILITY R2](FEASIBILITY.md)).
Both endpoints require `X-Tenant-Id`, are gated by `WorkoutLogViewAll` (+ `ResourceAccessGuard` for the
per-client read), return `Result<T>`, and follow the §0 conventions (200-with-empty, camelCase enums on the wire).

### 4.1 `GET /api/clients/progress/roster`

**Request**
```http
GET /api/clients/progress/roster
Authorization: Bearer <access-jwt>
X-Tenant-Id: <gym-id>           # required
```
No query parameters. The window is fixed server-side at **12 weeks**; the quiet gap is **10 days** and the
adherence band is **75%**.

**Response `200 OK` → `RosterDto`** (always; empty `Items` when the gym has no members with sessions)

```csharp
// Modules.WorkoutSession/Application/DTOs/PersonalTrainingDtos.cs
//   (namespace Modules.WorkoutSessionModule.Application.DTOs)

public sealed record RosterDto(
    IReadOnlyList<ClientStatusDto> Items);

public sealed record ClientStatusDto(
    Guid TraineeId,
    string DisplayName,
    DateTimeOffset? LastActiveAt,   // MAX(StartedAt), tenant-scoped; null if never trained here
    int CompletedThisWeek,          // completed this Monday-week, in the CLIENT's zone, this gym
    int? WeeklyGoal,                // in-gym active PlanAssignment.FrequencyDaysPerWeek; null if none
    int? AdherencePct,              // weeks-hitting-goal ÷ weeks-observed (in-gym); null without a goal/sessions
    RosterStatus Status);

public enum RosterStatus { OnTrack, Drifting, Quiet }   // "onTrack" | "drifting" | "quiet" on the wire
```

**Business rules (frozen)**
- **Members:** the active tenant's `Client` members (resolved via the internal `ResolveTenantMemberNamesQuery`),
  filtered to those with **≥1 in-gym completed session** — others are omitted (empty `Items` if none).
- **`LastActiveAt`** = `MAX(StartedAt)` over all in-gym completed sessions (tenant-filtered), null if none.
- **`CompletedThisWeek` / weeks** are bucketed Monday-anchored in the **client's** zone, never the coach's.
- **`WeeklyGoal`** = the member's **in-gym** active assignment goal (tenant-filtered, resolved via the WorkoutPlan
  `ResolveActiveAssignmentGoalsQuery`; most-recent `StartDate` wins on multiples). Never a cross-gym goal.
- **`Status` (Decision D4 — cheap signals only, NO "Stalled" at roster scale):** `Quiet` when no session in
  **10** days (`now − MAX(StartedAt)`); else `Drifting` when `AdherencePct < 75`; else `OnTrack`.
- **Sort:** at-risk-first — `Quiet`, then `Drifting`, then `OnTrack`; within a status, the longest-quiet client
  leads, then a stable name sort.
- The **"this gym only"** caption is a UI obligation, not a field.

### 4.2 `GET /api/clients/{traineeId}/progress/strength?take=6`

**Request**
```http
GET /api/clients/{traineeId:guid}/progress/strength?take=6
X-Tenant-Id: <gym-id>           # required
```
`take` (default **6**, clamped 1–12) bounds the number of top lifts returned.

**Response `200 OK` → `IReadOnlyList<LiftTrendDto>`** (a trimmed top-lift variant of the §2 series shape;
empty list when the client has no qualifying in-gym lifts)

```csharp
public sealed record LiftTrendDto(
    Guid ExerciseId,
    string? ExerciseName,            // durable PerformedExercise snapshot
    string TrackingType,             // "Strength" | "Bodyweight" (camelCase on the wire)
    decimal CurrentE1rmKg,
    decimal DeltaKgVsTrailing4w,
    LiftTrendDirection Direction,    // "up" | "flat" | "down"
    bool Stalled,
    int StallSessions,
    IReadOnlyList<decimal> SparkE1rmKg);   // up to 8 session-best points, oldest → newest
```

**Business rules (frozen)**
- **Critical ([FEASIBILITY R2](FEASIBILITY.md)):** a **separate handler** from the trainee §2 series, reading the
  **tenant-filtered** `Query()` (filter **ON**). It **never** calls `QueryOwnAcrossGyms` and is never parameterized
  by `traineeId` over the self-scoped path — the coach sees only the client's **in-gym** sessions.
- **Authorization:** `WorkoutLogViewAll` + `ResourceAccessGuard` bind the **coach** to their own gym; the
  **trainee** must also be a member of the active tenant (`ITenantRoleResolver`) — a non-member id returns
  **`NotFound`** (HTTP 404), **never** a silent rescope to self or an empty 200.
- **Honesty gate + reduction** are identical to §2 (Working ∧ e1RM ∧ reps ≤ 12 ∧ Strength/Bodyweight; one MAX
  point per session); top lifts are the most-trained, requiring **≥4** qualifying sessions; the
  Current/Delta/Direction/Stall/Spark summary is computed via the shared `E1rmSeriesCalculator`, so coach and
  trainee agree by construction (only the scope differs).
- Weeks are bucketed in the **client's** zone; default window 12 weeks.

- Roster `Status` uses **cheap signals only** (adherence band + last-active gap). "Stalled" is resolved on
  **client open** (§4.2 over tenant-scoped sessions), not per roster row (Decision **D4**).
- **No coach body-metrics endpoint** — another user's `MetricEntry` is private/self-scoped by design.

### 4.3 `GET /api/clients/{traineeId}/progress/load` — **FROZEN (Phase 4)**

The coach's gentle acute-vs-chronic **load** nudge for one open client (Decision **D14**). Same controller,
auth, and tenant-scoping as §4.2 — a **separate tenant-scoped handler** reading the **tenant-filtered**
`Query()` (EF filter **ON**, **never** `QueryOwnAcrossGyms`), so a client's cross-gym volume is invisible here
([FEASIBILITY R2](FEASIBILITY.md)). **Volume-based only** — `RpeOverall`/`DurationSeconds` are too sparse to
weight load ([R10](FEASIBILITY.md)). The two raw volumes are exposed **separately**; the endpoint **never**
computes or returns an ACWR **ratio** (an exposed ratio reads as a clinical injury claim on RPE-free data) —
the `Trend` is a **soft nudge**, never a medical/injury claim.

**Request**
```http
GET /api/clients/{traineeId:guid}/progress/load
X-Tenant-Id: <gym-id>           # required
```
No query parameters. The windows are fixed server-side: **acute = last 7 days**, **chronic = last 28 days**.

**Response `200 OK` → `AcuteChronicLoadDto`** (always; zeros + `Steady` when the client has no in-gym sessions
in the window — never `204`)

```csharp
// Modules.WorkoutSession/Application/DTOs/PersonalTrainingDtos.cs
//   (namespace Modules.WorkoutSessionModule.Application.DTOs)

public sealed record AcuteChronicLoadDto(
    decimal AcuteVolumeKg,            // Σ working-set volume over the last 7 days (this gym)
    decimal ChronicWeeklyVolumeKg,    // (Σ working-set volume over the last 28 days) ÷ 4 = avg weekly load
    LoadTrend Trend);                 // soft band; NEVER a ratio

public enum LoadTrend { Detraining, Steady, Ramping }   // "detraining" | "steady" | "ramping" on the wire
```

**Business rules (frozen)**
- **Critical ([FEASIBILITY R2](FEASIBILITY.md)):** a **separate handler** reading the **tenant-filtered**
  `Query()` (filter **ON**); the coach sees only the client's **in-gym** volume. It **never** calls
  `QueryOwnAcrossGyms` and is never parameterized by `traineeId` over the self-scoped path.
- **Authorization:** `WorkoutLogViewAll` + `ResourceAccessGuard` bind the **coach** to their own gym; the
  **trainee** must also be a member of the active tenant (`ITenantRoleResolver`) — a non-member id returns
  **`NotFound`** (HTTP 404), never a silent rescope to self. A plain member is **forbidden** (403). Missing
  `X-Tenant-Id` → 400.
- **`AcuteVolumeKg`** = Σ working-set volume over the **last 7 days** of in-gym completed sessions.
- **`ChronicWeeklyVolumeKg`** = (Σ working-set volume over the **last 28 days**) **÷ 4** = the average weekly
  load. (Both windows are anchored to `UtcNow`; sessions older than 28 days are excluded entirely.)
- **Volume parity:** per-set volume = `Σ WeightKg × Reps` over `SetType == Working` sets carrying **both**
  values — the **same** computation as `SessionMapping.ComputeVolumeKg` / `GetMyProgressHandler` (drop/AMRAP
  stages count; `ParentSetId` is **not** filtered). Volume only — **no** RPE/duration weighting ([R10](FEASIBILITY.md)).
- **`Trend` (soft band, internal comparison only — the ratio is NEVER exposed):** `Ramping` when
  `AcuteVolumeKg > ~1.5 × ChronicWeeklyVolumeKg`; `Detraining` when `< ~0.8 ×`; else `Steady`. A first week of
  work from no chronic baseline reads `Ramping` (never a divide-by-zero); a truly empty window is `Steady`.
- **Empty:** no in-gym sessions in the 28-day window → `AcuteVolumeKg: 0`, `ChronicWeeklyVolumeKg: 0`,
  `Trend: steady`.

**Authorization** `[Authorize]` + `X-Tenant-Id`; gated by `WorkoutLogViewAll` + `ResourceAccessGuard` in the
handler. Exemption `GetClientLoadQuery` (`ImperativeGuarded`) in `Tests/Authorization/TenantAuthorizationExemptions.cs`.

**Sample response**
```json
{ "acuteVolumeKg": 1000.0, "chronicWeeklyVolumeKg": 360.0, "trend": "ramping" }
```

## 5. `GET /api/me/progress/nutrition-adherence` — **FROZEN (Phase 3)**

The trainee's nutrition-plan adherence trend for the Progress Body section. Self-scoped via the Nutrition
`IDailyNutritionLogRepository.QueryOwnAcrossGyms(currentUser.UserId)`; **query-only** — it rides the existing
`DailyNutritionLog.AdherencePct` (`ComputeAdherencePct` on open days, the finalized value on closed days),
so there is **no new entity, no migration, no cache**. The calorie/macro **vs-target** card is explicitly
**out of scope** (Decision **D13**) — it needs a daily-target entity the nutrition program owns.

**Request**
```http
GET /api/me/progress/nutrition-adherence?from=&to=     # self-scoped, cross-gym; NO X-Tenant-Id
```
`from`/`to` are optional **local-day** bounds (the trainee's zone); default = trailing **4 weeks** ending
today. Nutrition is a *daily* signal, so the window is shorter than the strength endpoints' 12 weeks.

**Response `200 OK` → `NutritionAdherenceDto`** (always; empty-but-valid — never `204`, never `404`)

```csharp
// Application/DTOs/DailyNutritionLogDtos.cs  (namespace Modules.NutritionModule.Application.DTOs)

public sealed record NutritionAdherenceDto(
    bool HasPlan,                                 // false ⇒ user never had a planned nutrition day
    IReadOnlyList<DailyAdherenceDto> Days,        // PLANNED days in range, one per local date, oldest→newest
    int? CurrentWeekAvgPct,                       // mean AdherencePct over the current local week's planned days; null if none
    int LoggedDaysThisWeek,                       // D15 tracking: current local-week days with ≥1 logged item (ANY source)
    bool HasAnyLogging);                          // D15 tracking: has the caller EVER logged a nutrition day (any source)

public sealed record DailyAdherenceDto(
    DateOnly LocalDate,                           // the day's local date (already in the trainee's zone)
    int AdherencePct,                             // completed/substituted ÷ planned, 0–100
    int PlannedCount,
    int CompletedCount);                          // Completed or Substituted planned items
```

**Business rules (frozen)**
- **Adherence trend is plan-only (Decision D15 — honest):** a day contributes to `Days` / `CurrentWeekAvgPct`
  iff `Source == FromAssignment`. An ad-hoc self-logged day has no plan to adhere to (its adherence is 100% by
  convention) and is **excluded** so it never inflates the trend. Ad-hoc effort is surfaced **separately** as a
  *tracking* signal (see the two new fields below) — never folded into the %.
- **Per-day adherence** reuses the SQL count projection (`NutritionMapping.SummaryRowProjection`): a **closed**
  day reports its finalized `AdherencePct`; an **open** day a live recompute (`ComputeAdherencePct`). Counts are
  computed in SQL — neither the item rows nor the jsonb snapshot are loaded.
- **`HasPlan`** reflects whether the caller has **ever** had a planned nutrition day (any gym, any time) — a
  bounded `EXISTS`. A user simply *between* assignments in the window still reads `HasPlan: true` with an empty
  `Days`; only a never-planned user gets `HasPlan: false`, empty `Days`, null `CurrentWeekAvgPct` (the
  empty-invite shape).
- **`CurrentWeekAvgPct`** = mean `AdherencePct` over the current **Monday-anchored** local week's planned days,
  bucketed in the **trainee's** zone; `null` when no planned day falls in the current week (never `0`).
- **`Days`** sorted by `LocalDate` asc.
- **`LoggedDaysThisWeek` (D15 — ad-hoc counted, ANY source):** count of days in the current Monday-anchored
  local week (trainee's zone) that carry **≥1 actually-logged item** — an ad-hoc add or a ticked planned item
  (`Status ∈ {Completed, Substituted}`; `Planned`/`Skipped`/`Missed` placeholders do **not** count). Computed
  by a second ALL-SOURCES read (no `Source` filter) over `QueryOwnAcrossGyms`, **not** restricted to planned
  items — so a pure self-logged day registers. A touched-but-empty day is **not** a logged day.
- **`HasAnyLogging` (D15):** a bounded `EXISTS` — has the caller **ever** logged a nutrition day with a logged
  item (any source, any gym, any time). Lets a plan-less self-logger be recognized (`HasPlan: false` but
  `HasAnyLogging: true`) without a faked adherence record. Mirrors how workout sessions already count ad-hoc
  training. Still **query-only, no migration, no cache.**

**Authorization** `[Authorize]`, any authenticated role. Self-scoped via `QueryOwnAcrossGyms(currentUser.UserId)`.
Exemption `GetMyNutritionAdherenceQuery` (`ImperativeGuarded`) in `Tests/Authorization/TenantAuthorizationExemptions.cs`.

### Goal-weight — **no endpoint, no migration** (Decision **D12**)

Goal-weight is **not** a new endpoint or column. It rides the existing free-text `MetricEntry` as
`Type="goal_weight"` (the entity is designed for this — *"a new signal is a new value, never a migration"*).
The `LogMetricEntryCommandValidator` has **no type whitelist** (only `NotEmpty` + max-50), so the write path
genuinely accepts it; the frontend **reuses** the existing writes/reads:

```http
POST /api/me/nutrition/metrics   { "type": "goal_weight", "value": 75, "unit": "kg" }   # write (latest = current goal)
GET  /api/me/progress/metrics/series?type=goal_weight&from=&to=                          # read (§3, latest-per-day)
```

Verified (no backend change shipped for goal-weight): the validator accepts `goal_weight`, and the value
round-trips through `LogMetricEntryCommand` → `GetMyMetricSeriesQuery` (latest-per-day; the latest entry on a
day is the current goal). See `Tests/Commands/GoalWeightMetricRoundTripTests.cs` + the integration round-trip.

## 6. Reused as-is (no change)

```http
GET /api/me/progress    → ProgressDto             # weekly volume/sets, lifetime totals (feeds P2 volume drill-down)
GET /api/me/records     → PersonalRecordListDto   # current best per lift (feeds the §1 PR teaser + Records page)
GET /api/me/sessions    → session list            # existing; session-detail drill-down
```
`ProgressWeekDto.Sessions` is **all-status** and carries **no goal** — never used for adherence. Adherence/consistency come only from §1.
