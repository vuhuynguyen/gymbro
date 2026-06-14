# GymBro Progress Page — Canonical Redesign Specification

> **Status:** Source of truth for downstream elaboration. Every metric is grounded in real GymBro fields; anything not computable from current data is flagged explicitly. Scope boundaries (cross-gym self-scoped trainee vs. own-gym tenant-scoped coach; no coach body-metric endpoint) are architectural constraints, not preferences — do not design around them.

---

## A. PRODUCT THESIS

**Answer questions, don't display statistics.** Every element on the Progress page must complete the sentence *"because of this, I will ___."* The current page reports **activity totals** — monotonic lifetime counts ("9 Sessions / 0.7k Total kg / 7 PRs") that only ever go up, carry no reference point, and can glow green while a user is quietly regressing. The redesign replaces every cumulative vanity number with **directional change against a self-referenced baseline**: this week vs. your goal, this lift vs. past-you, this week vs. your trailing average. GymBro is **descriptive by design** — it has no progression/deload/periodization engine — so the page visualizes honest trends and leaves prescription to the human coach. It never fabricates a target, recovery score, or comparison the backend cannot compute, because a confident-looking number the data doesn't back destroys the one thing a Progress page must have: trust.

**The 3 questions a TRAINEE's Progress must answer:**
1. **Am I on track this week?** (adherence vs. weekly frequency goal)
2. **Am I getting stronger — and on which lift?** (per-lift e1RM trend + stall localization)
3. **Is my routine holding?** (consistency pattern over weeks)
- *(secondary)* Where am I breaking through / where have I gone quiet? (PR timeline)
- *(secondary)* Is my body trending the direction my goal needs? (bodyweight trend — future)

**The 3 questions a COACH's view must answer:**
1. **Which client do I message today?** (needs-attention triage across the roster)
2. **Is this client running the plan I gave them?** (per-client adherence vs. goal)
3. **Which lift has this client stalled on, so I know what to change?** (per-client e1RM stall flag)
- *(secondary)* Is their load ramping too fast or fading? (acute vs. chronic, shown separately)
- *(secondary)* Are they on the current plan version? (assignment status / Apply-latest)

---

## B. CANONICAL METRICS CATALOGUE

Field references use the real schema: `EstimatedOneRepMaxKg`, `WeightKg`, `Reps`, `SetType` (`Working` only feeds volume/e1RM/PR), `ParentSetId` (drop-cluster = 1 set for *counts*, all stages count for *volume*), `PerformedExercise.TrackingType` / `.ExerciseName` (durable snapshots), `WorkoutSession.{StartedAt, CompletedAt, Status, RpeOverall, DurationSeconds, PrCount}`, `PlanAssignment.FrequencyDaysPerWeek` (= `WeeklyGoal`), `ClientTimezone` (Monday-anchored weeks), `MetricEntry` (`Type="weight"|"sleep"`), `ExerciseMuscles` (6 coarse groups + `IsPrimary`). Trainee reads use the cross-gym self-scoped `/api/me/*` path (`QueryOwnAcrossGyms`); coach reads use tenant-scoped `/api/sessions?traineeId=`.

### B1. Training Adherence

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Weekly frequency adherence** | `completed sessions this week ÷ FrequencyDaysPerWeek`; week = Monday-anchored in `ClientTimezone` | Train again before the week resets / I'm drifting off plan | Progress ring ("3/4") | **Available now** | Needs an active `FromAssignment`. Multi-gym: use the active assignment in the most-trained gym; never run one gym's sessions vs another's goal. **Forgiving** — prescribed rest is not a miss; no red, no streak-break. |
| **Consistency pattern** | Day/week cells from completed `StartedAt`; intensity = session count (or `TotalSets`) | Re-engage after a visible gap / protect a building routine | GitHub-style calendar heatmap, 8–12 wk | **Available now** | Works for ad-hoc trainees with no plan. Show **consistency %**, not unbroken streak; legend the color; never imply magnitude. |
| **Trailing adherence %** | `% of last 8 weeks that hit the frequency goal` | Am I a consistent person now, or fading? | Sparkline / bullet strip | **Available now** | Reward 5/7 warmly; no confirmshaming copy. |

