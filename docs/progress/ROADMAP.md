# Progress Page — Future Roadmap

> **Status:** Phased delivery plan for the Progress-page redesign. The *what* and *why* live in [SPEC.md](SPEC.md)
> (canonical metrics, IA, visualization map) and [PRIORITY-MATRIX.md](PRIORITY-MATRIX.md) (P0–P3 per persona); the
> *can-we* lives in [FEASIBILITY.md](FEASIBILITY.md) (adversarial audit, query design, isolation risks). **This
> document sequences them** — it does not re-derive a metric's value or re-litigate its feasibility. Every phase
> ships only metrics the audit rates buildable on the data that phase's prerequisites provide; nothing here
> contradicts the spec. The headline invariant: **Phases 1–2 require zero EF migrations** — the work is query
> surface, not schema.

The redesign's thesis (replace monotonic vanity totals with directional change against a self-referenced baseline —
see [SPEC.md §A](SPEC.md)) is delivered incrementally. Each phase is independently shippable and leaves the page
honest: a phase never renders a card whose data the prior prerequisites can't yet compute. Two hard scope boundaries
hold across **every** phase and are architectural, not negotiable — trainee reads are cross-gym self-scoped
(`/api/me/*`, `QueryOwnAcrossGyms`, no `X-Tenant-Id`); coach reads are own-gym tenant-scoped
(`/api/...?traineeId=`, `X-Tenant-Id` required, `WorkoutLogViewAll`). They are **never** the same query
([FEASIBILITY.md R2](FEASIBILITY.md)).

## Phase map at a glance

| Phase | Theme | Migration? | Net-new backend surface | Unlocks |
|---|---|---|---|---|
| **1** | P0 trainee dashboard from data we already have | **None** | 1 new read query (e1RM series) + reuse 3 existing endpoints | "Am I on track / getting stronger" answered in 5s |
| **2** | P1 diagnostics + minimal new queries | **None** (queries only) | e1RM full-chart/stall, muscle-balance join, bodyweight range endpoint, coach roster | Stall localization, balance, weight trend, coach triage |
| **3** | Body & nutrition integration | **Yes** (nutrition macro rollup + target) | Daily macro aggregation + target entity, bodyweight↔strength cross-refs | "Is my body trending the way my goal needs" |
| **4** | Coach insights & motivation layer | **Some** (read-model for roster stall) | Roster stall precompute, kudos graph, acute/chronic | Management-by-exception + relatedness loop |
| **5+** | Wearable / readiness & advanced | **Yes** (new ingestion + sources) | Wearable adapter, MetricType lookup, adaptive TDEE | Readiness, relative strength, AI coaching — *future* |

---

## Phase 1 — P0 trainee dashboard (no migration)

**What ships.** The full glance layer of the trainee Progress home ([SPEC.md §D](SPEC.md), Sections 1–4) — every P0
item in [PRIORITY-MATRIX.md §C1](PRIORITY-MATRIX.md). The page stands alone without a single tap. No coach surface
in this phase; no body or nutrition cards.

| Card / element | Source (all existing columns) | Decision it enables |
|---|---|---|
| **Weekly frequency adherence ring** (`done/goal`, e.g. "3/4") | `COUNT(WorkoutSession WHERE Status=Completed)` per Monday week in `ClientTimezone` ÷ the authoritative `PlanAssignment.FrequencyDaysPerWeek` | Train again before the week resets, or accept I'm drifting off plan |
| **Per-lift e1RM direction strip** (sparkline + Δ, top 1–3 lifts) | `MAX(EstimatedOneRepMaxKg) WHERE SetType=Working` per `(ExerciseId, StartedAt)`, Δ vs trailing 4 wk | Push / hold / change *this* lift |
| **Headline state line** ("Bench & squat up · 3 of 4 this week") | Composition of the two above — no new fields | The 5-second verdict before any chart |
| **Consistency heatmap + forgiving %** | Completed `StartedAt` bucketed per day/week; % = weeks-hitting-goal ÷ weeks-observed | Re-engage after a gap / protect a building routine |
| **Latest PR teaser** (real lift + weight×reps + relative date) | `/api/me/records` (`PersonalRecordDto`) — **already returns this exact data** | Reinforce effort; which lift is climbing |

