# Progress Page — Metrics Catalogue

> **Status:** Elaboration of section B of the canonical [Progress redesign spec](SPEC.md). This document is the
> exhaustive, categorized metric reference; the spec is the source of truth and this never contradicts it — it only
> expands. Every metric is grounded in real GymBro fields. Anything not computable from current data is flagged, not
> faked. Scope boundaries (cross-gym self-scoped trainee vs. own-gym tenant-scoped coach; no coach body-metric
> endpoint) are architectural constraints from [PERMISSIONS.md](../PERMISSIONS.md), not preferences.
>
> Siblings: [Priority matrix](PRIORITY-MATRIX.md) · [Feasibility audit](FEASIBILITY.md) ·
> [Visualization map](VISUALIZATION.md) · [Mobile IA](MOBILE-DASHBOARD.md). Feasibility tags below trace to the audit;
> read it before building.

---

## The one rule

**Every metric must complete the sentence "because of this, I will ___."** A metric that drives no decision is cut —
not demoted, cut. The page is descriptive by design (GymBro has no progression/deload/periodization engine), so it
visualizes honest, self-referenced trends — *you vs. past-you* — and never fabricates a target, recovery score, or
comparison the backend cannot compute. The current page's cumulative vanity totals (lifetime sessions / total kg /
PR count) are excluded everywhere: they only go up, carry no reference point, and can glow green during a regression.

## Field glossary

The catalogue references the real schema. One definition, here, so each row stays terse.

| Field | Meaning / rule that matters downstream |
|---|---|
| `PerformedSet.EstimatedOneRepMaxKg` | Epley `weight×(1+reps/30)`. **Null** for non-strength tracking and for sets missing `Reps`/`WeightKg`. Inflates badly past ~12 reps. |
| `PerformedSet.SetType` | Only `Working` feeds volume / e1RM / PR. Warmup/failure/drop stages are excluded from those. |
| `PerformedSet.ParentSetId` | Drop-cluster link. Counts as **1 set** for *set counts* (filter `ParentSetId IS NULL`); **all stages** count for *volume*. |
| `PerformedExercise.{ExerciseName, TrackingType}` | Durable per-session snapshots — survive catalog edits. e1RM is honest only for `TrackingType ∈ {Strength, Bodyweight}`. |
| `WorkoutSession.{StartedAt, CompletedAt, Status, RpeOverall, DurationSeconds, PrCount}` | `RpeOverall` integer-only & often null. `DurationSeconds` is wall-clock (incl. rest). `PrCount` is per-session, **0 on Abandoned** — never an input to progress/stall. |
| `PlanAssignment.FrequencyDaysPerWeek` | The weekly goal denominator. Lives on the assignment, not the week rollup; ambiguous across multiple gyms (see Open questions). |
| `ClientTimezone` | Weeks are Monday-anchored in the **trainee's** zone (coach buckets in the *client's* zone too). |
| `MetricEntry (Type="weight"\|"sleep")` | Real time series (`LocalDate`/`LoggedAtUtc`) but only a single-date read exists today. `Type` is unvalidated free text. |
| `ExerciseMuscles (Muscle, IsPrimary)` | 6 coarse groups, no biceps/triceps split. Not denormalized onto performed rows → muscle reads need a live join. |

**Data paths.** Trainee reads use the cross-gym self-scoped `/api/me/*` path (`QueryOwnAcrossGyms`, EF tenant filter
**off**). Coach reads use the tenant-scoped `/api/clients/*` / `/api/sessions?traineeId=` path (filter **on**, own gym
only, gated by `WorkoutLogViewAll`). These are not interchangeable — reusing the self-scoped path for a coach read
leaks a client's cross-gym sessions (audit R2).

**Feasibility tags.** `Available now` = computable from existing columns + existing or trivially-new query ·
`Needs endpoint` = existing data, no read surface (no migration) · `Future` = needs a migration or a new subsystem ·
`Not computable` = no field exists; do not fabricate · `Excluded` = computable but drives no decision (vanity) or a
worse viz than an alternative.