### B2. Strength Progress

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Per-lift e1RM trend** *(THE headline)* | `MAX(EstimatedOneRepMaxKg)` over `Working` sets per session, ordered by `StartedAt`, grouped by `ExerciseId` | Push / hold / change this lift | Line chart (faint raw points + bold line), per selectable lift, 8–12 wk; sparkline on home | **Available now** (series read is the one genuinely expensive new query — bound to top 3–6 lifts) | **Suppress/grey e1RM for `Reps > 12`** (Epley breaks down). **No trend line below ~4 data points.** Only `TrackingType ∈ {Strength, Bodyweight}`. Evaluate drop-cluster **lead** set. |
| **e1RM stall / plateau flag** | "Best `Working`-set e1RM for lift X has not exceeded prior best in last K (~3–4) exposures" | Deload / swap / change rep-scheme on this lift | Quiet badge on lift row + annotation on its trend line | **Available now** | Pure read off the e1RM series. Frame as *"Bench flat 4 sessions"* — NOT a fatigue/overtraining verdict (RPE is integer-only; no HRV/sleep). |
| **Best-set / rep-anchored progression** | Top `Working` set by `WeightKg×Reps` per session; rep-anchored = `GROUP BY Reps, MAX(WeightKg)` | Add weight next session (double-progression) | "Last time" inline reference + secondary line on lift detail | **Available now** | Reuse `SessionHistoryLookup.TopWorkingSetPerExerciseAsync`. Evaluate cluster lead set. |
| **Per-lift PR timeline** | Per lift, best e1RM `Working` set across gyms, ties by `LoggedAt` → `{ExerciseName, WeightKg, Reps, e1RM, AchievedAt}` | Which lift is climbing / has gone quiet (+ motivation) | Annotated milestone timeline; PR markers pinned on the e1RM line | **Available now** — `/api/me/records` *already returns this exact data* | Current page discards it for a count. **Read `/api/me/records`, never sum `PrCount`** (abandoned sessions keep 0). Label "estimated 1RM"; don't celebrate the very first session's trivial PRs. |
| ~~Lifetime total volume / total kg~~ | `Σ weight×reps` lifetime | **none** | — | **Excluded (vanity)** | Monotonic; conflates heavier/more-sets/different-bodypart; "0.7k" is jargon. **Cut.** |
| **Strength-Level percentile** | vs. normative population | external benchmark | — | **Not computable** | No normative dataset; don't fabricate. |

### B3. Body Progress

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Smoothed bodyweight trend** | EMA/LOESS over `MetricEntry` where `Type="weight"`, by `LocalDate` | Stay the course / adjust; don't panic at a daily spike | Trend line: faint raw weigh-ins + bold smoothed line, **labeled** non-zero axis | **Needs new endpoint** | `MetricEntry` is a real series but **only single-date read exists** (`GetMyNutritionMetricsQuery(Date)`). Requires `GetMyMetricSeries(type, from, to)`. Sparse (often null); show "your weigh-ins," not a daily expectation. |
| **Sleep trend** | EMA over `MetricEntry` `Type="sleep"` | context only | Trend line | **Needs new endpoint** + low value | Same range-query blocker; only weight/sleep are written by any client. Low priority. |
| **Relative strength (lift ÷ bodyweight)** | e1RM ÷ session `BodyweightKg` | normalize strength to weight | line | **Future** | `BodyweightKg` is per-session and frequently null — too sparse to chart honestly. |
| **Body fat % / circumferences** | — | — | — | **Not computable** | No anthropometric model/field anywhere. Don't fake. |
| **Goal-weight progress bar** | actual vs. target weight | "on track to goal?" | — | **Not computable** | **No goal-weight field exists** (the `TargetWeightKg` in code is lifting load). Fabricating a target erodes trust. |

### B4. Muscle Balance

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Weekly hard sets per muscle group** | Join `PerformedExercise.ExerciseId → Exercises → ExerciseMuscles`; count `Working`, parentless sets per `MuscleGroup` per Monday week; count to **primary** muscle | Where to add/cut sets; spot a lagging area | **Sorted horizontal bar** (length reads accurately) | **Available now but costly** (live join; no denormalized muscle on performed rows) | Only **6 coarse groups** (no biceps/triceps split). **Do NOT draw MEV/MAV/MRV bands** (individualized, finer than 6 buckets). Recategorized exercises retro-color old logs. Start primary-only; no fractional weighting. |
| **Volume composition by group** | weekly `Σ weight×reps` stacked by primary muscle | is the mix balanced + total rising | Stacked bar (zero-baseline), ≤6 segments | **Available now but costly** | Same join + coarseness caveats. Ship as labeled v2 of the volume chart. |
| ~~Radar/spider muscle map~~ | — | — | — | **Excluded (viz)** | 6 axes, area distortion, arbitrary axis order — sorted bar beats it. |

