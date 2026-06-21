# Progress Page — Window Differentiation (4-week Block vs 12-week Phase)

> **Status:** Design + execution doc for the **v2 differentiation layer**, built ON TOP of the shipped Phases 1–4
> ([README.md](README.md), [ROADMAP.md](ROADMAP.md)). It does **not** re-derive any metric or re-litigate feasibility —
> those live in [SPEC.md](SPEC.md), [METRICS-CATALOGUE.md](METRICS-CATALOGUE.md) and [FEASIBILITY.md](FEASIBILITY.md),
> and nothing here contradicts them. This doc owns ONE new idea: **the 4-week and 12-week windows are two different
> dashboards, not one dashboard at two date ranges.** All guardrails from [SPEC.md §G](SPEC.md) still hold (never
> fabricate; acute/chronic as two soft bars, never an ACWR ratio — [FEASIBILITY.md R10](FEASIBILITY.md); trainee reads
> cross-gym self-scoped — [FEASIBILITY.md R2](FEASIBILITY.md); EMA bodyweight; no leaderboards). **Zero migrations.**

---

## 1. The problem this layer fixes

Phases 1–4 made every metric honest and directional, but the window selector (Week / 4w / 12w) only **rescales the
same sections**. Switching 4w↔12w changes the numbers, not the questions — so the 12-week view, which should be the
strategic one, says nothing the 4-week view doesn't. Applying the page's own test — *"what decision can the user make
after seeing this?"* — the two windows currently enable the **same** decision.

## 2. The thesis — zoom out as you go right

The windows are three altitudes, matched to how training is actually periodized:

| Window | Altitude | The trainee's job | Decision cadence |
|---|---|---|---|
| **Today / Week** | micro | What's happening now (adherence, today's plan) — *unchanged, owned by Phase 1* | daily |
| **4 weeks = "This Block"** | meso (a training block) | **Execution & momentum** — did I execute, am I ahead of last block, what do I fix this week? | weekly |
| **12 weeks = "This Phase"** | macro (a training phase) | **Adaptation & strategy** — is the program working, am I adapting, what do I change next phase? | per-phase |

Two consequences drive every section choice below:

- **4w compares to the PREVIOUS block** (period-over-period Δ). It is operational and granular.
- **12w shows TRAJECTORY** (start→now gain, slopes). It is strategic and smoothed. It never just shows a bigger 4w.

## 3. The two dashboards (information architecture)

Verdict → evidence → action, top to bottom, in both. A thin-data section degrades to an honest empty state, never a
fabricated trend (the existing honesty gate).

### 3a. 4-week — "This Block" (execution & momentum)

| # | Section | Role | Source / feasibility |
|---|---|---|---|
| 1 | **Coach's read** | 1–2 sentence block verdict + the single highest-priority action | `CoachRead` (§4c) — new, rule-based |
| 2 | **Momentum scorecard** | Strength Δ · volume Δ% · consistency · load balance — **all vs last block** | `PeriodStats` (§4a) + existing TopLifts + `LoadBalance` |
| 3 | **Strength — moving / stuck** | Per-lift recent direction; **stalls flagged with an action** | existing `TopLifts` (direction/stall) |
| 4 | **Execution detail** (merge consistency + nutrition + sleep) | Where did execution slip this block? | existing consistency + nutrition-adherence + sleep series |
| 5 | **Recent PRs** | Wins this block (motivation) | existing `RecentPrs` |

*Demoted on 4w:* bodyweight (too noisy < 4 wk → one muted line) and full muscle-distribution (a strategic concern).

### 3b. 12-week — "This Phase" (adaptation & strategy)