---

## 1. Training Adherence

*Answers "am I on track / is my routine holding?" — the most actionable signal on the page.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Weekly frequency adherence** | `COUNT(Session WHERE Status=Completed this week) ÷ FrequencyDaysPerWeek`; week = Monday-anchored in `ClientTimezone` | Train again before the week resets, or accept I'm drifting off plan | Progress ring ("3/4"), days-remaining as quiet caption | Available now |
| **Consistency pattern** | Completed-session `StartedAt` bucketed per day/week; cell intensity = session count | Re-engage after a visible gap, or protect a building routine | GitHub-style calendar heatmap, 8–12 wk | Available now |
| **Trailing adherence %** | `weeks hitting the frequency goal ÷ weeks observed`, last 8–12 wk | Am I a consistent person now, or quietly fading? | Sparkline / bullet strip; the secondary number under the heatmap | Available now |

**Notes.** Forgiving by design: prescribed rest is **not** a miss — no red, no streak-break, show **consistency %**
over a fragile unbroken streak. Needs an active `FromAssignment`; ad-hoc trainees with no plan have no goal, so show
**raw frequency**, not a %, and hide the ring (never render `0/0`). The goal denominator lives on `PlanAssignment`,
not the week rollup, and is ambiguous for multi-gym trainees — resolve it deterministically (see Open questions).

---

## 2. Strength Progress

*The core of the page. Answers "am I getting stronger — and on which lift?" The headline is per-lift e1RM trend; volume is secondary, not the strength story.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Per-lift e1RM trend** *(THE headline)* | `MAX(EstimatedOneRepMaxKg WHERE SetType=Working)` per session, ordered by `StartedAt`, grouped by `ExerciseId` | Push / hold / change *this specific lift* | Line chart (faint raw points + bold line) per selectable lift, 8–12 wk; sparkline on home | Available now (new series query — the one genuinely heavy read; bound to top 3–6 lifts) |
| **e1RM stall / plateau flag** | Session-best `Working` e1RM for lift X has not exceeded its prior best in the last K (~3–4) exposures | Deload / swap / change rep-scheme on *that* lift | Quiet badge on the lift row + annotation on its trend line | Available now (read off the e1RM series) |
| **Best-set / rep-anchored progression** | Top `Working` set by `WeightKg×Reps` per session; rep-anchored = `GROUP BY Reps, MAX(WeightKg)` | Add weight next session (double-progression) | "Last time" inline reference + secondary line on lift detail | Available now (reuse `SessionHistoryLookup.TopWorkingSetPerExerciseAsync`) |
| **Per-lift PR list** | Per lift, best e1RM `Working` set across gyms, ties by `LoggedAt` → `{ExerciseName, WeightKg, Reps, e1RM, AchievedAt}` | Which lift is climbing / has gone quiet — and motivation | Records list / milestone cards; PR markers pinned on the e1RM line | Available now — `/api/me/records` **already returns this exact data** |
| ~~Lifetime total volume / total kg~~ | `Σ weight×reps` lifetime | none | — | Excluded (vanity) — monotonic; conflates heavier vs. more-sets vs. different bodypart |
| ~~Strength-level percentile~~ | vs. normative population | external benchmark | — | Not computable — no normative dataset; do not fabricate |

**Notes.** Honesty gates are **load-bearing, enforced server-side in the series query**, not just hidden in UI:
suppress e1RM for `Reps > 12` (Epley breaks down), restrict to `TrackingType ∈ {Strength, Bodyweight}`, require both
`Reps` and `WeightKg` non-null, and draw **no trend line below ~4 data points** (show raw points + "log N more").
Evaluate the **lead** set of a drop cluster. The stall flag is a flat-trend statement (*"Bench flat 4 sessions"*),
**not** a fatigue/overtraining verdict — RPE is integer-only, there is no HRV/sleep input.