### B5. Performance / Recovery

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Weekly volume trend (vs. baseline)** | `ProgressWeekDto.TotalVolumeKg` (Σ `Working` weight×reps; stages count, parentless for set count) + trailing-4-wk mean | Workload rising/flat/spiking → add/cut sets | Bar chart, 6–8 wk, **zero-baseline**, with trailing-avg reference line | **Available now** — `/api/me/progress` already computes per-week volume | **Secondary to e1RM**, not the strength headline. Label "lifting volume" (cardio/bodyweight = 0). Never a 2-bar "trend." |
| **Session effort score (sRPE)** | `f(RpeOverall or mean set Rpe, Working volume)` per session | Was today genuinely hard / is RPE creeping at same load? | Per-session value + sparkline | **Available now** (coarse) | **RPE is integer-only** (7→7.5→8 invisible) & often null; `DurationSeconds` is wall-clock incl. rest so density isn't trustworthy. Keep as drill-down diagnostic, not a headline. |
| **Acute vs. chronic load** *(coach)* | 7-day load sum vs. 28-day avg; load = volume or `DurationSeconds×RpeOverall` | ramping too fast / detraining | Two **separate** small bars + soft "ramping fast" label | **Available now** | Show acute & chronic **separately, NOT as ACWR ratio** (literature rejects the ratio as injury predictor). Soft nudge, never a medical claim. |
| **Readiness / recovery score** | — | "train hard today?" | — | **Not computable** | No HRV/RHR/sleep-contribution; integer RPE. Don't manufacture an ANS score. |
| **Training density** | active-work ÷ rest | efficiency | — | **Not computable** | `DurationSeconds` is wall-clock; `RestSeconds` never aggregated. |

### B6. Nutrition (Future)

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Plan adherence % (item count)** | `round(100 × adherentCount ÷ plannedCount)` (`AdherencePct`, Completed/Substituted ÷ planned) | am I following my meal plan | Ring / bullet | **Available now (nutrition module)** | The **only** real "vs target" in nutrition; count-based, not macro-based. Out of scope for the workout Progress page v1. |
| **Calories / protein vs. target** | per-item `LoggedItem` macros | "eat more protein today" | progress bar | **Not computable today** | Macros are **never summed server-side**; **no daily calorie/macro target** entity exists. Both sides need new aggregation + a target field. |
| **Adaptive (measured) TDEE** | trailing intake − Δsmoothed-weight×energy-density | self-correcting calorie target | number + trend | **Future** | Needs daily macro rollup + bodyweight range endpoint + logging-completeness guard. |

### B7. Coach Insights

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **Needs-attention flag** | per client: below-band adherence **OR** no session in N days (`max StartedAt`) **OR** stalled key lift | Who to message today | Status chip (On track / Drifting / Quiet / Stalled), sortable roster | **Available now** (tenant-scoped) | 75–90% adherence band default. **Own-gym only** — caption "this gym only"; cross-gym work is invisible by design. |
| **Per-client adherence + trend** | completed ÷ `FrequencyDaysPerWeek`, per ISO week | re-program / message / progress the plan | Ring + 6–8 wk compliance trendline | **Available now** | Frequency ring already exists in `ClientMonitorScreen` — elevate from header stat to hero. |
| **Per-client e1RM trend + stall** | §B2 logic over tenant-scoped sessions | which lift to intervene on | Sparkline per lift + stall badge | **Available now** | Compute lazily on opening a client (not across whole roster). |
| **Per-client PR detail** | session-detail per-lift PR (not bare `PrCount`) | "client hit a 5kg deadlift PR Tuesday" | timeline | **Available now** | Stop showing "3 PRs"; show the lift. |
| **Body data (weight/sleep)** | — | recovery/weight context | — | **Not computable (coach)** | **No coach endpoint for another user's `MetricEntry`** — private/self-scoped by design. **Delete the `—` card**; don't render an apologetic placeholder. |
| **Nutrition adherence column** | `AdherencePct` per client | nutrition compliance | column | **Not built** | Coach nutrition-adherence endpoint is roadmap-only. Design row so nutrition is an *additive* column later. |

### B8. Achievements / Motivation

