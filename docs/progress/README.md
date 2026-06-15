# Progress Page — Product Discovery & Redesign

> **Status:** **Phases 1–4 implemented and test-verified** (backend + Flutter; uncommitted; **zero migrations**;
> 529 backend + 151 frontend tests green; redundancy/optimization pass applied). Phase 5+ (readiness/TDEE) is
> data-gated — documented, not built ([IMPLEMENTATION.md §11](IMPLEMENTATION.md)). This folder is the design +
> execution source of truth for the Progress-page rebuild; **[IMPLEMENTATION.md](IMPLEMENTATION.md) is the live
> tracker** (readiness, decisions, progress, rollout). In the same spirit as [`../nutrition/`](../nutrition/) and
> [`../master-data/`](../master-data/). Every metric here is grounded in real GymBro fields (verified against the
> schema); anything not computable is flagged, not faked. Scope boundaries (cross-gym self-scoped trainee vs. own-gym
> tenant-scoped coach; no coach body-metric endpoint) come from [`../PERMISSIONS.md`](../PERMISSIONS.md) and are
> architectural constraints, not preferences.

---

## The problem

Today's Progress page leads with three **lifetime totals** — `9 Sessions · 0.7k Total kg · 7 PRs` — a weekly-volume
bar with two bars, and a "Recent personal records" list that shows the word *"Session"* instead of the lift. The
failure is structural, not cosmetic:

- All three headline tiles are **cumulative counts that can only rise.** The page glows green while a user is quietly
  regressing — their main-lift estimated 1RM can slide for a month and every number on screen still goes up.
- **`Total kg`** conflates heavier vs. more-sets vs. a different body part, renders bodyweight/cardio as `0`, and
  prints `"0.7k"` jargon. It drives no decision.
- **`PRs: 7`** is a summed `WorkoutSession.PrCount` — a count, not a lift. The backend *already* returns the actual
  PR lift, weight and date at `/api/me/records`; the page throws that away for a number.

Applying one test to every element — **"what decision can the user make after seeing this?"** — the current page
scores *none, none, none*. This document set rebuilds it around metrics that answer real questions.

## The thesis

**Answer questions, don't display statistics.** Every element must complete the sentence *"because of this, I will
___."* GymBro is **descriptive by design** — it has no progression/deload/periodization engine — so the page
visualizes honest, **self-referenced** trends (*you vs. past-you*) and leaves prescription to the human coach. It
never fabricates a target, a recovery score, or a comparison the backend cannot compute, because a confident-looking
number the data doesn't back destroys the one thing a Progress page must have: trust.

## The questions the page must answer

| | Trainee | Coach |
|---|---|---|
| **1** | Am I on track this week? *(adherence vs. weekly frequency goal)* | Which client do I message today? *(needs-attention triage)* |
| **2** | Am I getting stronger — and on which lift? *(per-lift e1RM trend + stall)* | Is this client running the plan I gave them? *(per-client adherence)* |
| **3** | Is my routine holding? *(consistency over weeks)* | Which lift has this client stalled on? *(per-client e1RM stall)* |

Everything in the catalogue earns its place by serving one of these. If it serves none, it is cut — not demoted, cut.

---

## What the research actually taught us

We studied Strong, Hevy, Alpha Progression, Fitbod, RP Hypertrophy, Boostcamp, Juggernaut AI, Trainerize, Whoop,
Garmin, Apple Fitness, MacroFactor and Strava — not to copy features, but to extract **why** each shows what it shows.
The convergent lessons:

1. **Estimated 1RM trend per lift is the strength signal.** Every serious logger has deprecated lifetime totals in
   favor of a per-exercise e1RM line, because it's the only number that answers *"am I getting stronger?"* and it's
   self-referenced. → GymBro already stores `EstimatedOneRepMaxKg` on every working set. **This is our headline.**
2. **Adherence beats achievement for everyday motivation.** RP/Trainerize/Apple lead with *did you show up vs. your
   goal*, not *how much you lifted*. → A forgiving weekly-frequency ring (prescribed rest is not a miss) is our P0.
3. **Coaches triage; they don't browse.** RP/Juggernaut/Trainerize coach views answer *who needs attention today*
   first. → A roster sorted by On-track / Drifting / Quiet / Stalled, not a wall of equal-weight clients.
4. **Make load legible, but don't fake physiology.** Garmin/Whoop translate training load and readiness into one
   glanceable verdict — but readiness needs HRV/sleep we don't have. → We can show acute-vs-chronic **volume**
   (shown separately, never as the discredited ACWR ratio); we do **not** manufacture a recovery score.
5. **Trend over noise; ethics over dark patterns.** MacroFactor smooths weight to a trend and is adherence-neutral;
   Strava's healthiest motivation is relatedness (kudos), its unhealthiest is competitive leaderboards. → Bodyweight
   is an EMA trend line, streaks are forgiving and earn-back, and there are **no cross-user strength leaderboards.**
