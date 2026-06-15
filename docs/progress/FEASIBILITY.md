# Progress Page — Technical Feasibility

> **Stance:** a metric is not feasible until proven from real columns. Every verdict below is tied to actual
> fields/handlers in the current schema. Where the redesign over-promises or risks a tenant-isolation breach,
> it is flagged with the offending field. This doc *elaborates* the [Canonical spec](SPEC.md) and grounds the
> [Priority matrix](PRIORITY-MATRIX.md) — it does not contradict either.
>
> **Headline finding:** **no new EF migration, column, or entity is required for any P0/P1 training metric.**
> What is missing is **query surface, not schema**. The only real engineering is (a) a new e1RM-series query —
> trainee self-scoped **plus a separately-implemented** coach tenant-scoped variant, (b) a `MetricEntry` range
> query for bodyweight, and (c) a new coach roster projection.

**Related:** [SPEC.md](SPEC.md) · [PRIORITY-MATRIX.md](PRIORITY-MATRIX.md) · [../PERMISSIONS.md](../PERMISSIONS.md) · [../DATABASE.md](../DATABASE.md) · [../ARCHITECTURE.md](../ARCHITECTURE.md)

---

## 1. Per-metric verdict table

Three verdicts only: **Available now** (computable from existing columns, possibly via a new *read*), **Needs
endpoint** (data exists as a series but no read surface), **Not computable** (no field, or a deliberate
scope/privacy boundary). Every row below states the decision it enables — anything driving no decision is
excluded by the spec and not relisted here.

### 1.1 Trainee — P0 / P1