| Metric | Definition / Formula (real fields) | Decision it enables | Best visualization | Feasibility | Notes / caveats |
|---|---|---|---|---|---|
| **PR celebration (in-session + reel)** | `PrCount` finalized at `Complete()`; per-set flags `SessionMapping.DetectPrs`; reel from `/api/me/records` | reinforce effort (closed feedback loop) | banner + milestone reel | **Available now** | Celebrate **genuine e1RM PRs only** (avoid extrinsic-reward crowding). e1RM PRs only — no rep/volume PRs. |
| **Forgiving consistency streak** | consecutive weeks hitting frequency goal | habit reinforcement | counter paired with heatmap | **Available now** | **Plan-aware**: prescribed rest doesn't break it. Grace/earn-back; **no confirmshaming**; never monetize streak anxiety. |
| **Coach/gym acknowledgement (kudos)** | membership graph + completed session | relatedness (SDT) | lightweight ack | **Partial / Future** | GymBro's real coach↔trainee graph is the safe relatedness lever. **Collaborative, opt-in — never cross-user strength leaderboards** (documented harm: session-deletion, image management). |

---

## C. PRIORITY MATRIX

**Priorities:** P0 = must show every day (home glance) · P1 = very valuable (home or top drill-down) · P2 = good detail screen · P3 = historical/contextual only.

### C1. TRAINEE

| Priority | Metric | Why |
|---|---|---|
| **P0** | Weekly frequency adherence ring | The single most actionable signal; answers "am I on track?" in 5s. |
| **P0** | Per-lift e1RM direction (sparkline, top 1–3 lifts) | Answers "am I getting stronger?" — the core progress question. |
| **P0** | Headline state line ("Bench & squat up · 3/4 this week") | The 5-second insight; one sentence before any card. |
| **P1** | Consistency heatmap + forgiving % | The habit story; surfaces a quietly collapsing routine before a quit. |
| **P1** | e1RM stall flag | The one prescriptive nudge the data honestly supports. |
| **P1** | Per-lift PR timeline | Backend already returns it; motivation + "which lift to push." |
| **P1** | Full per-lift e1RM trend chart (selectable) | The diagnostic behind the sparkline; per-lift by nature → one tap down. |
| **P2** | Weekly volume trend (vs. 4-wk avg) | Legitimate workload signal, but secondary to e1RM; needs ≥4 wks. |
| **P2** | Sets per muscle group | Real balance decision, but costly live join + coarse 6 groups → drill-down. |
| **P2** | Session effort (sRPE) diagnostic | Useful but coarse (integer RPE); per-exercise drill-down only. |
| **P2** | Smoothed bodyweight trend | High value, low effort — but blocked on a new range endpoint; show empty-state invite until then. |
| **P3** | Best-set / rep-anchored history | Contextual on the lift detail; inline "last time" reference. |
| **P3** | Sleep trend | Rarely logged; contextual only. |

### C2. COACH

| Priority | Metric | Why |
|---|---|---|
| **P0** | Needs-attention triage chip + sortable roster | The entire reason a busy coach opens the view — who to message today. |
| **P0** | Last-active / "quiet" flag | Leading churn indicator; one glance per client. |
| **P1** | Per-client adherence ring + trend | "Is the plan being run as written?" — re-program vs. progress. |
| **P1** | Per-client e1RM trend + stall badge | The programming decision: which lift to deload/swap/push. |
| **P1** | Assignment status / Apply-latest | Already built and genuinely actionable; keep. |
| **P2** | Acute vs. chronic load (separate bars) | Soft "ramping fast / fading" nudge; not a verdict. |
| **P2** | Per-client volume trend (vs. baseline) | Supporting context, zero-baseline, ≥4 wks. |
| **P2** | Per-client PR detail | Show the lift, not a count. |
| **P3** | Nutrition adherence column | Additive once the coach nutrition endpoint ships. |
| ❌ | Body data (weight/sleep) tiles | **Delete** — no coach endpoint; placeholder reads as broken. |

---

## D. MOBILE DASHBOARD IA — Trainee Progress Home

Ordered sections, ≤7 visual elements total on the glance layer (7±2 working-memory budget). Progressive disclosure: the glance layer **must stand alone** without a single tap.

