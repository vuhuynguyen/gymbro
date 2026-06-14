# Progress Drill-Down Pages

The detail screens behind the Progress home glance. The glance layer (see [Dashboard IA](MOBILE-DASHBOARD.md)) must
stand alone without a tap — these pages are the **diagnostic** behind each glance signal: the user already knows
*what* is happening from home, and comes here to learn *why* and *what to change*. One screen, one decision.

**Related:** [Dashboard IA](MOBILE-DASHBOARD.md) · [Metrics catalogue](METRICS-CATALOGUE.md) · [Priority matrix](PRIORITY-MATRIX.md) · [Visualization map](VISUALIZATION.md) · [Coach vs trainee](COACH-VS-TRAINEE.md)

Every screen below obeys the same rules as the home glance: self-referenced (you vs past-you, never leaderboards),
anti-shame (no red "you failed," prescribed rest is success), and **never fabricated** — no readiness score, no
MEV/MAV/MRV bands, no goal-weight bar, no e1RM on high-rep or non-strength work. A page that has nothing honest to
show renders an empty-state invitation, not a faked chart.

## The glance → drill-down map

| # | Drill-down screen | Entered from (home glance) | The decision it sharpens |
|---|---|---|---|
| 1 | **Per-lift detail** | Tap a lift in the Strength direction strip (or a stall badge) | Push / hold / change *this* lift |
| 2 | **PR / Records page** | Tap the "latest PR" teaser | Which lift is climbing / has gone quiet |
| 3 | **Consistency detail** | Tap the consistency heatmap | Re-engage after a gap / protect a building routine |
| 4 | **Volume detail** | Tap the weekly-volume sparkline (or via Strength menu) | Workload rising / flat / spiking → add or cut sets |
| 5 | **Muscle balance** | Tap "muscle balance" from Volume or Strength menu | Where to add or cut sets across body parts |
| 6 | **Effort diagnostic** | Tap a session row, or the effort line on Per-lift detail | Is RPE creeping at the same load? |
| 7 | **Session detail** | Tap any session in the heatmap / history list | Read back exactly what I did that day |
| 8 | **Bodyweight detail** *(post-endpoint)* | Tap the bodyweight trend in Section 5 | Stay the course / adjust — don't panic at a daily spike |

All paths are **one tap** from the glance. Nothing actionable is buried two levels deep; second-level content
(e.g. effort from inside Per-lift detail) is supporting context only.

---

## 1. Per-lift detail

**Entered from:** tapping a lift in the home Strength direction strip, or its stall badge.

**Purpose.** The diagnostic behind the home sparkline. Home says *"Bench +4.2 kg / 8 wks, trending up"*; this page
answers *should I add weight, hold, or change the lift?* It is the single most important drill-down — strength
direction is the core progress question, and it is per-lift by nature, so it lives one tap down.

**Contents**

| Element | Source (real fields) | Decision it enables |
|---|---|---|
| Full e1RM trend, selectable lift | `MAX(EstimatedOneRepMaxKg)` over `Working` sets per session, ordered by `StartedAt`, grouped by `ExerciseId` | Push / hold / change this lift |
| PR markers pinned on the line | Points where session-best e1RM strictly exceeds the running max (derived from the series — *not* `/api/me/records`) | See exactly when the lift last advanced |
| Stall callout | "Best `Working` e1RM has not exceeded prior best in last K (~3–4) exposures" | Deload / swap / change rep-scheme |
| "Last time" reference + best-set / rep-anchored history | `SessionHistoryLookup.TopWorkingSetPerExerciseAsync`; rep-anchored = `GROUP BY Reps, MAX(WeightKg)` | Add weight next session (double-progression) |

**Primary visualization.** Line chart — faint raw e1RM points, bold lightly-smoothed line, PR annotations on the
line, optional secondary best-set line. See [Visualization map](VISUALIZATION.md).