The PR *list* and PR *teaser* come from `/api/me/records` (current best per lift). The PR *progression markers* on the
trend line — each point where the record advanced — are **derived from the e1RM series**, not from `/api/me/records`,
which returns only the current best, not a history (audit R3). Read `/api/me/records`; **never** sum `PrCount`
(abandoned sessions keep 0, and it is per-session not per-lift — audit R4). Label everything "estimated 1RM"; don't
celebrate the trivial first-session PR.

---

## 3. Body Progress

*Answers "is my body trending the way my goal needs?" — high value, but the honest subset is small and one item is endpoint-blocked.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Smoothed bodyweight trend** | EMA/LOESS over `MetricEntry WHERE Type="weight"`, by `LocalDate` | Stay the course or adjust — don't panic at a daily spike | Trend line: faint raw weigh-ins + bold smoothed line, **labeled** non-zero axis | Needs endpoint — series data exists; only single-date read today. Add `GetMyMetricSeries(type, from, to)` (no migration) |
| **Sleep trend** | EMA over `MetricEntry WHERE Type="sleep"` | Context for a stalled lift / low adherence | Trend line | Needs endpoint (same range-query blocker) + low value — rarely logged |
| **Relative strength (lift ÷ bodyweight)** | session e1RM ÷ session `BodyweightKg` | Normalize strength to a changing bodyweight | Line | Future — `BodyweightKg` is per-session and frequently null; too sparse to chart honestly |
| ~~Body fat % / circumferences~~ | — | — | — | Not computable — no anthropometric field anywhere |
| ~~Goal-weight progress bar~~ | actual vs. target weight | "on track to goal weight?" | — | Not computable — **no goal-weight field** (`TargetWeightKg` in code is lifting load); a fabricated target erodes trust |

**Notes.** `MetricEntry` is sparse (often null) — show "your weigh-ins," never a daily expectation, and gate the
chart behind the new range endpoint. Until it ships, Section 5 of the home is an **empty-state invitation** to log via
the daily check-in, never a chart with faked points. `Type` is unvalidated free text, so the series query must match
defensively (case-insensitive / normalized) or grouping fragments (audit R9).

---

## 4. Muscle Balance

*Answers "where do I add or cut sets?" — a real programming decision, but costly (live join) and coarse (6 groups), so it lives in drill-down, not the home glance.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Weekly hard sets per muscle group** | Join `PerformedExercise → Exercises → ExerciseMuscles (IsPrimary)`; count `Working`, `ParentSetId IS NULL` sets per `Muscle` per Monday week | Add sets to a lagging group / cut from an overworked one | **Sorted horizontal bar** (length reads accurately) | Available now but costly (live join; no denormalized muscle on performed rows) |
| **Volume composition by group** | weekly `Σ weight×reps` (stages count) stacked by primary muscle | Is the mix balanced *and* the total rising | Stacked bar (zero-baseline), ≤6 segments | Available now but costly (same join + coarseness caveats) |
| ~~Radar / spider muscle map~~ | — | — | — | Excluded (viz) — 6 axes, area distortion, arbitrary axis order; sorted bar beats it |

**Notes.** Only **6 coarse groups** (no biceps/triceps split) — do **not** draw MEV/MAV/MRV bands; they are
individualized and finer than the taxonomy supports. Start primary-only, no fractional multi-muscle weighting.
A recategorized exercise retro-colors old logs (no muscle snapshot on performed rows). The **set-count** version
filters `ParentSetId IS NULL` (drop cluster = 1 set); the **volume** version must not (stages count) — diverging here
silently breaks parity with `SessionMapping.ComputeVolumeKg` (audit §3b).

---

## 5. Performance / Recovery