### Section 1 — Today's / This-week status *(the 5-second insight)*
**5-second insight:** *"Am I getting stronger, and am I on track this week?"*
- **[P0] Headline state line** — one sentence with color: *"Bench & squat trending up · 3 of 4 this week."* Green/neutral, never red.
- **[P0] Weekly adherence ring** — `done/goal` (e.g. "3/4"), days-remaining as a quiet caption. Forgiving: prescribed rest excluded; a 5/7 week is warm green.
- **Empty/new-user:** No active assignment → hide the ring, show *"Get a plan assigned to track your weekly goal."* Never render 0/0.

### Section 2 — Strength progress
**5-second insight:** *"Which of my lifts are climbing, flat, or slipping?"*
- **[P0] Strength direction strip** — top 1–3 most-trained lifts, each a **sparkline + Δ vs. trailing 4 weeks** ("Bench e1RM +4.2 kg / 8 wks") with an up/flat/down indicator. `Reps>12` sets excluded; lifts with <4 data points show "log N more to see your trend."
- **[P1] Stall flag** — if any tracked lift is flat ≥K sessions, a quiet inline badge ("Squat flat 4×"). Appears only when actionable.
- **Empty/new-user:** *"Log a lift 3–4 times to see your strength trend."* Show logged points, never a line.

### Section 3 — This week's training (consistency)
**5-second insight:** *"Is my routine holding?"*
- **[P1] Consistency heatmap** — compact 8–12 week grid, intensity = sessions/week, with a forgiving **consistency %** ("hit your goal 9 of the last 12 weeks") as the secondary number — not a fragile streak.
- **Empty/new-user:** An empty grid is honest and fine; it fills in. No fake data.

### Section 4 — Achievements
**5-second insight:** *"Did I break through recently?"*
- **[P1] Latest PR teaser** — one celebratory line if any PR this week: *"Bench press — 82.5 kg × 5, new best."* Real lift + weight×reps + relative date (from `/api/me/records`). Tappable to full timeline.
- **Empty/new-user:** *"Your first PR shows up here after a couple of sessions."* Don't celebrate the trivial first-session PRs.

### Section 5 — Body progress *(conditional)*
**5-second insight:** *"Is my weight trending the way I want?"*
- **[P2] Bodyweight trend** — smoothed line, *only once the range endpoint exists.* Until then, an **empty-state invitation** to log via the daily check-in — never a fake chart.

### Section 6 — Coach feedback / acknowledgement *(optional, opt-in)*
- **[Future] Coach/gym acknowledgement** of completed sessions — collaborative, opt-in. No leaderboards.

---

## E. DRILL-DOWN PAGES

| Screen | Contents | Primary visualization |
|---|---|---|
| **Per-lift detail** (tap a lift) | Full e1RM trend (8–12 wk, selectable lift) with **PR markers pinned on the line**; best-set / rep-anchored history; stall callout; "last time" reference | Line chart (faint raw points + bold line + annotations) |
| **PR / Records page** | Lifetime per-lift records: lift name, weight×reps, e1RM, date; sorted by recency or e1RM | Annotated milestone timeline / records list |
| **Consistency detail** | Full-year calendar heatmap; consistency % per period; per-week hit/miss | Calendar heatmap (full year) |
| **Volume detail** | 6–8 wk weekly volume bars (zero-baseline) + trailing-4-wk reference line; **[v2]** stacked by muscle group | Bar / stacked bar |
| **Muscle balance** | Weekly hard sets per muscle group (primary), current + trailing 4 wk | Sorted horizontal bar (coarse-taxonomy disclaimer) |
| **Effort diagnostic** | Per-exercise RPE-vs-load over a mesocycle; session sRPE history | Sparkline / scatter (labeled "coarse — integer RPE") |
| **Bodyweight detail** *(post-endpoint)* | Smoothed weight trend, raw weigh-ins | Trend line + EMA overlay |

---

## F. COACH VS TRAINEE