**Data prerequisites.**
- **One genuinely new read query:** the e1RM **series** per lift, self-scoped via `QueryOwnAcrossGyms`. No endpoint
  returns a series today (`/api/me/records` is single lifetime-best per lift). **Bound to the top 3–6
  most-trained lifts** — resolve them with a cheap `COUNT(*) GROUP BY ExerciseId` first, then series only for
  those. This is the single most important cost control ([FEASIBILITY.md §3a](FEASIBILITY.md)). New endpoint:
  `GET /api/me/progress/strength?lifts=top&take=6`; `204` when no e1RM-bearing working set exists.
- **Honesty gates enforced server-side in the query** (not just hidden in UI): suppress e1RM for `Reps > 12`
  (Epley breaks down), emit only `TrackingType ∈ {Strength, Bodyweight}`, require both `Reps` and `WeightKg`
  non-null. The client adds the **≥4-data-point gate** before drawing a line ([FEASIBILITY.md R8](FEASIBILITY.md)).
- **Reuse, do not rebuild:** `/api/me/progress` (weekly `Sessions` + `TotalVolumeKg` already computed),
  `/api/me/records` (PR teaser), `/api/me/sessions` (feeds the heatmap via `StartedAt`).
- **Adherence denominator must be made authoritative.** A trainee can hold multiple active assignments across gyms;
  there is no single cross-gym goal. Pick one deterministically (active assignment with the most completed sessions
  this week, tie-broken by latest `StartDate`) and surface it as `ProgressWeekDto.WeeklyGoal`. Ad-hoc users with no
  active assignment: **hide the ring** (never render `0/0`), show raw frequency only ([FEASIBILITY.md R1](FEASIBILITY.md)).
- **No new column, table, or migration.** Caching per [FEASIBILITY.md §4](FEASIBILITY.md): `me:e1rm:{userId}:{exerciseId}`,
  `me:records:{userId}`, `me:progress:{userId}`, all evicted on `SessionCompletedEvent`.

**User value unlocked.** The trainee's three core questions are answered on the glance layer with no tap:
*am I on track this week* (ring), *am I getting stronger and on which lift* (e1RM strip), *is my routine holding*
(heatmap). The vanity "9 Sessions / 0.7k kg / 7 PRs" wall — monotonic numbers that glow green during a regression —
is gone, replaced by directional, self-referenced, anti-shame signals. Ships on the existing schema; the only
engineering risk is the one new series query, which is bounded and cached.

---

## Phase 2 — P1 diagnostics + minimal new queries (no schema migration)

**What ships.** The drill-down layer and the prescriptive nudges — every P1 trainee item and the **coach triage
P0/P1** that needs only queries. Still **zero EF migrations**: the new work is a muscle-group join, a `MetricEntry`
range query, and a coach roster projection — all over existing columns.

| Card / element | Persona | Source | Decision it enables |
|---|---|---|---|
| **Full per-lift e1RM trend chart** (selectable, PR markers pinned) | Trainee P1 | Unsuppressed e1RM series; PR markers = points where session-best strictly exceeds running max | The diagnostic behind the sparkline — push/hold/change |
| **e1RM stall flag** | Trainee P1 | "Best working-set e1RM has not exceeded prior best in last ~3–4 exposures" off the series | Deload / swap / change rep-scheme on *this* lift |
| **Per-lift PR timeline** (records page) | Trainee P1 | `/api/me/records` for the list; PR *progression* markers from the series, **not** from records | Which lift is climbing vs gone quiet |
| **Weekly volume trend** (vs 4-wk avg, zero-baseline) | Trainee P2 | `ProgressWeekDto.TotalVolumeKg` + trailing-4-wk mean | Workload rising/flat/spiking → add/cut sets |
| **Sets per muscle group** (sorted horizontal bar, drill-down) | Trainee P2 | Live join `PerformedExercise.ExerciseId → Exercises → ExerciseMuscles`, `IsPrimary`, count working parentless sets per 6-group week | Where to add/cut sets; spot a lagging area |
| **Smoothed bodyweight trend** | Trainee P2 | EMA over `MetricEntry WHERE Type='weight'` by `LocalDate` | Stay the course / adjust; don't panic at a daily spike |
| **Needs-attention roster** (status chip + mini-ring + last-active) | Coach P0 | Tenant-scoped projection: per member `MAX(StartedAt)`, completed-this-week ÷ goal | Which client do I message today |
| **Per-client adherence ring + trend** | Coach P1 | Completed ÷ `FrequencyDaysPerWeek` per ISO week, tenant-scoped | Re-program vs progress the plan |
| **Per-client e1RM trend + stall** | Coach P1 | e1RM series over **tenant-scoped** sessions (own gym only) | Which lift to intervene on |