| # | Section | Role | Source / feasibility |
|---|---|---|---|
| 1 | **Coach's read** | Phase verdict + strategic recommendation | `CoachRead` (§4c) |
| 2 | **Adaptation scorecard** | Strength gain % · volume trajectory · body trajectory · adherence X/12 | `StrengthGain` + `PeriodStats.weeklyVolumeKg[]` + metric series + consistency |
| 3 | **Is the program working?** | Synthesis verdict (strength↑ + volume↑ + bodyweight on-plan = working) | `CoachRead` effectiveness leg |
| 4 | **Strength — long-term growth** | Per-lift total gain over 12w + **long plateaus flagged as strategic** | `StrengthGain` (first→last, longest plateau) |
| 5 | **Muscle balance** | Sets / muscle / week vs the 10–20 growth zone; chronic under/over-dosing | `MuscleVolume` (§4b) |
| 6 | **Body & nutrition trajectory** (merged) | Bodyweight EMA + intake, shown as cause→effect | existing metric series + nutrition-adherence |
| 7 | **Recovery sustainability** | Sleep trend over the phase (honest: sleep, not a readiness score) | existing sleep series |
| 8 | **PRs banked this phase** | Cumulative achievement | `PeriodStats.prCount` + `RecentPrs` |

### 3c. Does each section earn its place?

| Section | 4w | 12w | Verdict |
|---|---|---|---|
| Top metrics | momentum scorecard (vs last block) | adaptation scorecard (trajectory) | rebuilt, different per window |
| Coach's read | block verdict + action | phase verdict + strategy | **new — both** |
| Strength | moving / stalls → act | total gain / long plateaus | both, different cut |
| Consistency | execution heatmap + misses | compressed to adherence ratio | 4w full, 12w lite |
| PRs | recent wins | phase achievements | both |
| Volume | vs last block | trajectory slope | **new — both** |
| Body | one muted line | hero trajectory vs goal | mostly 12w |
| Nutrition | adherence this block | trend, paired with body | both, merged on 12w |
| Sleep | folded into Execution | sustainability trend | both |
| Recovery (load) | load balance (acute vs chronic) | — (sleep covers it) | 4w only, soft |
| Muscle distribution | quick flag (optional) | strategic hero | mostly 12w |
| Program-working verdict | — | centerpiece | **new — 12w** |

## 4. New primitives (added to `ProgressOverviewDto`)

All folded into the existing single `GET /api/me/progress/overview` call — no new endpoints, no round-trips. The
handler widens its one bounded read to cover **2× the window** (so the previous period is available for deltas) and
adds set-level fields to the projection; all aggregation stays in memory. See [API-CONTRACTS.md](API-CONTRACTS.md)
for the frozen base shape; these are additive.

### 4a. `PeriodStats` — period-over-period (the 4w engine)
`(sessions, prevSessions, volumeKg, prevVolumeKg, workingSets, prevWorkingSets, prCount, weeklyVolumeKg[])`.
Volume = Σ weight×reps over non-warmup sets (matches `SessionMapping.ComputeVolumeKg`; stages count). `prevX` is the
immediately-preceding window of equal length. `weeklyVolumeKg[]` (oldest→newest) feeds the 12w slope. `prCount` is the
**true** count of PRs set in the window (the `RecentPrs` teaser still caps at 3 for display).

### 4b. `StrengthGain` + `MuscleVolume` — the 12w engine
- `StrengthGain(avgGainPct, lifts[])` where each lift carries first→last e1RM over the window (`gainKg`, `gainPct`)
  and `longestPlateauWeeks`. Reuses the shared `StrengthLiftSeries` math + honesty gate (≥4 sessions, Working, e1RM,
  reps ≤ 12). Thin lifts contribute nothing — no fabricated gain.
- `MuscleVolume[]` = `(muscle, setsPerWeek, prevSetsPerWeek)` over the window, via the live
  `ExerciseId → Exercises → ExerciseMuscles (IsPrimary)` join already used by the strength-lift list. **Set-count
  version filters `ParentSetId IS NULL`** (drop cluster = 1 set), per [FEASIBILITY.md §3b](FEASIBILITY.md). Six coarse
  groups; **no MEV/MAV/MRV bands** — the 10–20 "growth zone" is a soft reference shade, not a prescription.