**Honesty gates (load-bearing, enforced server-side in the series query, not just hidden in UI):**
- Suppress e1RM for `Reps > 12` — fixed Epley inflates badly at high reps.
- Only `TrackingType ∈ {Strength, Bodyweight}`; e1RM is null for cardio/timed/HIIT/mobility.
- Evaluate the **lead** set of a drop cluster (`EstimatedOneRepMaxKg` is null on drop/amrap/failure stages anyway).
- **No trend line below ~4 data points** — show the raw points and *"log N more to see your trend."*
- Frame a stall as *"Bench flat 4 sessions,"* never as a fatigue/overtraining verdict (RPE is integer-only; no HRV/sleep).

**Empty state.** Fewer than ~4 exposures → plot the logged points and *"Log this lift a few more times to see your trend."*

---

## 2. PR / Records page

**Entered from:** tapping the home "latest PR" teaser.

**Purpose.** The motivation surface and the "which lift to push" answer in one list — progress *earned over time*,
not a trophy count. Replaces the current page's discarded PR data, which it throws away for a meaningless lifetime count.

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Per-lift current records | `/api/me/records` → `{ ExerciseName, WeightKg, Reps, EstimatedOneRepMaxKg, AchievedAt }`, cross-gym via `QueryOwnAcrossGyms` | Which lift is climbing / has gone quiet |
| Sort: by recency or by e1RM | client-side over the records list | Find the freshest win, or the heaviest |
| Tap-through to that lift's Per-lift detail | screen 1 | Go from "I PR'd bench" to "is bench still climbing?" |

**Primary visualization.** Annotated milestone timeline / records list — real lift + `weight × reps` + relative date.

**Caveats.**
- `/api/me/records` returns the **current best per lift**, not the full history of successive PRs. The PR *teaser* and
  this list come from `/api/me/records`; PR *markers on a trend line* are derived from the e1RM series (screen 1). Don't
  imply one endpoint gives both.
- Read `/api/me/records` — **never sum `WorkoutSession.PrCount`** (it is per-session, not per-lift, and stays 0 on
  abandoned sessions).
- Label "estimated 1RM." Celebrate genuine e1RM PRs only (no rep/volume PRs); don't celebrate the trivial first-session PRs.

---

## 3. Consistency detail

**Entered from:** tapping the home consistency heatmap.

**Purpose.** The full habit story behind the compact home grid. Surfaces a quietly collapsing routine before it
becomes a quit, and rewards a building one — *re-engage after a gap, or protect what's working.*

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Full-year calendar heatmap | completed `WorkoutSession.StartedAt`, bucketed per day/week in `ClientTimezone`; intensity = session count | Spot gaps and runs across a whole year |
| Consistency % per period | weeks-hitting-goal ÷ weeks-observed, where goal = `FrequencyDaysPerWeek` | "Am I a consistent person now, or fading?" |
| Forgiving streak counter | consecutive weeks hitting the frequency goal; **prescribed rest doesn't break it** | Habit reinforcement without shame |
| Per-week hit/miss readout | per-week completed count vs goal | See which specific weeks slipped |

**Primary visualization.** Calendar heatmap (full year). Colorblind-safe palette with a legend; shows *frequency*,
not magnitude.

**Caveats.** Show **consistency %**, not a fragile unbroken streak. No red, no streak-break on prescribed rest, no
confirmshaming, never monetize streak anxiety. Ad-hoc trainees with **no active assignment** have no goal → show raw
frequency, not a %. An empty grid for a new user is honest and fine; it fills in — never fake data.

---

## 4. Volume detail

**Entered from:** tapping the home weekly-volume sparkline (or via the Strength menu).

**Purpose.** The legitimate workload signal — *is my lifting workload rising, flat, or spiking, so do I add or cut
sets?* Secondary to e1RM by design: this answers "how much work," not "am I stronger."

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Weekly volume bars, 6–8 wk | `ProgressWeekDto.TotalVolumeKg` (Σ `Working` `WeightKg×Reps`; stages count, parentless for set count) | Workload rising / flat / spiking → add or cut sets |
| Trailing-4-week reference line | arithmetic mean of the prior 4 weeks | Is this week above or below my own baseline? |
| **[v2]** stacked by muscle group | live join through `Exercises → ExerciseMuscles` (primary), ≤6 segments | Is the work mix balanced as the total rises? |