**Data prerequisites.**
- **`MetricEntry` range query** (the spec's only "blocked" near-P1 item): add
  `MetricEntryRepository.GetOwnSeriesAsync(traineeId, type, from, to)` + `GET /api/me/progress/metrics/series?type=weight&from=&to=`.
  Today only single-date read exists (`GetMyNutritionMetricsQuery(Date)`). The `MetricEntries` table is already a
  time series (`LocalDate`, `LoggedAtUtc`, index `IX_MetricEntries_TraineeId_LocalDate`) — **no new column, no
  migration**. Match the `Type` string **case-insensitively/normalized** ("weight"/"Weight"/"bodyweight") — it is
  unvalidated free text ([FEASIBILITY.md R9](FEASIBILITY.md)). Until the endpoint exists, Section 5 shows an
  **empty-state invitation to log**, never a faked chart.
- **Muscle-balance live join** is unavoidable — performed rows carry no denormalized muscle. Accept the
  retro-recolor caveat (a recategorized exercise rewrites old logs' attribution). **Set-count version filters
  `ParentSetId IS NULL`** (drop cluster = 1 set); the volume version must **not** (stages count) — diverging
  silently breaks parity with `SessionMapping.ComputeVolumeKg` ([FEASIBILITY.md §3b](FEASIBILITY.md)). Only 6
  coarse groups: **no MEV/MAV/MRV bands.** Kept drill-down, never the home glance.
- **Coach surface is a brand-new tenant-scoped projection.** `GET /api/clients/progress/roster` (roster) and
  `GET /api/clients/{traineeId}/progress/strength` (per-client e1RM). The per-client series **must be a separate
  handler with the EF tenant filter ON** — never parameterize the `QueryOwnAcrossGyms` path by someone else's id,
  or it leaks that client's sessions from every gym ([FEASIBILITY.md R2](FEASIBILITY.md), the single most dangerous
  item). Coach progress reads must **not** piggyback the 20-session monitor page; give them date-bounded windowed
  queries, or 8-week trends silently truncate for active clients ([FEASIBILITY.md R5](FEASIBILITY.md)).
- **Roster chip uses cheap signals only** (adherence-below-band + last-active gap). The "stalled key lift" leg is an
  N×lifts fan-out — **compute it lazily on opening a client**, not at roster-list time
  ([FEASIBILITY.md R6](FEASIBILITY.md)). Cache `coach:roster:{tenantId}` and `coach:client:{tenantId}:{traineeId}`
  — keys **must** include `tenantId` so a coach result never leaks across gyms ([FEASIBILITY.md §4](FEASIBILITY.md)).
- **Delete the coach body-data card.** No endpoint reads another user's `MetricEntry` (private/self-scoped by
  design). An apologetic `—` placeholder reads as broken — absence is honest ([SPEC.md §B7](SPEC.md)).

**User value unlocked.** Trainees get the *why* behind each glance signal — the full trend they tap into, the one
honest prescriptive nudge the data supports (stall flag), and a real balance/weight read for the first time. Coaches
get their entire reason for opening the view: a triage roster sorted at-risk-first that answers *who do I message
today* and *is this client running the plan I gave them*, lifting the frequency ring from a header stat to the hero.
Still no migration — the page's depth doubled on query surface alone.

---

## Phase 3 — Body & nutrition integration

**What ships.** The Body-progress section graduates from a single bodyweight line into goal-aware body trends, and
nutrition adherence joins the picture. This is the **first phase that needs migrations** — the data the spec marks
*Not computable today* (daily macro totals, a calorie/macro target, a goal-weight field) requires real schema, and
the spec is explicit it must not be faked ([SPEC.md §B3, §B6](SPEC.md)).