*Answers "is my workload rising, flat, or spiking?" Volume is a legitimate workload signal but is secondary to e1RM and must never drive the "are you progressing" headline. Several tempting items here are simply not computable — do not fake them.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Weekly volume trend (vs. baseline)** | `ProgressWeekDto.TotalVolumeKg` (Σ `Working` weight×reps) + trailing-4-wk mean | Workload rising / flat / spiking → add or cut sets | Bar chart, 6–8 wk, **zero-baseline**, trailing-avg reference line | Available now — `/api/me/progress` already computes per-week volume |
| **Session effort score (sRPE)** | `f(RpeOverall or mean set Rpe, Working volume)` per session | Was today genuinely hard / is RPE creeping at the same load? | Per-session value + sparkline (drill-down only) | Available now (coarse) |
| **Acute vs. chronic load** *(coach)* | 7-day load sum vs. 28-day avg; load = volume (default) | Is this client ramping too fast or detraining? | Two **separate** small bars + soft "ramping fast" label | Available now |
| ~~Readiness / recovery score~~ | — | "train hard today?" | — | Not computable — no HRV/RHR/sleep contribution; integer RPE only |
| ~~Training density~~ | active-work ÷ rest | session efficiency | — | Not computable — `DurationSeconds` is wall-clock; `RestSeconds` never aggregated |

**Notes.** Label the volume chart **"lifting volume — barbell/dumbbell work only"**: bodyweight, cardio, timed, HIIT
and mobility all contribute 0, so a calisthenics or running week looks like regression unless captioned (audit R7).
Never render a 2-bar "trend." sRPE stays a **drill-down diagnostic**, not a headline: `RpeOverall` is integer-only
(7→7.5→8 is invisible) and often null. Acute-vs-chronic is shown as **two separate bars, never the ACWR ratio** —
the literature rejects the ratio as an injury predictor; it is a soft nudge, never a medical claim. Default load to
volume, not `DurationSeconds×RpeOverall`, because the RPE leg is too sparse (audit R10).

---

## 6. Nutrition (Future)

*Answers "am I following my nutrition plan?" The completion-based adherence % is real today; everything macro/target-based needs new server aggregation and is out of scope for the workout Progress page v1.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Plan adherence % (item count)** | `round(100 × adherentCount ÷ plannedCount)` (`AdherencePct`, Completed/Substituted ÷ planned) | Am I following my meal plan | Ring / bullet | Available now (nutrition module) — out of scope for workout Progress v1 |
| **Calories / protein vs. target** | per-item `LoggedItem` macros | "eat more protein today" | Progress bar | Not computable today — macros never summed server-side; no daily target entity |
| **Adaptive (measured) TDEE** | trailing intake − Δsmoothed-weight × energy-density | Self-correcting calorie target | Number + trend | Future — needs daily macro rollup + bodyweight range endpoint + logging-completeness guard |

**Notes.** `AdherencePct` is the **only** real "vs. target" in nutrition, and it is count-based, not macro-based.
Both calorie/macro items need new server aggregation **and** a target field that does not exist (a migration). See
the nutrition future plan in [ROADMAP.md](../ROADMAP.md); do not pull these into the workout Progress page.

---

## 7. Coach Insights

*Answers "which client do I message today, and what do I change?" Triage and management-by-exception, strictly own-gym (tenant-scoped). Cross-gym training is invisible by design — caption "this gym only."*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **Needs-attention flag** | per client: adherence below band **OR** no session in N days (`MAX(StartedAt)`) **OR** stalled key lift | Who to message today | Status chip (On track / Drifting / Quiet / Stalled), sortable roster | Available now (tenant-scoped; new roster projection) |
| **Per-client adherence + trend** | completed ÷ `FrequencyDaysPerWeek`, per ISO week | Re-program vs. progress the plan | Ring + 6–8 wk compliance trendline | Available now (needs its own windowed query — not the 20-row monitor page, audit R5) |
| **Per-client e1RM trend + stall** | section 2 logic over **tenant-scoped** sessions (own gym) | Which lift to deload / swap / push | Sparkline per lift + stall badge | Available now — **separate** tenant-scoped handler, never the self-scoped path (audit R2) |
| **Per-client PR detail** | session-detail per-lift PR (not bare `PrCount`) | "client hit a 5 kg deadlift PR Tuesday" | Timeline / records list | Available now |
| **Assignment status / Apply-latest** | `PlanAssignment.{PlanVersion, IsActive}` + newer-published-version check | Are they on the current plan version | Assignment card with Apply-latest / pause / resume | Available now (already built) |
| ~~Body data (weight/sleep)~~ | — | recovery / weight context | — | Not computable (coach) — **no endpoint for another user's `MetricEntry`**; private by design. Delete the card |
| **Nutrition adherence column** | `AdherencePct` per client | Nutrition compliance | Additive column | Not built — coach nutrition endpoint is roadmap-only |

