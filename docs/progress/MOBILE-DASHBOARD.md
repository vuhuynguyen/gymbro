# Mobile Dashboard — Trainee Progress Home

> Owns spec section **D**: the information architecture of the trainee Progress home on the Flutter client. This
> document elaborates the canonical spec — it does not re-derive metric definitions ([Metrics catalogue](METRICS-CATALOGUE.md)),
> priorities ([Priority matrix](PRIORITY-MATRIX.md)), drill-down screens ([Drill-downs](DRILL-DOWNS.md)), or chart
> choices ([Visualization](VISUALIZATION.md)). It answers one question: **what does a trainee see, in what order, on
> the glance layer, and what does each card let them decide.**

---

## The 5-second principle

A trainee opens Progress to settle **one** thought — *"am I getting stronger, and am I on track this week?"* — and
closes it. The glance layer **must answer that without a single tap.** Everything below is built around a hard test:
if a card does not change what the user does next, it does not earn a place on the home screen. We do not display
statistics; we answer questions.

Two budgets govern the layout:

| Budget | Value | Why |
|---|---|---|
| **Working-memory** | ≤ 7 visual elements on the glance layer (7±2) | More than ~7 cards and the glance stops being a glance; the page becomes a dashboard the user scans instead of reads. |
| **Reading order** | Verdict first, evidence second, detail on tap | The headline state line resolves the 5-second question; cards below justify it; drill-downs carry the diagnostics. [Progressive disclosure](DRILL-DOWNS.md) keeps the home honest without hiding depth. |

**Self-referenced, never competitive; forgiving, never red.** Every number on this page is *you vs. past-you* — this
week vs. your goal, this lift vs. your trailing average. Prescribed rest is success, not a broken streak. We never
fabricate a target, a recovery score, or a comparison the backend cannot compute (see the
[universal guardrails](VISUALIZATION.md)). A confident number the data doesn't back destroys the one thing a Progress
page must have.

---

## Why the current home fails the test

Today's mobile Progress home leads with three lifetime totals — **`Sessions` / `Total kg` / `PR count`** — rendered
as equal-weight stat tiles.

| Current tile | What it shows | Decision it enables | Verdict |
|---|---|---|---|
| **9 Sessions** | Monotonic lifetime session count | none — only goes up, has no reference point | Cut from home |
| **0.7k Total kg** | Lifetime `Σ weight×reps` | none — conflates heavier vs. more-sets vs. a different body part; "0.7k" is jargon; bodyweight/cardio contribute 0 | Cut entirely (vanity) |
| **7 PRs** | Summed `WorkoutSession.PrCount` | none — a count, not a lift; `PrCount` is 0 on abandoned sessions and is per-session, not per-lift | Replace with the actual lift |

The failure is structural, not cosmetic: all three are **cumulative counts that can only rise**, so the page glows
green while a user is quietly regressing — the e1RM on their main lift can slide for a month and every number on screen
still goes up. The redesign replaces each with **directional change against a self-referenced baseline.** `PrCount`
in particular must never be summed for a progress signal; the real PR data lives in `/api/me/records` and names the
lift (see [Metrics catalogue §B2/§B8](METRICS-CATALOGUE.md)).

---

## Ordered sections

Six sections, top to bottom. Sections 1–4 are the glance layer and stay within the 7-element budget; Section 5 is
conditional (renders only once its endpoint exists); Section 6 is opt-in future. Each section leads with the
5-second insight it delivers, then its cards, then its empty state.

### Section 1 — This-week status *(the 5-second insight)*

**Insight:** *"Am I on track this week?"* — resolved before the user reads anything else.

```
┌─────────────────────────────────────────────┐
│  Bench & squat trending up · 3 of 4 this week│  ← [P0] headline state line (green/neutral)
│                                              │
│        ╭───────╮                             │
│        │  3/4  │   2 days left this week      │  ← [P0] weekly adherence ring + quiet caption
│        ╰───────╯                             │
└─────────────────────────────────────────────┘
```

| Card | Content | Visualization | Decision it enables |
|---|---|---|---|
| **[P0] Headline state line** | One sentence composed from the adherence ring + the strength strip: *"Bench & squat trending up · 3 of 4 this week."* Color green or neutral, **never red**. | Plain text, single line, color-weighted | The entire 5-second verdict — do I need to act this week, or am I fine? |
| **[P0] Weekly adherence ring** | `completed / goal` (e.g. "3/4") for the Monday-anchored week in the trainee's `ClientTimezone`; days-remaining as a quiet caption. Forgiving — prescribed rest excluded; a 5/7 week reads warm green. | [Progress ring](VISUALIZATION.md) | Train again before the week resets, or rest easy — am I drifting off plan? |