**Primary visualization.** Bar chart, **zero-baseline**, with a trailing-average reference line; v2 adds a stacked
bar (≤6 segments, zero-baseline).

**Caveats.**
- Volume is **`Working`-set lifting only** — bodyweight, cardio, timed, HIIT, and mobility all contribute **0**. Carry
  a rigid "barbell/dumbbell work only" caption so a calisthenics or running week doesn't *read as regression*. This is
  exactly the dishonesty the product thesis forbids.
- Needs ≥4 weeks before a trend reads. Never a 2-bar "trend."
- The headline "are you progressing" state line must be e1RM-driven, **never** volume-driven.

---

## 5. Muscle balance

**Entered from:** the Volume detail or Strength menu.

**Purpose.** The body-part balance decision — *where do I add or cut sets, and which area is lagging?* Kept as a
drill-down (not a home glance) because it requires a costly live join and the taxonomy is coarse.

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Weekly hard sets per muscle group | join `PerformedExercise.ExerciseId → Exercises → ExerciseMuscles`; count `Working`, **parentless** (`ParentSetId IS NULL`) sets per `MuscleGroup` per Monday week; **primary** muscle only | Add / cut sets; spot a lagging area |
| Current week + trailing-4-week reference | same query over the prior window | Is this week's distribution off my own norm? |

**Primary visualization.** **Sorted horizontal bar** — length reads accurately and imbalance pops. *Not* a
radar/spider chart (area distortion, arbitrary axis order, only 6 axes).

**Caveats.**
- Only **6 coarse groups** (no biceps/triceps split). **Do not draw MEV/MAV/MRV bands** — they're individualized and
  finer than 6 buckets; carry a coarse-taxonomy disclaimer.
- Set-count version filters `ParentSetId IS NULL` (drop cluster = 1 set); a volume version must **not** (stages count) —
  diverging silently breaks parity with `SessionMapping.ComputeVolumeKg`.
- Live join, no denormalized muscle on performed rows → recategorizing an exercise retro-recolors old logs. Start
  primary-only, no fractional weighting.

---

## 6. Effort diagnostic

**Entered from:** a session row, or the effort line inside Per-lift detail.

**Purpose.** A *diagnostic*, not a headline — *is RPE creeping at the same load, or was today genuinely hard?* Demoted
to a drill-down because the underlying signal is coarse.

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Per-exercise RPE-vs-load over a mesocycle | per-set `Rpe` (or `RpeOverall`) vs `WeightKg` across sessions | Is effort rising at a fixed load (fatigue creep)? |
| Session sRPE history | `f(RpeOverall or mean set Rpe, Working volume)` per session | Was today genuinely hard? |

**Primary visualization.** Sparkline / scatter, explicitly labeled **"coarse — integer RPE."**

**Caveats.** `RpeOverall`/`Rpe` are **integer-only** (7→7.5→8 is invisible) and **frequently null**; `DurationSeconds`
is wall-clock incl. rest, so density isn't trustworthy. Keep this a drill-down diagnostic, never a headline or a
recovery verdict.

---

## 7. Session detail

**Entered from:** tapping any session in the consistency heatmap or a history list.

**Purpose.** The factual read-back — *what exactly did I do that day, and how did it compare to last time?* This is
the ground truth every other chart aggregates; it carries no inferred verdict, just the logged record.

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Session header | `WorkoutSession.{StartedAt, CompletedAt, Status, DurationSeconds, RpeOverall, PrCount}` | Confirm date, status, overall effort |
| Per-exercise set list | `PerformedExercise.{ExerciseName, TrackingType}` (durable snapshots) + each `PerformedSet.{SetType, WeightKg, Reps, EstimatedOneRepMaxKg, Rpe, RestSeconds}` | Read back the actual work, mode-aware per tracking type |
| Superset / drop-cluster grouping | `SupersetGroupId` (full rotation) and `ParentSetId` (drop cluster) | Understand how sets were structured |
| In-session PR flags | per-set PR markers from `SessionMapping.DetectPrs` | See which sets were genuine e1RM PRs |
| "Last time" comparison | `SessionHistoryLookup.TopWorkingSetPerExerciseAsync` | Did I beat last session on each lift? |