### 4c. `LoadBalance` — honest recovery (4w only)
`(acute7dKg, chronicWeeklyKg, trend)` reusing the existing `LoadTrend` enum (`Detraining` / `Steady` / `Ramping`).
**Two raw volumes + a soft band — never the ACWR ratio** ([FEASIBILITY.md R10](FEASIBILITY.md)): a ratio reads as a
clinical injury claim on integer/sparse, RPE-free data. This is the self-scoped trainee analogue of the coach's
`AcuteChronicLoadDto`. It is **not** a readiness score (no HRV/sleep contribution — that stays Phase 5+, deferred).

### 4d. `CoachRead` — the verdict layer
`(headline, detail, action?, tone)`. **Deterministic and rule-based** over the already-computed aggregates — it is a
*writer, not a calculator* and invents nothing: 4w composes a block verdict + one action; 12w composes a phase verdict
+ the "is the program working?" synthesis (strength gain vs volume vs bodyweight direction). A future swap to the
LLM weekly narrative is drop-in compatible — same DTO — and is scoped in [AI-NARRATIVE.md](AI-NARRATIVE.md), not here.

## 5. Phased rollout (this layer)

Each phase is independently shippable; backend merges first (endpoint live via CD), then the Flutter PR consumes it.
**No migrations in any phase.**

| Phase | Ships | Backend | Flutter |
|---|---|---|---|
| **W1** | The window split | `PeriodStats` (widen read to 2× window) | `BlockScorecard` (4w) + `PhaseScorecard` (12w), branched on range |
| **W2** | Strength & structure cuts | `StrengthGain`, `MuscleVolume` | strength moving/stuck (4w) vs growth/plateau (12w); muscle-balance card |
| **W3** | Honest recovery + body/nutrition pairing | trainee `LoadBalance` (reuse coach load logic) | load-balance card (4w); merged body+nutrition trajectory + recovery (12w) |
| **W4** | The coaching layer | rule-based `CoachRead` + program-working synthesis | coach's-read banner (both); program-working card (12w) |

> **Status (2026-06-20): all four W-phases IMPLEMENTED in one pass, uncommitted.** Backend: `ProgressOverviewDto`
> extended with `Period`/`StrengthGain`/`MuscleVolume`/`Load`/`Coach` (one extra bounded read for the prev window;
> zero migrations; reuses the shared `StrengthLiftSeries`, the `ResolveExerciseMuscleGroupsQuery` muscle map, and the
> `GetClientLoadHandler` acute/chronic logic). 592 backend tests green. Flutter: model mirrors the new DTOs;
> `progress_screen` branches 4w (coach's-read + `_BlockScorecard`) vs 12w (coach's-read + `_PhaseScorecard` +
> `_MuscleBalanceCard`); body demoted off 4w. 251 FE tests green. The coach's-read is rule-based (W4 LLM swap deferred
> to [AI-NARRATIVE.md]). `LoadBalance` reuses the existing `LoadTrend` enum (no UI load card yet — the verdict carries
> the ramping signal). Forward-compatible: a pre-v2 payload degrades to empty defaults, so the app can ship before
> the backend deploys.

## 6. Guardrails honored (unchanged from [SPEC.md §G](SPEC.md))

- **Never fabricate** — thin data → honest empty/insufficient state, not a faked trend or target.
- **Acute vs chronic = two soft bars + a band, never a ratio** ([FEASIBILITY.md R10](FEASIBILITY.md)).
- **No readiness/recovery score** — "Recovery" is sleep trend + volume load balance only; HRV-based readiness stays
  Phase 5+ deferred.
- **Cross-gym self-scoped** trainee reads (`QueryOwnAcrossGyms`, no `X-Tenant-Id`) — never the coach path
  ([FEASIBILITY.md R2](FEASIBILITY.md)).
- **No MEV/MAV/MRV bands** on six coarse muscle groups; the 10–20 zone is a soft shade.
- **No cross-user leaderboards**; PRs/streaks stay forgiving and self-referenced.