The state line is derived, not stored — it is a string built from Section 1 and Section 2. The ring's denominator
is the trainee's weekly frequency goal (`FrequencyDaysPerWeek` on the active assignment); see [Open questions](#open-questions)
on which goal wins for a multi-gym trainee.

**Empty / new-user:** no active assignment → **hide the ring** and show *"Get a plan assigned to track your weekly
goal."* Never render `0/0` — a zero-denominator ring reads as broken, and an ad-hoc trainee has no goal to miss.

### Section 2 — Strength progress

**Insight:** *"Which of my lifts are climbing, flat, or slipping?"* — the core progress question.

```
┌─────────────────────────────────────────────┐
│  Strength                                    │
│  Bench   ▁▂▃▄▆  ↑  +4.2 kg / 8 wks           │  ← [P0] sparkline + Δ vs trailing 4 wk
│  Squat   ▅▅▅▅▅  →  flat 4 sessions  ⚑        │  ← [P1] inline stall flag (only when actionable)
│  Deadlift▃▄▄▅▆  ↑  +6.0 kg / 8 wks           │
└─────────────────────────────────────────────┘
```

| Card | Content | Visualization | Decision it enables |
|---|---|---|---|
| **[P0] Strength direction strip** | Top 1–3 most-trained lifts. Each row = lift name + **sparkline + Δ e1RM vs. trailing 4 weeks** + an up/flat/down indicator. e1RM = `MAX(EstimatedOneRepMaxKg)` over `Working` sets per session. | [Sparkline](VISUALIZATION.md) per row | Push, hold, or change this lift. |
| **[P1] Stall flag** | A quiet inline badge on a lift row — *"Squat flat 4×"* — when that lift's best e1RM has not exceeded its prior best in the last K (~3–4) exposures. Appears **only when actionable**. | Inline badge on the row | Deload / swap / change rep-scheme on this lift — the one prescriptive nudge the data honestly supports. |

Honesty gates are **server-side**, not just UI: sets with `Reps > 12` are excluded (Epley inflates at high reps),
only `TrackingType ∈ {Strength, Bodyweight}` qualifies, and a lift with **< 4 data points draws no line** — it shows
*"log N more to see your trend."* The strip is the diagnostic doorway: tapping a lift opens its
[full e1RM trend with PR markers](DRILL-DOWNS.md). The stall badge is a *flatness* observation, never a
fatigue/overtraining verdict — RPE is integer-only and there is no HRV/sleep input.

**Empty / new-user:** *"Log a lift 3–4 times to see your strength trend."* Show the logged points as dots, **never a
line** — a trend line under the minimum-N gate is a fabricated signal.

### Section 3 — This week's training (consistency)

**Insight:** *"Is my routine holding?"* — surfaces a quietly collapsing habit before it becomes a quit.

```
┌─────────────────────────────────────────────┐
│  Consistency        hit goal 9 of last 12 wk │  ← [P1] forgiving % (not a fragile streak)
│  ▢▣▣▢ ▣▣▣▣ ▣▣▢▣ ▣▣▣▢ ▣▢▣▣ ▣▣▣▣ ...           │  ← [P1] 8–12 wk heatmap, intensity = sessions/wk
└─────────────────────────────────────────────┘
```

| Card | Content | Visualization | Decision it enables |
|---|---|---|---|
| **[P1] Consistency heatmap** | Compact 8–12 week grid from completed `StartedAt`, intensity = sessions per week. Secondary number is a forgiving **consistency %** — *"hit your goal 9 of the last 12 weeks"* — not an unbroken streak. | [Calendar heatmap](VISUALIZATION.md) + percentage | Re-engage after a visible gap, or protect a building routine — am I a consistent person now, or fading? |

Consistency-% over streak by design: a streak is fragile and confirmshaming; a percentage rewards a 5/7 week and
recovers from a single miss. The heatmap works for **ad-hoc trainees with no plan** (it shows raw frequency when there
is no goal to compute a % against). Full-year detail lives in the [consistency drill-down](DRILL-DOWNS.md).

**Empty / new-user:** an empty grid is honest and fine — it fills in as sessions land. **No fabricated cells.**

### Section 4 — Achievements

**Insight:** *"Did I break through recently?"* — the closed feedback loop on effort.

```
┌─────────────────────────────────────────────┐
│  🏅 Bench press — 82.5 kg × 5, new best       │  ← [P1] latest PR teaser (real lift + load + date)
│     2 days ago · see all records →           │
└─────────────────────────────────────────────┘
```