**Primary visualization.** Structured set list (mode-aware per `TrackingType`), with PR markers inline. Not a chart —
this screen is the record itself.

**Caveats.** `PrCount` here is fine as its original **in-session celebration count** — never as a progress or stall
input. Render mode-aware: a timed/cardio set shows duration/distance, not `weight × reps`.

---

## 8. Bodyweight detail *(post-endpoint)*

**Entered from:** tapping the home bodyweight trend (Section 5), once the range endpoint exists.

**Purpose.** *Is my weight trending the way my goal needs — and should I stay the course or adjust without panicking
at a daily spike?* Smoothing exists to separate signal from hydration noise.

**Contents**

| Element | Source | Decision it enables |
|---|---|---|
| Smoothed weight trend | EMA/LOESS over `MetricEntry` where `Type="weight"`, ordered by `LocalDate` | Stay the course / adjust |
| Raw weigh-ins | the same series, faint | See the spread behind the smoothed line |

**Primary visualization.** Trend line — faint raw weigh-ins, bold smoothed (EMA) overlay, **labeled** non-zero axis.

**Feasibility (the one blocked screen).** `MetricEntry` is a real series but **only a single-date read exists**
(`GetMyNutritionMetricsQuery(Date)`). This screen needs a new self-scoped range query
`GetMyMetricSeries(type, from, to)` — **no schema change**, the table already has `LocalDate` + `LoggedAtUtc` and an
index. Until it ships, Section 5 on home shows an **empty-state invitation** to log via the daily check-in — never a
faked chart with sparse points.

**Caveats.** Sparse and often null — show "your weigh-ins," not a daily expectation. The `Type` string is unvalidated
free text, so the series query must match defensively (case-insensitive / normalized). Don't over-read a raw spike;
don't over-smooth.

---

## Coach drill-downs

The coach surface is its own IA (triage roster → per-client detail) and is specified in
[Coach vs trainee](COACH-VS-TRAINEE.md). Two boundaries bind every coach drill-down here:

- **Tenant-scoped, own gym only.** Coach reads use `/api/sessions?traineeId=` / `/api/clients/...` with the EF tenant
  filter **ON** — they must **never** reuse the trainee's `QueryOwnAcrossGyms` path, which turns the filter off and
  would leak a client's training from *every* gym. Caption "this gym only."
- **No coach body-metric screen.** There is no coach endpoint for another user's `MetricEntry` — body data is
  private/self-scoped by design. There is **no Bodyweight detail (screen 8) on the coach side**; delete the placeholder
  rather than render an apologetic `—` card that reads as broken.

The per-client detail leads with the **verdict** (needs-attention chip + adherence ring + compliance trend), then
reuses screens 1, 4, and 6 over **tenant-scoped** data, plus acute-vs-chronic load (two **separate** bars, never the
ACWR ratio) and per-lift PR detail (the lift, never a bare count).

---

## Open questions

1. **PR markers vs. records list — two sources, one mental model.** Per-lift detail (screen 1) derives PR markers from
   the e1RM *series*, while the PR/Records page (screen 2) reads `/api/me/records` (current best per lift only). These
   can momentarily disagree at the edges (a just-logged PR not yet reflected in a cached records read). The spec treats
   both as "the PR timeline" — confirm we present them as one consistent story and reconcile cache eviction on
   `SessionCompletedEvent`.
2. **Coach trend depth vs. the 20-session page cap.** Screens 1/4/6 on the coach side assume 6–8 week (and 28-day
   acute/chronic) windows, but the existing coach monitor pages **20 sessions** — a 4–6×/week client exceeds that in
   ~3–5 weeks. Coach drill-downs need their own date-bounded query, not the 20-row monitor page, or these windows
   silently truncate.