| Dimension | TRAINEE | COACH |
|---|---|---|
| **Data path** | Self-scoped `/api/me/*` (`MeController`); `QueryOwnAcrossGyms(UserId)`; **no `X-Tenant-Id`**; aggregates **across all gyms** | Tenant-scoped `/api/sessions?traineeId=` (`SessionController` → `ListSessionsHandler`, gated by `WorkoutLogViewAll`, bounded to coach's **own gym**); `X-Tenant-Id` **required** |
| **Scope of truth** | Unified personal history across every gym | One gym only — cross-gym training is invisible (label "this gym only") |
| **Body metrics** | Reads own `weight`/`sleep` via self-scoped `/api/me/nutrition/metrics` | **No endpoint exists** — body data is private/self-scoped by design |
| **Core job** | Self-reflection: am I stronger / on track | Triage / management-by-exception: who needs me |
| **Weeks** | Monday-anchored in the **trainee's** `ClientTimezone` | Same — bucket in the **client's** zone, not the coach's |

**Coach multi-client roster (triage view):** A list sorted **at-risk first**, each row = client name + a status chip (On track / Drifting / Quiet / Stalled) + a compact frequency mini-ring + last-active. The chip is the needs-attention verdict (adherence below band OR no session in N days OR stalled key lift). This *is* the coach home — management by exception.

**Coach per-client detail:** Lead with the **verdict**, not a chart. Sections: (1) needs-attention chip + adherence ring + 6–8 wk compliance trend; (2) per-lift e1RM sparklines with stall badges; (3) acute vs. chronic load (separate bars, soft nudge); (4) per-lift PR detail; (5) assignment card with Apply-latest/pause/resume. **Delete the hardcoded `—` body-data card** — an apologetic placeholder reads as broken; absence is honest.

**What the coach view must NOT do:** duplicate the trainee's thin volume chart as its headline; imply cross-gym totals; show ACWR as an injury predictor; render body-metric placeholders; fabricate periodization/deload recommendations (no engine exists).

---

## G. VISUALIZATION MAP

| Metric type | Chosen viz | One-line rationale | Pitfalls to avoid |
|---|---|---|---|
| e1RM per lift (slow, noisy, directional) | **Line + light smoothing**, raw points faint | Load-normalized strength trend is the core progress story | Truncated axis OK only if **labeled**; suppress `Reps>12`; **no line under ~4 points** |
| Single goal vs. target (weekly frequency) | **Progress ring** | Glanceable "% done"; completion motivation | Vanity if trivially hit; poor for trend/comparison; must be forgiving (no red, rest-aware) |
| Consistency / streaks (binary-per-period) | **Calendar heatmap** + consistency % | Best habit-formation viz; reveals streaks & gaps | Color legend + colorblind-safe; shows frequency not magnitude; bare streak = vanity |
| Volume composition (total + breakdown) | **Stacked bar, zero-baseline, ≤6 segments** | Total + mix + week-over-week in one | Inner segments hard to compare; keep zero baseline; ≤6 groups; secondary to e1RM |
| Multi-axis muscle balance | **Sorted horizontal bar** (NOT radar) | Length reads accurately; imbalance pops | Radar distorts area, arbitrary axis order; only 6 groups |
| PRs / milestones (discrete events) | **Annotated timeline on the e1RM line** | "Progress earned over time," not a trophy count | Bare count = vanity; don't imply a trend from 1–2 PRs; label "estimated 1RM" |
| Inline trend in a list/card | **Sparkline** | Shape next to the number, tiny footprint | No scale → shape only; keep aspect ratio consistent; lightly smooth |
| Actual vs. target vs. bands (multi-KPI) | **Bullet graph** | Dense actual/target/context in a strip | Needs a *meaningful* target — don't fabricate bands GymBro lacks (no MEV/MAV/MRV, no calorie target) |
| Bodyweight / noisy slow metric | **Trend line + EMA**, faint raw + bold smoothed | Smoothing exposes signal over hydration noise | Over-reading raw spikes; over-smoothing; labeled non-zero axis only |
| Acute vs. chronic load (coach) | **Two separate bars** + soft label | Honest per the literature | **Never the ACWR ratio** as injury verdict |

### Universal guardrails (apply to every chart)
1. **Minimum-N gate:** ≥4 weeks for any trend line, ≥4 sessions-of-a-lift for an e1RM line, ≥4 weeks for adherence trend. Below the gate, show raw points and say *"not enough data yet."*
2. **Zero-based bars; labeled non-zero axis allowed only on lines** where real change is small.
3. **Adherence-neutral / anti-shame:** no red "you failed," prescribed rest counts as success, consistency-% over unbroken streaks, no confirmshaming, never monetize streak anxiety.
4. **Self-referenced over competitive:** "you vs past-you" only; no cross-user strength leaderboards.
5. **Never fabricate:** no readiness/recovery score (no wearable), no MEV/MAV/MRV bands (6 coarse groups), no goal-weight bar (no field), no daily calorie/macro "on track" (nothing summed server-side), no strength percentile (no normative data), no e1RM on high-rep or non-strength work, no training density (wall-clock duration only).