**Notes.** Compute the per-client e1RM/stall **lazily on opening a client**, not across the whole roster — the roster
chip uses only cheap signals (adherence below band + last-active gap) to avoid an N-clients × M-lifts fan-out
(audit R6). Default adherence band 75–90%. The coach progress endpoints must not piggyback the 20-session monitor
page, or an 8-week trend and the 28-day chronic load silently truncate for active clients (audit R5). **Delete** the
body-data tile entirely — an apologetic `—` placeholder reads as broken; absence is honest.

---

## 8. Achievements / Motivation

*Answers "did I break through, and is my habit holding?" Self-referenced and forgiving — reinforce effort without manufacturing competition or shame.*

| Metric | Definition / formula (real fields) | Decision it enables | Best visualization | Feasibility |
|---|---|---|---|---|
| **PR celebration (in-session + reel)** | `PrCount` finalized at `Complete()`; per-set flags via `SessionMapping.DetectPrs`; reel from `/api/me/records` | Reinforce effort — close the feedback loop | In-session banner + milestone reel | Available now |
| **Forgiving consistency streak** | consecutive weeks hitting the frequency goal | Reinforce the habit | Counter paired with the consistency heatmap | Available now |
| **Coach / gym acknowledgement (kudos)** | membership graph + completed session | Relatedness (SDT) — feel seen by a real human | Lightweight, opt-in acknowledgement | Partial / Future |

**Notes.** Celebrate **genuine e1RM PRs only** — no rep/volume PRs, no trivial first-session PRs (avoids
extrinsic-reward crowding). Here `PrCount` is acceptable **only** as the in-session celebration count (its original
purpose), never as a progress or stall input (audit R4). The streak is **plan-aware**: prescribed rest doesn't break
it; offer grace / earn-back; **no confirmshaming**, never monetize streak anxiety. Acknowledgement is collaborative
and opt-in — GymBro's real coach↔trainee graph is the safe relatedness lever; **never** cross-user strength
leaderboards (documented harm: session-deletion gaming, body-image pressure).

---

## Open questions

These are genuine ambiguities the spec leaves under-specified; flagged here rather than resolved silently against it.

1. **Adherence denominator for multi-gym / multi-assignment trainees.** `FrequencyDaysPerWeek` lives on
   `PlanAssignment`, but the self-scoped `/api/me/progress` aggregates sessions across all gyms via
   `QueryOwnAcrossGyms`, and a trainee can hold multiple active assignments. The spec says "use the active assignment
   in the most-trained gym" — make that binding and deterministic: pick the active assignment with the most completed
   sessions this week, tie-broken by latest `StartDate`, and surface it explicitly as `ProgressWeekDto.WeeklyGoal`
   rather than re-deriving client-side. Confirm this tie-break is the product intent (audit R1).
2. **PR "timeline" wording.** The spec uses "PR timeline" for two different things — the records *list* (current best
   per lift, from `/api/me/records`) and the PR *markers on the trend line* (derived from the e1RM series). They come
   from different sources; this catalogue splits them (section 2). Confirm the home teaser pulls from
   `/api/me/records` and the on-line markers from the series, not vice-versa (audit R3).
3. **Bodyweight trend sequencing.** It is tagged P2 because it is endpoint-blocked, yet its blocker (a `MetricEntry`
   range query) is a one-method, no-migration addition of the same class that enables P0 strength reads. Worth
   confirming whether it should be pulled forward, given the low cost and high value (audit R9).