| Card / element | Source / new work | Decision it enables |
|---|---|---|
| **Nutrition plan adherence %** (item-count) | `AdherencePct` (Completed/Substituted ÷ planned) — already computed at day-close on `DailyNutritionLog` | Am I following my meal plan |
| **Calories / protein vs target** | New daily macro rollup over `LoggedItem` macros **+ a new daily-target entity** | Eat more protein today |
| **Goal-weight progress** (vs target) | New goal-weight field (today's `TargetWeightKg` is lifting load, not bodyweight) + the Phase-2 weight series | Am I on track to my body goal |
| **Weight↔strength context** | Cross-reference Phase-2 weight series with per-session `BodyweightKg` | Is strength holding while weight moves |

**Data prerequisites.**
- **Daily macro aggregation + a target entity (migration).** Macros are never summed server-side and **no daily
  calorie/macro target entity exists** — both sides need building ([SPEC.md §B6](SPEC.md)). This rides the existing
  nutrition program: the snapshot capture on `LoggedItem` is already built (macros denormalized at log time), so the
  read model is *query-and-visualize*, finalized out-of-band via `DailyLogClosedEvent` — see the nutrition
  analytics plan in the repo [ROADMAP](../ROADMAP.md) (`/api/me/nutrition/summary`). This roadmap **consumes** that
  endpoint; it does not duplicate its design.
- **Goal-weight field (migration).** A genuine `MetricEntry`-typed or profile target. Until it exists, render no
  goal-weight bar — fabricating a target erodes the trust the whole page depends on ([SPEC.md §B3](SPEC.md)).
- **Bodyweight series from Phase 2** is the prerequisite that's already in place — Phase 3 builds *on* it, it does
  not re-add it.
- **Still no coach body-metric read** — the boundary from Phase 2 holds. Relative strength (lift ÷ bodyweight) stays
  *future*: per-session `BodyweightKg` is frequently null, too sparse to chart honestly ([SPEC.md §B3](SPEC.md)).

**User value unlocked.** The trainee's secondary question — *is my body trending the direction my goal needs* — gets
a real answer for the first time, tying the smoothed weight line to a goal and to nutrition adherence. Nutrition stops
being a separate app and becomes context on the same Progress surface. Critically, this is delivered without faking a
single target: every "vs target" here is backed by a real new field, never an invented band.

---

## Phase 4 — Coach insights & motivation layer

**What ships.** The coach view matures from triage to *insight*, and the trainee gets the relatedness/motivation loop
the spec scopes ([SPEC.md §B7, §B8](SPEC.md)). Some of this needs a small read-model migration; the motivation layer
needs none.

| Card / element | Source / new work | Decision it enables |
|---|---|---|
| **Roster "Stalled" chip at list level** | New per-trainee `lastE1rmAdvanceAt`-style **read model** (precompute) | Spot a stalled client without opening them |
| **Acute vs chronic load** (two separate bars, coach) | 7-day Σ volume vs 28-day avg, tenant-scoped | Is this client ramping too fast / fading |
| **Per-client PR detail** | Session-detail per-lift PR (the lift, not a count) | "Client hit a 5kg deadlift PR Tuesday" |
| **Nutrition adherence column** (coach) | `AdherencePct` per client via the coach nutrition endpoint | Nutrition compliance at a glance |
| **PR celebration + forgiving streak** (trainee) | `PrCount` at `Complete()` for in-session celebration; streak = consecutive weeks hitting goal | Reinforce effort; habit reinforcement |
| **Coach/gym acknowledgement (kudos)** | Membership graph + completed session, opt-in | Relatedness (SDT) — the safe motivation lever |

**Data prerequisites.**
- **Roster-level stall (small read-model migration).** Promoting "Stalled" from the per-client open to the roster
  row means precomputing a per-`(trainee, exerciseId)` last-e1RM-advance marker — a new table updated on
  `SessionCompletedEvent`, the `PrCount` read-model pattern. **Defer until Phase 2's lazy-open stall proves too slow
  at the roster** ([FEASIBILITY.md R6](FEASIBILITY.md)); ship the cheap-signals roster first.
- **Acute vs chronic must show two separate bars, never the ACWR ratio** (literature rejects the ratio as an injury
  predictor). Default the load to **volume-based**, not `DurationSeconds×RpeOverall` — `RpeOverall` is integer-only
  and frequently null ([SPEC.md §B5](SPEC.md), [FEASIBILITY.md R10](FEASIBILITY.md)). Needs the windowed coach query
  from Phase 2 (28-day window), not the 20-row page.
- **Coach nutrition-adherence endpoint** (`/api/nutrition/adherence?traineeId=`) ships in the nutrition program; the
  coach row is designed so nutrition is an *additive* column ([SPEC.md §B7](SPEC.md)). No Progress-page migration.
- **`PrCount` is celebration-only.** Never sum it for a progress or stall signal — it is per-session, not per-lift,
  and `0` on Abandoned sessions ([FEASIBILITY.md R4](FEASIBILITY.md)). Celebrate **genuine e1RM PRs only**; the
  streak is **plan-aware** (prescribed rest never breaks it), no confirmshaming, never monetized.
- **Kudos is collaborative and opt-in — never cross-user strength leaderboards** (documented harm). Uses GymBro's
  real coach↔trainee graph; partial/future infra ([SPEC.md §B8](SPEC.md)).

**User value unlocked.** Coaches shift from "open each client to find problems" to management-by-exception — the
roster itself surfaces stalls and load spikes, and PR detail tells them the *lift* not a count. Trainees get a closed
feedback loop (genuine-PR celebration, forgiving streak) and the one safe relatedness lever (coach acknowledgement),
raising adherence without the dark patterns the spec forbids.

---

## Phase 5+ — Wearable / readiness & advanced (future)

**What ships.** Everything the audit rates **Not computable** today because the *signal does not exist* — not a
query gap but a missing data source. Deliberately last: it needs new ingestion infrastructure, and shipping a
confident readiness number the data can't back is exactly the trust-destroying move the thesis forbids.

| Capability | Why it's last | Prerequisite |
|---|---|---|
| **Readiness / recovery score** | No HRV/RHR/sleep-contribution; RPE is integer-only | Wearable ingestion + `MetricType` lookup (`Source=wearable`) |
| **Adaptive (measured) TDEE** | Self-correcting calorie target | Phase-3 macro rollup + Phase-2 weight series + logging-completeness guard |
| **Relative strength (lift ÷ bodyweight)** | Per-session `BodyweightKg` too sparse today | A dense canonical weight series (`MetricEntry` as canonical, cross-ref `WorkoutSession.BodyweightKg`) |
| **Sleep trend, mood/energy correlations** | Only weight/sleep written by any client today | `MetricType` lookup + wearable/AI sources |
| **Strength-Level percentile** | No normative population dataset | Licensed normative data — **do not fabricate** |
| **AI coaching insight** | Needs the full typed series + grounded reference data | The master-data moat + nutrition analytics maturity |

**Data prerequisites.** The full deferred `MetricEntry` design from the repo [ROADMAP](../ROADMAP.md) — the
`MetricType` lookup (`ValueKind`, `AllowMultiplePerDay`), the `Source=wearable|ai` ingestion adapter (a new *writer*,
not a new model), and photos/private-body-data handling. One typed series absorbs the entire open-ended expansion
list (body fat, HRV, sleep, energy, mood) as data, not schema. **Hard guardrails never lift:** no fabricated
readiness score, no MEV/MAV/MRV bands on 6 coarse groups, no strength percentile without a real normative dataset, no
training density from wall-clock `DurationSeconds` ([SPEC.md §G universal guardrails](SPEC.md)).

**User value unlocked.** The "should I train hard today" and "is my calorie target self-correcting" questions —
genuinely valuable, genuinely impossible on current data. Sequenced honestly: each lands only when its source exists,
so the page never shows a number the backend can't stand behind.

---

## Open questions

These are sequencing ambiguities surfaced while aligning the phases to the spec and audit — flagged here rather than
resolved silently against the SPEC.

1. **Bodyweight trend priority vs phase.** The spec lists smoothed bodyweight as **P2**, but the audit notes its
   blocker (a `MetricEntry` range query) is the same *class* of work as a P0/P1 enabler — a one-method repo
   addition, no migration ([FEASIBILITY.md R9, §2](FEASIBILITY.md)). It is placed in **Phase 2** here (with the
   other no-migration queries) rather than deferred to Phase 3, so the trainee gets a real weight line before the
   heavier goal-weight/nutrition migrations land. The card stays an empty-state invite until the endpoint ships —
   consistent with the spec. Confirm this placement is acceptable, or pull it forward into Phase 1 if the range
   query is cheap enough to bundle with the e1RM series.
2. **Coach roster as P0 vs its build cost.** The matrix rates the needs-attention roster **coach P0**
   ([PRIORITY-MATRIX.md §C2](PRIORITY-MATRIX.md)), but today the coach pages one client at a time — the roster is a
   *new* tenant-scoped aggregate, not a reuse. It lands in **Phase 2**, not Phase 1, because Phase 1 is
   trainee-only. If a coach-first launch is wanted, the roster (cheap signals only) and per-client adherence could
   be pulled into a Phase 1b with no extra migration — the only constraint is the separate-handler isolation rule
   ([FEASIBILITY.md R2](FEASIBILITY.md)).
3. **e1RM-series read model (precompute) trigger.** The spec and audit both say compute-on-read for v1 and add a
   `PrCount`-style precomputed series only **if measured slow** ([FEASIBILITY.md §2, §3a](FEASIBILITY.md)). That
   precompute is a migration; it is provisionally slotted in Phase 4 alongside the roster-stall read model, but its
   real trigger is a measurement, not a phase. Treat the Phase-4 placement as "no earlier than," contingent on
   observed query cost.