| Card | Content | Visualization | Decision it enables |
|---|---|---|---|
| **[P1] Latest PR teaser** | One celebratory line **only if a PR landed recently**: *"Bench press — 82.5 kg × 5, new best."* Real lift name + `weight × reps` + relative date, read from `/api/me/records`. Tappable to the full [PR / records timeline](DRILL-DOWNS.md). | Single celebratory line | Reinforce the effort that worked, and see which lift to keep pushing. |

PRs are **genuine e1RM PRs only** — no rep or volume PRs, to avoid extrinsic-reward crowding — and labeled "estimated
1RM" so the claim is honest. The teaser reads `/api/me/records` (current best per lift); the PR *markers on the trend
line* are a different computation derived from the e1RM series, not from this endpoint — see
[Metrics catalogue §B2](METRICS-CATALOGUE.md). Do **not** sum `PrCount` for this.

**Empty / new-user:** *"Your first PR shows up here after a couple of sessions."* Do **not** celebrate the trivial
first-session PRs — every lift is a "PR" the first time it's logged, and celebrating that cheapens the real ones.

### Section 5 — Body progress *(conditional)*

**Insight:** *"Is my weight trending the way I want?"*

| Card | Content | Visualization | Decision it enables |
|---|---|---|---|
| **[P2] Bodyweight trend** | Smoothed line over `MetricEntry` weigh-ins — **rendered only once the range endpoint exists.** Until then, an empty-state invitation to log via the daily check-in. | [Trend line + EMA](VISUALIZATION.md), labeled non-zero axis | Stay the course or adjust — without panicking at a daily spike. |

`MetricEntry` is a real time series, but only a single-date read exists today; charting it requires a new
`GetMyMetricSeries(type, from, to)` query (no schema migration — see [Metrics catalogue §B3](METRICS-CATALOGUE.md)). Until that
ships, Section 5 renders an **empty-state invite, never a chart** — sparse or faked weigh-ins on a labeled axis would
be exactly the dishonesty this page forbids.

**Empty / pre-endpoint:** *"Log your weight in the daily check-in to see your trend here."* No placeholder chart.

### Section 6 — Coach acknowledgement *(optional, opt-in, future)*

| Card | Content | Visualization | Decision it enables |
|---|---|---|---|
| **[Future] Coach / gym acknowledgement** | A lightweight, opt-in acknowledgement of completed sessions from the trainee's coach or gym. | Lightweight ack chip | Relatedness (SDT) — the safe motivational lever GymBro's real coach↔trainee graph supports. |

**Collaborative and opt-in only — never a cross-user strength leaderboard.** Competitive ranking is documented harm
(session-deletion gaming, image management). This section ships only when the acknowledgement path exists.

---

## Glance-layer summary

| # | Section | Card(s) | Priority | Renders when |
|---|---|---|---|---|
| 1 | This-week status | Headline state line · adherence ring | P0 | Always (ring hidden if no assignment) |
| 2 | Strength progress | Direction strip · stall flag | P0 / P1 | Always (line gated to ≥4 points) |
| 3 | Consistency | Heatmap + forgiving % | P1 | Always |
| 4 | Achievements | Latest PR teaser | P1 | When a recent PR exists |
| 5 | Body progress | Bodyweight trend | P2 | Only post range-endpoint |
| 6 | Coach acknowledgement | Ack chip | Future | Opt-in, when path exists |

Sections 1–4 hold the glance layer to **five visual elements** at full population (state line, ring, strength strip,
heatmap, PR teaser) — comfortably under the 7-element budget, with headroom for the stall badge as an inline overlay
rather than a sixth tile.

---

## Empty-state philosophy

A new user is the **default** case, not an edge case — the home must be dignified on day one.

| State | Behavior | Anti-pattern it avoids |
|---|---|---|
| No assignment | Hide the adherence ring; invite a plan | `0/0` ring reading as broken |
| < 4 sessions of a lift | Show dots, copy *"log N more"* | A fabricated trend line from 2 points |
| No PRs yet | *"Your first PR shows up here…"* | Celebrating trivial first-session PRs |
| No weigh-ins / no endpoint | Empty-state invite | A sparse or faked bodyweight chart |
| Empty consistency grid | Render the empty grid honestly | Fabricated heatmap cells |

The through-line: **honest absence beats a confident fake.** An empty grid that fills in earns more trust than a chart
of invented points.

---

## Resolved decisions

The multi-gym adherence denominator is resolved as **D1** in [IMPLEMENTATION.md §2](IMPLEMENTATION.md): the
authoritative goal is the active assignment with the most completed sessions this week, tie-broken by latest
`StartDate`, computed server-side in `GET /api/me/progress/overview`; no active plan → hide the ring, show the raw
count. No open items remain here.