| Metric | Decision it enables | Verdict | Source fields / formula | Gap if not "now" |
|---|---|---|---|---|
| **P0 Weekly frequency adherence ring** | Train again before the week resets / I'm drifting | **Available now** | `COUNT(WorkoutSession WHERE Status=Completed)` per Monday week in `ClientTimezone` ÷ `PlanAssignment.FrequencyDaysPerWeek`. Per-week `Sessions` already in `ProgressWeekDto`; `WeeklyGoal` already on `SessionSummaryDto`. | None for the math. `ProgressWeekDto` lacks `WeeklyGoal`; goal lives on the assignment, not the week rollup → see [R1](#r1--adherence-denominator-is-ambiguous-for-multi-gym-trainees). |
| **P0 Per-lift e1RM direction** (sparkline, top 1–3) | Push / hold / change this lift | **Available now** (new query) | `MAX(PerformedSet.EstimatedOneRepMaxKg) WHERE SetType=Working`, per `(ExerciseId, StartedAt)`, ordered by `StartedAt`. Δ vs trailing-4-wk = arithmetic on that series. | No endpoint emits an e1RM **series** — `/api/me/records` returns only the single lifetime best per lift. New self-scoped query, **no migration**. |
| **P0 Headline state line** | The 5-second "stronger + on track?" insight | **Available now** (derived) | Pure string composition of the ring + the e1RM direction strip. | None — built client-side from the two above. |
| **P1 Consistency heatmap + forgiving %** | Re-engage after a gap / protect a routine | **Available now** | `WorkoutSession.StartedAt` (Completed), day/week-bucketed in `ClientTimezone`. Consistency % = weeks-hitting-goal ÷ weeks-observed. | None for the cells. Forgiving-% denominator inherits the [R1](#r1--adherence-denominator-is-ambiguous-for-multi-gym-trainees) goal ambiguity; ad-hoc users with no assignment show **raw frequency, not %**. |
| **P1 e1RM stall flag** | Deload / swap / change rep-scheme on this lift | **Available now** | Read off the same series: session-best `Working` e1RM has not exceeded prior best in last K (~3–4) exposures. | Same missing series query. **Not** off `PrCount` → see [R4](#r4--prcount-must-never-feed-a-progress-or-stall-signal). |
| **P1 Per-lift PR timeline / teaser** | Which lift is climbing / has gone quiet | **Available now** | `/api/me/records` → `PersonalRecordDto { ExerciseId, ExerciseName, WeightKg, Reps, EstimatedOneRepMaxKg, AchievedAt }`; already cross-gym via `QueryOwnAcrossGyms`. | **None — already returns this exact data.** Caveat: it is *current best per lift*, **not** a PR-progression history → see [R3](#r3--apimerecords-is-current-best-per-lift-not-a-pr-progression-timeline). |
| **P1 Full per-lift e1RM trend chart** | Diagnostic behind the sparkline | **Available now** (same series query) | Same source, unsuppressed, 8–12 wk, selectable lift. | Same series endpoint. `Reps>12` suppression + ≥4-point gate enforced **server-side** → see [R8](#r8--the-e1rm-honesty-gate-is-a-correctness-requirement-enforce-it-server-side). |
| **P2 Weekly volume trend** (feeds the volume card) | Workload rising/flat/spiking → add/cut sets | **Available now** | `ProgressWeekDto.TotalVolumeKg` already computed by `GetMyProgressHandler` (Σ `WeightKg×Reps`, `Working`, both non-null). | None — already shipped server-side. Label "lifting volume only" → see [R7](#r7--volume-is-barbelldumbbell-work-only--never-the-progress-headline). |
| **P2 Smoothed bodyweight trend** | Stay the course / adjust; don't panic at a spike | **Needs endpoint** | `MetricEntry WHERE Type='weight'` ordered by `LocalDate`. | **No range query exists** — only `GetOwnForDateAsync(date)` / single-date `GetMyNutritionMetricsQuery(Date)`. Add `GetMyMetricSeries(type, from, to)`. **No schema change** (already a `LocalDate` series). |

### 1.2 Coach — P0 / P1 (tenant-scoped, own gym only)

| Metric | Decision it enables | Verdict | Source fields / formula | Gap if not "now" |
|---|---|---|---|---|
| **P0 Needs-attention triage chip + sortable roster** | Who to message today | **Available now, scaling caveat** | Per member (tenant-scoped): adherence = completed ÷ `FrequencyDaysPerWeek`; "quiet" = `now − MAX(StartedAt)`; "stalled" = e1RM-stall on that client's tenant-scoped sessions. | No roster endpoint exists — today the coach pages **one** client at a time (`/api/sessions?traineeId=`, 20 rows). Needs a new tenant-scoped aggregate; the stall leg is an N×M fan-out → see [R6](#r6--roster-level-stalled-key-lift-is-an-nm-fan-out). |
| **P0 Last-active / quiet flag** | Leading churn indicator | **Available now** | `MAX(WorkoutSession.StartedAt)` per trainee, tenant-scoped. | Cheap, but still needs the roster-level read. |
| **P1 Per-client adherence ring + trend** | Re-program vs progress the plan | **Available now** | `ListSessionsHandler` already carries per-session `WeeklyGoal`/plan context; bucket completed by ISO week ÷ goal. | None new for one open client. The 20-row monitor page caps trend depth → see [R5](#r5--coach-trend-depth-is-capped-at-20-sessions). |
| **P1 Per-client e1RM trend + stall** | Which lift to deload/swap/push | **Available now** (tenant-scoped) | Same series logic over **tenant-scoped** sessions in *this gym only*. | The series endpoint **must** have a coach variant — **do NOT reuse `QueryOwnAcrossGyms`** → see [R2](#r2--reusing-the-trainee-series-query-for-the-coach-breaches-tenant-isolation). |
| **P1 Assignment status / Apply-latest** | Is the client on the current plan version | **Available now** | `PlanAssignment.{PlanVersion, IsActive}` + newer-published-version check; already drives the existing button. | None — already built. |
| **P2 Acute vs chronic load** (separate bars) | Ramping too fast / detraining | **Available now (coarse)** | Acute = 7-day Σ volume; chronic = 28-day avg, from tenant-scoped sessions. | Default to **volume-based** load; `RpeOverall` is integer-only + often null → see [R10](#r10--rpe-and-duration-density-are-too-sparse-for-load). Never the ACWR **ratio**. |
| **❌ Body data (weight/sleep) tiles** | (recovery/weight context) | **Not computable (coach)** | — | **No coach endpoint for another user's `MetricEntry`** — it is deliberately not `ITenantEntity`; reads scope to `currentUser.UserId`. **Delete the card** → see [R2](#r2--reusing-the-trainee-series-query-for-the-coach-breaches-tenant-isolation). |

### 1.3 Excluded / not-computable (do not design around)

| Item | Verdict | Why |
|---|---|---|
| Lifetime total volume / total kg | **Excluded (vanity)** | Monotonic; conflates load/sets/bodypart; drives no decision. Cut. |
| Strength-Level percentile | **Not computable** | No normative dataset. Don't fabricate. |
| Goal-weight progress bar | **Not computable** | **No goal-weight field** — the `TargetWeightKg` in code is lifting load. |
| Body fat % / circumferences | **Not computable** | No anthropometric field and no client write path. `MetricEntry.Type` is free text but has no typed vocabulary → vapor. |
| Readiness / recovery score | **Not computable** | No HRV/RHR; integer RPE. |
| Training density | **Not computable** | `DurationSeconds` is wall-clock incl. rest; `RestSeconds` never aggregated. |
| Daily calorie / macro "on track" | **Not computable today** | Macros never summed server-side; no daily target entity. Needs aggregation **and** a target field (a migration) — out of P0/P1. |

---

## 2. Minimal new fields / migrations justified for P0/P1

The audit's central result: **zero EF migrations for any P0/P1 training metric.** Everything is computable from
existing columns; the work is new *read* surface.

**No migration — reuse existing entities:**

- **e1RM series (P0/P1, trainee + coach):** reads `PerformedSet.EstimatedOneRepMaxKg` / `SetType`, joined via
  `PerformedExercise.ExerciseId` to `WorkoutSession.StartedAt`. New query + DTO only.
- **Adherence / consistency / volume / PR timeline:** already computed by `GetMyProgressHandler` /
  `GetMyPersonalRecordsHandler`, or trivially derivable from existing per-week / per-session aggregates.
- **Coach roster triage:** a new tenant-scoped projection over existing `WorkoutSession` columns.

**Needs a new query/endpoint but NO schema change — the single justified read addition near P1:**

- **Bodyweight series (P2, the only "blocked" item):** add `MetricEntryRepository.GetOwnSeriesAsync(traineeId,
  type, from, to)` + `GetMyMetricSeriesQuery`. `MetricEntries` already carries `LocalDate` + `LoggedAtUtc` and
  the index `IX_MetricEntries_TraineeId_LocalDate`. **No new column.** Justified as a one-method repo addition
  exposing data already stored as a series. It lives on the **Nutrition migration chain** with `MetricEntry`,
  not `AppDbContext` — respect the [two-chain rule](../DATABASE.md).

**Would need a migration — and is correctly *not* in P0/P1:**

- Daily nutrition macro totals / calorie targets → new aggregate projection **and** a target entity. Deferred.
- Goal-weight, body-fat %, circumferences → no field, no write path. Not computable.

**Optional, deferred — denormalized e1RM read model.** Like the existing precomputed `WorkoutSession.PrCount`,
a per-`(trainee, exerciseId, sessionDate)` best-e1RM row would avoid walking working sets per chart load. This
**is a migration** (new table + a handler on `SessionCompletedEvent`) and is deferred until §3's on-read query
is *measured* too slow. Not required for launch.

---

## 3. Aggregate query design — compute-on-read vs precompute

**Default: compute on read for v1.** Precompute (a `PrCount`-style read model) only where measured slow. The
one genuinely heavy new read is the e1RM series; everything else reuses shipped aggregates.

### 3.1 e1RM-over-time per exercise (the one new heavy read)

```sql
SELECT pe.ExerciseId,
       week_bucket(ws.StartedAt)    AS bucket,   -- Monday-anchored, ClientTimezone
       MAX(ps.EstimatedOneRepMaxKg) AS sessionBestE1rm,
       MAX(ws.StartedAt)            AS at
FROM   PerformedSets ps
JOIN   PerformedExercises pe ON pe.Id = ps.PerformedExerciseId
JOIN   WorkoutSessions  ws  ON ws.Id = pe.SessionId
WHERE  ps.SetType = 2 /*Working*/ AND ps.EstimatedOneRepMaxKg IS NOT NULL
  AND  ws.TraineeId = @uid              -- TRAINEE: QueryOwnAcrossGyms → tenant filter OFF
  --   COACH:  AND ws.TenantId = @tenant → tenant filter ON  (MUST differ — see R2)
  AND  pe.ExerciseId = ANY(@topLiftIds) -- BOUND to 3–6 lifts, never all
GROUP  BY pe.ExerciseId, week_bucket(ws.StartedAt)
ORDER  BY pe.ExerciseId, at;
```

- **Bound the lift set — the single most important cost control.** Never compute the series for every lift a
  user ever touched. Resolve "top N most-trained lifts" first (one cheap `COUNT(*) GROUP BY ExerciseId`), then
  series only for those.
- **Drop-cluster correctness:** `MAX(e1RM)` over `Working` sets is inherently cluster-safe — drop/AMRAP/failure
  stages have `EstimatedOneRepMaxKg = NULL` anyway. **Do not** filter `ParentSetId` here (matches existing PR
  logic).
- **Honesty gate in the query, not the UI:** filter `TrackingType ∈ {Strength, Bodyweight}` and `Reps ≤ 12`
  **server-side** so the API never emits a misleading e1RM point ([R8](#r8--the-e1rm-honesty-gate-is-a-correctness-requirement-enforce-it-server-side)). e1RM exists only where both `Reps` and
  `WeightKg` are non-null — null-handle, don't assume density.
- **Compute on read for v1.** Precompute only if measured slow.

**Indexes.** Existing `PerformedSets(PerformedExerciseId, SetNumber)` and `PerformedSets(LoggedAt)` help only
partially. The hot path is *filter on `SetType` + `EstimatedOneRepMaxKg IS NOT NULL`, then join up to
`ExerciseId`/`StartedAt`*. Add a partial/covering index:

```sql
CREATE INDEX IX_PerformedSets_Working_E1rm
  ON PerformedSets (SetType, EstimatedOneRepMaxKg)
  WHERE EstimatedOneRepMaxKg IS NOT NULL;
```

and rely on existing `WorkoutSessions(TraineeId, StartedAt)`; the `ExerciseId` FK index on
`PerformedExercises` already exists. (Adding an index *is* a migration on the `AppDbContext` chain — it is the
only schema touch the series read might justify, and only if profiling demands it.)

### 3.2 Weekly volume per muscle group (P2 — explicitly costly)

```sql
... JOIN Exercises e        ON e.Id = pe.ExerciseId
    JOIN ExerciseMuscles em ON em.ExerciseId = e.Id AND em.IsPrimary = true
WHERE ps.SetType = 2 AND ps.WeightKg IS NOT NULL AND ps.Reps IS NOT NULL
GROUP BY em.Muscle, week_bucket(ws.StartedAt);   -- only 6 MuscleGroup values
```

- **Live join is unavoidable** — performed rows carry **no denormalized muscle/equipment/category** (only
  `ExerciseName`/`TrackingType` are snapshotted). Accept the retro-recolor caveat: a recategorized exercise
  rewrites old logs' muscle attribution. **Keep P2/drill-down**, never the home glance.
- **Set-count version** filters `ParentSetId IS NULL` (drop cluster = 1 set); **volume version must NOT**
  (stages count). Diverging here silently breaks parity with `SessionMapping.ComputeVolumeKg`.

### 3.3 Adherence (trainee + coach)

**Correction (architecture audit):** `ProgressWeekDto.Sessions` counts **all** statuses (incl. Abandoned/InProgress)
and carries no goal, so it **cannot** feed adherence. Phase 1 ships a dedicated `GET /api/me/progress/overview` query
computing **completed-only** weekly counts + the authoritative goal ([D1](IMPLEMENTATION.md)) + daily consistency
buckets server-side. Cheap on-read; shape in [API-CONTRACTS §1](API-CONTRACTS.md). Goal selection is [R1](#r1--adherence-denominator-is-ambiguous-for-multi-gym-trainees).

### 3.4 PR timeline

**No new query.** `/api/me/records` already runs the correlated "no other working set beats this" reduction —
consume it. PR *markers on the trend line* are a separate derivation off §3.1, **not** this endpoint ([R3](#r3--apimerecords-is-current-best-per-lift-not-a-pr-progression-timeline)).

### 3.5 Coach roster triage

New tenant-scoped projection: per member, `MAX(StartedAt)`, completed-this-week count, goal. The "stalled lift"
leg is the expensive part — **defer per-lift stall to the per-client open**, not the roster row. The roster row
uses only cheap signals (last-active + adherence) to avoid an N×lifts fan-out ([R6](#r6--roster-level-stalled-key-lift-is-an-nm-fan-out)).

---

## 4. Caching strategy (against the existing `Cache` abstraction)

> **Phase 1 ships with NO caching** ([D8](IMPLEMENTATION.md)). Self-scoped Progress reads are un-cached today
> (verified: `GetMyProgress` / `GetMyPersonalRecords` use no cache), and the `overview` query is bounded and cheap.
> The table below is a **deferred optimization** — introduce it only if profiling shows need, using `IDistributedCache`
> with event-driven eviction. It is not a Phase-1 requirement.

Fit the existing `Cache:Provider` abstraction (Memory or Redis; the in-process fallback boots with no Redis).
TTLs are all short — progress data tolerates minutes of staleness, and the only failure mode (a just-finished
session not yet on the chart) is benign.

| Data | Cache? | Key | TTL | Invalidation |
|---|---|---|---|---|
| e1RM series per lift (trainee) | **Yes** | `me:e1rm:{userId}:{exerciseId}` | 10–15 min | `SessionCompletedEvent` for that trainee → evict that user's e1rm keys (or all-lifts wildcard). |
| `/api/me/progress` weekly rollup | **Yes** | `me:progress:{userId}` | 10–15 min | Same `SessionCompletedEvent` eviction. |
| `/api/me/records` PR list | **Yes** | `me:records:{userId}` | 30 min | Evict on session complete (a PR can land). |
| Bodyweight series | **Short** | `me:metric:{userId}:weight` | 5 min | Evict on `LogMetricEntryCommand` for that user. Append-only → cheap. |
| Coach roster triage | **Yes, per gym** | `coach:roster:{tenantId}` | 5–10 min | Evict on any member's `SessionCompletedEvent` in that tenant. Most-read coach surface. |
| Per-client coach e1RM / adherence | **Short** | `coach:client:{tenantId}:{traineeId}` | 5 min | Evict on that trainee's session complete in that tenant. |

**Rules:**

1. **Never cache across the scope boundary.** Trainee keys are `userId`-only (cross-gym). Coach keys **must**
   include `tenantId` — a tenant-less coach key would leak cross-gym data into a single-gym view. Mirrors [R2](#r2--reusing-the-trainee-series-query-for-the-coach-breaches-tenant-isolation).
2. **Prefer event-driven eviction on `SessionCompletedEvent`** over pure TTL — that event already fires at the
   exact moment these go stale. TTL is the backstop.
3. **All TTLs ≤ 30 min.** Short windows keep the honest-data contract: a stale chart is a recent chart, never a
   wrong one.

---

## 5. Proposed API structure

> **Endpoint shapes are now frozen in [API-CONTRACTS.md](API-CONTRACTS.md) — that file is authoritative.** This
> section is the rationale. Phase 1's home is a **single `GET /api/me/progress/overview`** call, not the `/strength`
> sketch below (which is the Phase-2 fuller per-lift series).

Versioning via the **`X-Api-Version`** header (clean URLs, **no `/v1` in path**), matching `ApiVersionMiddleware`.
Empty results return **`200 OK` with an empty-but-valid DTO** — `MeController` reads do **not** use `204` (that is a
different controller's convention; corrected from an earlier draft). All new reads return `Result<T>`; controllers map to HTTP.

### 5.1 Trainee — self-scoped (`/api/me/*`, no `X-Tenant-Id`, `QueryOwnAcrossGyms`)

```http
GET /api/me/progress/strength?lifts=top&take=6&from=&to=
→ 200 StrengthProgressDto {
    Lifts: [ LiftTrendDto {
      ExerciseId, ExerciseName,            // durable snapshot name
      TrackingType,                        // gated server-side to Strength|Bodyweight
      Points: [ { WeekStart, SessionBestE1rmKg, TopSetWeightKg, TopSetReps } ],
      DeltaKgVsTrailing4w, Direction,      // Up | Flat | Down
      Stalled, StallSessions
    } ] }
→ 200  StrengthProgressDto { Lifts: [] }  if no e1RM-bearing working sets exist
```

```http
GET /api/me/progress/metrics/series?type=weight&from=&to=
→ 200 MetricSeriesDto { Type, Unit, Points: [ { LocalDate, Value } ] }   // latest-per-day
→ 200  MetricSeriesDto { Points: [] }  if no entries in range
```

Requires the new repo method (§2). Match `Type` **case-insensitively / normalized** — it is unvalidated free
text ([R9](#r9--metricentrytype-is-unvalidated-free-text-match-defensively)).

**Reuse — do not rebuild:**

```http
GET /api/me/progress   → existing ProgressDto      (weekly volume/sets/sessions + lifetime)
GET /api/me/records    → existing PersonalRecordListDto  (PR timeline — already per-lift)
GET /api/me/sessions   → existing SessionListDto    (feeds heatmap via StartedAt)
```

The adherence ring/heatmap can be derived client-side from `ProgressDto.Weeks` + the assignment's
`FrequencyDaysPerWeek`, **or** — preferred — add `WeeklyGoal` to `ProgressWeekDto` server-side so the goal
denominator is authoritative, not re-derived ([R1](#r1--adherence-denominator-is-ambiguous-for-multi-gym-trainees)).

### 5.2 Coach — tenant-scoped (`X-Tenant-Id` required, `WorkoutLogViewAll`, own gym only)

```http
GET /api/clients/progress/roster
→ 200 RosterDto { Items: [ ClientStatusDto {
      TraineeId, DisplayName,
      LastActiveAt,                        // MAX(StartedAt), tenant-scoped
      CompletedThisWeek, WeeklyGoal, AdherencePct,
      Status                               // OnTrack | Drifting | Quiet | Stalled
    } ] }                                  // 'Stalled' may be omitted at roster level (R6)
→ 200  RosterDto { Items: [] }  if the gym has no members with sessions
```

The "this gym only" caption is a **UI obligation, not a field** — cross-gym training is invisible by design.

```http
GET /api/clients/{traineeId}/progress/strength?lifts=top&take=6
→ 200 LiftTrendDto[]   // SAME shape, built from TENANT-SCOPED sessions (own gym)
→ 403/404 if traineeId is not a member of the active tenant
          — NEVER silently rescope to self
```

- **Do NOT add a coach body-metrics endpoint.** No `MetricEntry` read for another user; the trainee
  `/api/me/...` series is the only sanctioned read of that data.
- The coach roster is a **new** surface (today the coach pages one client at a time). Gate it on
  `WorkoutLogViewAll` and bind to the active tenant via `ResourceAccessGuard`, exactly like `ListSessionsHandler`.
- Give coach progress endpoints their **own** date-windowed query — do **not** piggyback the 20-row monitor
  page ([R5](#r5--coach-trend-depth-is-capped-at-20-sessions)).

---

## 6. Risks & corrections

These are the audit's binding corrections. They are constraints, not preferences — implementing around them
re-introduces a vanity number, a wrong chart, or a cross-gym leak.

### R1 — Adherence denominator is ambiguous for multi-gym trainees
`FrequencyDaysPerWeek` lives on `PlanAssignment`, and a trainee can hold **multiple active assignments across
gyms**; `/api/me/progress` aggregates sessions across **all** gyms via `QueryOwnAcrossGyms`, but there is **no
single cross-gym goal**. **Correction:** pick one authoritative goal deterministically (active assignment with
the most completed sessions this week, tie-broken by latest `StartDate`), surface it in
`ProgressWeekDto.WeeklyGoal`, and for ad-hoc users with **no** active `PlanAssignment` **hide the ring** and
show raw frequency only. Never sum one gym's sessions against another gym's goal.

### R2 — Reusing the trainee series query for the coach breaches tenant isolation
**The single most dangerous item.** The trainee path uses `QueryOwnAcrossGyms(UserId)`, which **deliberately
turns the EF tenant filter OFF**. A coach series endpoint that reuses that path keyed by `traineeId` would
return that client's sessions from **every gym they belong to** — a cross-gym leak that violates the documented
"coach sees own gym only" boundary ([../PERMISSIONS.md](../PERMISSIONS.md)). **Correction:** the coach series
**must** be a separate handler with the tenant filter **ON** (`ws.TenantId = activeTenant`), gated by
`WorkoutLogViewAll` + `ResourceAccessGuard`. Never parameterize the `QueryOwnAcrossGyms` path by someone else's id.

### R3 — `/api/me/records` is current-best-per-lift, not a PR-progression timeline
`GetMyPersonalRecordsHandler` returns the **single current best** per lift, not the history of successive PRs. A
true PR-progression timeline (each time the e1RM record advanced) must be **derived from the e1RM series**
(§3.1: points where session-best strictly exceeds the running max). **Correction:** the PR *teaser* and
*records list* come from `/api/me/records`; the PR *markers on the trend line* come from the new e1RM series.
Don't imply one endpoint gives both.

### R4 — `PrCount` must never feed a progress or stall signal
`WorkoutSession.PrCount` is **0 on Abandoned sessions**, is **per-session not per-lift**, and the current vanity
UI sums it. **Correction:** `PrCount` is acceptable **only** as an in-session celebration count (its original
purpose) — never as a progress, stall, or "did they PR" input on either the trainee or coach side. All
progress/stall reads come from the e1RM series / `/api/me/records`.

### R5 — Coach trend depth is capped at 20 sessions
`ListSessionsHandler` feeds the coach panel a single page of **20 sessions**. A 4–6×/week client passes 20
sessions in ~3–5 weeks, so an "8-week compliance trend" and "acute vs chronic (28-day)" silently truncate.
**Correction:** coach progress endpoints get their own `from`/`to`-windowed, server-side weekly-rollup query —
not a client-side grouping of the 20-row monitor page.

### R6 — Roster-level "stalled key lift" is an N×M fan-out
A stall flag per roster row means an e1RM series per lift per client at list time. **Correction:** the roster
chip uses only **cheap signals** (adherence-below-band + last-active gap). Promote "Stalled" onto the chip only
after opening the client, or precompute a per-trainee `lastE1rmAdvanceAt` read model (a migration — defer).
Don't ship a roster that walks every working set of every member on each load.

### R7 — Volume is barbell/dumbbell work only — never the progress headline
Volume = `Σ WeightKg×Reps` over `Working` sets only; **bodyweight, cardio, timed, HIIT, mobility all contribute
0**. A calisthenics/running week shows a flat/empty volume bar that *looks like regression*. **Correction:** the
"are you progressing" state line is **e1RM-driven, never volume-driven**, and every volume chart carries the
"barbell/dumbbell work only" caption.

### R8 — The e1RM honesty gate is a correctness requirement, enforce it server-side
`EstimatedOneRepMaxKg` uses fixed Epley, which inflates badly at high reps and is **null for non-strength
tracking types**. **Correction:** enforce `TrackingType ∈ {Strength, Bodyweight}` and `Reps ≤ 12` as **filters
in the series query**, not UI hides — the API must never emit a misleading e1RM point. Honor the ≥4-point gate
before drawing a line.

### R9 — `MetricEntry.Type` is unvalidated free text — match defensively
`MetricEntry.Type` is free text ("weight" vs "Weight" vs "bodyweight"), so the series query must match
case-insensitively / normalized or risk fragmented grouping. The data is a real `LocalDate`/`LoggedAtUtc` series
— show an **empty-state invite** until the range endpoint ships; never render Section 5 as a chart of sparse or
faked points.

### R10 — RPE and duration density are too sparse for load
`RpeOverall`/`Rpe` are **integer-only and frequently null**; `DurationSeconds` is **wall-clock incl. rest** and
`RestSeconds` is never aggregated. **Correction:** coach acute-vs-chronic load defaults to **volume-based** load,
not `DurationSeconds×RpeOverall`; offer the RPE-weighted variant only where `RpeOverall` coverage is high. sRPE
and density stay drill-down / Not-Computable.

---

## 7. Resolved decisions

Both items previously open here are resolved in the central register — [IMPLEMENTATION.md §2](IMPLEMENTATION.md):
the cross-gym goal tie-break (**D1** — most completed sessions this week, then latest `StartDate`) and the roster
"Stalled" surfacing (**D4** — three cheap states OnTrack/Drifting/Quiet, "Stalled" only on client-open; the
`lastE1rmAdvanceAt` precompute is deferred to Phase 4). No open items remain.