6. **The right chart per metric.** Trend line + rolling average for strength; calendar heatmap for consistency;
   **sorted bar, not radar,** for muscle balance; ring for adherence; annotated timeline for PRs; zero-baseline bars
   for volume. The "5-second insight" rule governs the home: verdict first, evidence second, diagnostics on tap.

## The headline feasibility finding

**The entire P0/P1 trainee experience needs zero schema migrations.** The data is already there — `/api/me/records`
names the PR lift, `/api/me/progress` computes per-week volume, and `EstimatedOneRepMaxKg` is stored per set. The
redesign is mostly a **presentation problem plus a few aggregate read queries**, not a data-model problem. The only
genuinely new read is a per-lift e1RM-over-time series (bounded to the top 3–6 lifts), and the only near-term
migration-adjacent gap is a bodyweight **range** endpoint (`MetricEntry` is a real series but only a single-date read
exists today). See [FEASIBILITY.md](FEASIBILITY.md) for the per-metric verdicts.

---

## Document set

Read in this order. **[SPEC.md](SPEC.md) is the single source of truth**; the rest elaborate one section each and
never contradict it.

| Doc | Owns | Read it to know… |
|---|---|---|
| **[IMPLEMENTATION.md](IMPLEMENTATION.md)** | The execution tracker — readiness, decisions, tasks, tests, rollout | …what's done, what's next, and how to resume after a context reset |
| **[PHASE-1.md](PHASE-1.md)** | Frozen Phase 1 — scope, cards, IA, flow, states | …exactly what to build first, without interpreting the design |
| **[API-CONTRACTS.md](API-CONTRACTS.md)** | Frozen endpoint contracts | …request/response/DTO/auth/perf/query for each endpoint |
| **[SPEC.md](SPEC.md)** | The canonical spec (thesis, catalogue, matrix, IA, drill-downs, coach/trainee, viz) | …the authoritative, internally-consistent whole in one file |
| [METRICS-CATALOGUE.md](METRICS-CATALOGUE.md) | Every candidate metric, categorized | …the exact formula, decision and feasibility tag for each metric |
| [PRIORITY-MATRIX.md](PRIORITY-MATRIX.md) | P0–P3 for trainee and coach | …what shows every day vs. what lives one tap down, and why |
| [MOBILE-DASHBOARD.md](MOBILE-DASHBOARD.md) | The trainee home IA | …the ordered sections, card-by-card text wireframes, empty states |
| [DRILL-DOWNS.md](DRILL-DOWNS.md) | The detail screens | …what each drill-down contains and its glance→detail path |
| [COACH-VS-TRAINEE.md](COACH-VS-TRAINEE.md) | The two surfaces & two data paths | …the roster triage, per-client detail, and the tenant-scope rules |
| [VISUALIZATION.md](VISUALIZATION.md) | Chart choices | …which viz fits each metric, the pitfalls, and the Flutter mapping |
| [FEASIBILITY.md](FEASIBILITY.md) | Technical feasibility | …per-metric verdicts, queries/indexes, caching, and API structure |
| [ROADMAP.md](ROADMAP.md) | Phased rollout | …what ships in Phase 1 (no migration) through wearables (deferred) |
| [MARKET-BENCHMARK.md](MARKET-BENCHMARK.md) | Competitive benchmark + code-verified feasibility | …how GymBro's reporting compares to ~45 products, and what each gap costs to close |
| [AI-NARRATIVE.md](AI-NARRATIVE.md) | Design proposal (not frozen) | …the LLM weekly-narrative lane — writer-not-calculator, span-checked, fallback-guarded |

## What changes on day one (Phase 1, no migration)

The three vanity tiles are replaced with a glance layer built entirely from data we already have:

- **Headline state line** — one sentence: *"Bench & squat trending up · 3 of 4 this week."* The 5-second verdict.
- **Weekly adherence ring** — `completed / FrequencyDaysPerWeek`, forgiving, Monday-anchored in the client's zone.
- **Per-lift strength strip** — e1RM sparklines for the top lifts, each tagged *up / flat / slipping*, with a quiet
  *"flat 4 sessions"* stall badge — the one prescriptive nudge the data honestly supports.
- **Consistency heatmap** — a GitHub-style calendar of the last 8–12 weeks; the habit story.
- **PR timeline** — from `/api/me/records`, naming the lift and weight, not a count.

See [ROADMAP.md](ROADMAP.md) for Phases 2–5 (muscle balance & coach triage → body/nutrition → motivation layer →
wearable readiness, deferred behind data we don't yet collect).

---

*Built from a six-phase product-discovery investigation: codebase audit → industry research → five expert personas
(strength coach, personal trainer, exercise scientist, product designer, data analyst) → synthesis → adversarial
feasibility audit against the real schema. Field/endpoint names referenced throughout were verified to exist in the
`gymbro/` codebase.*
