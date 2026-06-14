# Progress Page — Priority Matrix

> Elaborates **§C** of the [Progress redesign spec](SPEC.md). Every metric here is grounded in real GymBro fields
> (see the [Metrics catalogue](SPEC.md#b-canonical-metrics-catalogue)) and gated by the [Feasibility audit](FEASIBILITY.md).
> The rule for this page is the product thesis: **a metric earns a row only if it drives a decision.** A number that
> answers no question — however true — does not belong on the homepage and is not listed below.

**Priority bands**

| Band | Meaning | Surface |
|---|---|---|
| **P0** | Must show every day | Home glance — stands alone without a tap |
| **P1** | Very valuable | Home, or the top of the first drill-down |
| **P2** | Good detail | Dedicated detail screen, reached by a tap |
| **P3** | Historical / contextual | Inline reference on a detail screen only |
| **❌** | Cut | Not rendered — absence is the honest design |

Two roles, two scopes of truth. Trainee reads are cross-gym self-scoped (`/api/me/*`, `QueryOwnAcrossGyms`); coach
reads are own-gym tenant-scoped (`/api/sessions?traineeId=`, `X-Tenant-Id` required). The matrices below are therefore
**separate by design** — they are not the same list with a label swapped. See [Coach vs trainee](SPEC.md#f-coach-vs-trainee).

---

## A. Trainee

The trainee's Progress page answers three questions in order: *am I on track this week, am I getting stronger and on
which lift, is my routine holding.* Priority tracks that order — adherence and strength direction lead, the diagnostics
behind them sit one tap down.

### P0 — must show every day

| Metric | Why it sits here |
|---|---|
| **Weekly frequency adherence ring** | The single most actionable signal: `completed ÷ FrequencyDaysPerWeek` answers "am I on track this week?" in five seconds, and the decision — *train again before the week resets* — is the page's whole reason to exist. |
| **Per-lift e1RM direction (sparkline, top 1–3 lifts)** | The headline strength question. The Δ-vs-trailing-4-weeks arrow answers *push / hold / change this lift* — the core "am I getting stronger?" decision the old vanity totals never could. |
| **Headline state line** | The five-second insight as one sentence (*"Bench & squat up · 3/4 this week"*). It is pure composition of the two metrics above, so it costs nothing and lets the user decide *do I need to read further* before any card loads. |

### P1 — very valuable

| Metric | Why it sits here |
|---|---|
| **Consistency heatmap + forgiving %** | The habit story. Surfaces a quietly collapsing routine before it becomes a quit, and the decision — *re-engage after a visible gap* — is leading, not lagging. Below P0 only because it is a pattern read, not a same-day "do I train" trigger. |
| **e1RM stall flag** | The one prescriptive nudge the data honestly supports: *deload / swap / change rep-scheme on this lift.* A pure read over the e1RM series; appears only when actionable, so it stays quiet until it has earned attention. |
| **Per-lift PR timeline** | Motivation plus *which lift to push.* The backend already returns this exact data via `/api/me/records` — the current page throws it away for a count. Free to ship, drives a real "where am I breaking through" decision. |
| **Full per-lift e1RM trend chart (selectable)** | The diagnostic behind the P0 sparkline. Per-lift by nature, so it lives one tap down — but it is where *push / hold / change* is actually confirmed against the raw points and PR markers. |

### P2 — good detail screen

| Metric | Why it sits here |
|---|---|
| **Weekly volume trend (vs. 4-wk avg)** | A legitimate *add / cut sets* workload signal, but secondary to e1RM and meaningless under four weeks of data. Must carry the "barbell/dumbbell work only" caption so a bodyweight athlete is not falsely told they did nothing. |
| **Sets per muscle group** | A real *where to add or cut sets* balance decision — but it needs a costly live join (no denormalized muscle on performed rows) and is only six coarse groups, so it earns a drill-down, not the glance. |
| **Session effort (sRPE) diagnostic** | Answers *is RPE creeping at the same load* — useful, but coarse: RPE is integer-only and often null, and `DurationSeconds` is wall-clock. Per-exercise drill-down only; never a headline. |
| **Smoothed bodyweight trend** | High value, low effort — *stay the course / adjust* without panicking at a daily spike. Blocked only on a new `MetricEntry` range endpoint; until it ships, show an empty-state invite, never a faked sparse line. |

### P3 — historical / contextual

| Metric | Why it sits here |
|---|---|
| **Best-set / rep-anchored history** | Drives *add weight next session* (double-progression), but only in the moment of programming the next set — so it belongs inline as a "last time" reference on the lift detail, not as its own card. |
| **Sleep trend** | Rarely logged and decision-thin (context only). Same range-endpoint blocker as bodyweight, lower value — contextual at best. |

---

## B. Coach

The coach view is triage, not self-reflection: *which client do I message today, is this client running the plan I
gave them, which lift have they stalled on.* Priority therefore leads with the roster verdict — the chart is never the
headline.

### P0 — must show every day

| Metric | Why it sits here |
|---|---|
| **Needs-attention triage chip + sortable roster** | The entire reason a busy coach opens the view: *who do I message today.* The chip is the verdict (below-band adherence **OR** no session in N days **OR** stalled key lift); the sorted roster is management-by-exception made glanceable. |
| **Last-active / "quiet" flag** | `now − MAX(StartedAt)` is the cheapest, earliest churn indicator — one glance per client tells the coach *who has gone silent.* It needs no per-lift computation, so it can drive the roster row without an N-client fan-out. |

### P1 — very valuable

| Metric | Why it sits here |
|---|---|
| **Per-client adherence ring + trend** | Answers *is the plan being run as written* — the decision to *re-program vs. progress.* The ring already exists in the client monitor; the redesign elevates it from a header stat to the hero. |
| **Per-client e1RM trend + stall badge** | The programming decision itself: *which lift to deload / swap / push.* Computed lazily on opening a client (never across the whole roster) over **tenant-scoped** sessions — a separately-implemented query from the trainee path. |
| **Assignment status / Apply-latest** | Already built and genuinely actionable — *is this client on the current plan version, and should I apply the latest.* Keep it; it is the bridge from "what's wrong" to "what I change." |

### P2 — good detail screen

| Metric | Why it sits here |
|---|---|
| **Acute vs. chronic load (separate bars)** | A soft *ramping too fast / fading* nudge, shown as two separate bars — never an ACWR ratio (the literature rejects it as an injury predictor). Default to volume-based load; the RPE-weighted leg is too sparse to lead. |
| **Per-client volume trend (vs. baseline)** | Supporting workload context, zero-baseline, ≥4 weeks — the same secondary-to-e1RM caveat as the trainee's. Never duplicated as the coach view's headline. |
| **Per-client PR detail** | *"Client hit a 5 kg deadlift PR Tuesday"* — show the lift, not a bare `PrCount`. Drives a concrete acknowledgement / message, which is why it stays on the client detail rather than the roster. |

### P3 — historical / contextual

| Metric | Why it sits here |
|---|---|
| **Nutrition adherence column** | An additive *nutrition compliance* signal once the coach nutrition-adherence endpoint ships. Designed as a column the row can grow later — contextual until the endpoint exists. |

### ❌ — cut

| Metric | Why it is cut |
|---|---|
| **Body data (weight / sleep) tiles** | No coach endpoint exists for another user's `MetricEntry` — it is private and self-scoped by design. Deliberately deleted, not stubbed: an apologetic `—` placeholder reads as broken, and absence is the honest answer. |

---

## C. What we deliberately leave off the homepage — and why

The homepage is a glance layer with a ~7-element working-memory budget. Things are kept off it for one of three
reasons: **the data can't honestly support them**, **they drive no decision**, or **they're real but belong one tap
down.** None of these is an oversight — each is a guardrail from [§G of the spec](SPEC.md#g-visualization-map).

| Kept off the homepage | Reason | Where it lives instead |
|---|---|---|
| **Lifetime total volume / total kg / "0.7k"** | Vanity. Monotonic, only ever rises, conflates heavier vs. more-sets vs. a different bodypart, and drives **no** decision — it can glow green while the user regresses. | Nowhere. Cut entirely; replaced by directional change. |
| **Bare PR count ("7 PRs")** | A count answers no question. The decision — *which lift is climbing* — needs the lift, the weight, the date. | The per-lift PR timeline (P1) and Records page. |
| **Sets-per-muscle-group bars** | Real balance decision, but a costly live join over six coarse groups — too heavy and too blunt to earn the glance. | Muscle-balance drill-down (P2). |
| **Goal-weight / body-fat % / circumference bars** | **Not computable.** No goal-weight, body-fat, or anthropometric field exists (`TargetWeightKg` in code is lifting load). Fabricating a target erodes the one thing Progress must have — trust. | Nowhere, until a real field exists. |
| **Readiness / recovery / training-density score** | **Not computable.** No HRV/RHR/sleep-contribution; RPE is integer-only and `DurationSeconds` is wall-clock incl. rest. A confident ANS score the data can't back is the exact dishonesty the thesis forbids. | Nowhere. |
| **Strength-Level percentile** | **Not computable.** No normative population dataset — an invented percentile is fabrication. | Nowhere. |
| **Daily calorie / macro "on track" bar** | **Not computable today.** Macros are never summed server-side and no daily target entity exists; both sides need new aggregation *and* a target field. | Roadmap (nutrition), not the workout Progress page v1. |
| **Cross-user strength leaderboards** | Self-referenced by design. Competitive ranking is documented harm (session-deletion, image-gaming); GymBro compares you only to past-you. | Nowhere — replaced by opt-in coach/gym acknowledgement. |
| **Radar / spider muscle map** | A viz cut, not a data cut: six axes distort area and impose an arbitrary order. A sorted horizontal bar reads imbalance accurately. | The muscle-balance drill-down uses a sorted bar instead. |
| **Full e1RM chart, effort diagnostic, bodyweight detail** | Real and valuable, but per-lift / diagnostic by nature — they'd blow the glance budget. | Their respective [drill-down pages](SPEC.md#e-drill-down-pages). |

**Empty states are part of this discipline.** A new user with no plan sees *"Get a plan assigned to track your weekly
goal,"* not a `0/0` ring; a user with fewer than four sessions of a lift sees logged points and *"log N more to see your
trend,"* not a line drawn through noise. The honest absence is the design — see the
[mobile dashboard IA](SPEC.md#d-mobile-dashboard-ia--trainee-progress-home).

---

## Open questions

These are genuine inconsistencies surfaced against the spec — flagged here rather than silently resolved in the matrix.

1. **Bodyweight trend is P2 but its blocker is a P0-class enabler.** The metric is ranked P2 for the trainee, yet its
   only obstacle is a missing `MetricEntry` range query — the same *new-query-not-new-schema* class as the P0 e1RM
   series. The priority is correct (value is real but conditional on the endpoint); the sequencing note is that it
   should not be treated as "far off" — it is one repo method away. See [Feasibility audit §2/§R9](FEASIBILITY.md).
2. **"Stalled key lift" appears in the coach P0 chip but is an N×M fan-out.** The roster chip lists *stalled* as a
   trigger, while the per-client e1RM stall is explicitly *computed lazily on opening a client.* These can't both hold
   at roster scale without precomputation. Binding resolution: the **roster chip uses only cheap signals**
   (adherence below band + last-active gap); "Stalled" is promoted to the chip only after the client is opened, or via
   a deferred per-trainee read model. See [Feasibility audit §R6](FEASIBILITY.md).
3. **Per-lift PR timeline vs. PR markers on the trend line are two different sources.** `/api/me/records` returns the
   *current best per lift*, not the history of successive PRs. The P1 "PR timeline" card and teaser come from
   `/api/me/records`; the PR *markers pinned on the e1RM line* must be derived from the new e1RM series, not from
   `/api/me/records`. Stated here so the two are not implemented as one endpoint. See [Feasibility audit §R3](FEASIBILITY.md).
