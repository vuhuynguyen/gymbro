# Progress Page — Implementation Guide & Tracker

> **This is the single execution tracker for the Progress-page redesign.** It survives chat/context resets: a future
> session reads §0 and is fully oriented. The *what/why* lives in [SPEC.md](SPEC.md); the *frozen how* lives in
> [PHASE-1.md](PHASE-1.md) + [API-CONTRACTS.md](API-CONTRACTS.md); this file owns **execution order, decisions,
> tasks, tests, acceptance, rollout, and progress.** Update the checklists in §8 as work lands.

---

## 0. Resume instructions (read this first after any reset)

1. **Orient:** read [README.md](README.md) → this file §1 (readiness) + §2 (decisions) → [PHASE-1.md](PHASE-1.md) → [API-CONTRACTS.md §1](API-CONTRACTS.md).
2. **Conventions are verified** against the real codebase (§7). Trust them, but re-confirm a file path before editing — code may have moved.
3. **State of play:** check §8 checkboxes. Unchecked = not done. The current phase is whatever §8 shows in progress.
4. **Do not re-research or redesign.** The design is frozen. If a genuine new ambiguity appears, add it to §2's decision list with a proposed default; don't silently diverge.
5. **When you finish a task,** tick its box in §8 and, if you changed behavior, update the **one** doc that owns that fact (no duplication — the gymbro `docs/` update rule).

## 1. Phase readiness (execution order for all phases)

| Phase | Classification | One-line gate |
|---|---|---|
| **1 — P0 trainee home** | ✅ **Ready to Implement** | None — decisions resolved, zero migrations |
| **2 — P1 diagnostics + coach triage** | ✅ **Done** (2a trainee + 2b coach; backend+frontend, audited, tests green) | — |
| **3 — Body & nutrition** | ✅ **Done** (no migration); vs-target deferred to nutrition program | Adherence % + goal-weight ride existing columns (query-only); only calorie/macro **vs-target** needs the nutrition program's daily-target entity |
| **4 — Coach insight (acute-vs-chronic load)** | ✅ **Done** (no migration; 529 BE + 151 FE tests green) | Streak + genuine-PR celebration already shipped (Phase 1/2); roster-stall precompute (migration) + kudos (infra) **deferred** with seams |
| **5+ — Wearable / readiness / TDEE** | ⚪ **Deferred — groundwork documented (§11)** | No data source (no HRV/sleep/RHR/wearable ingestion). The `MetricEntry` series *read* infra already generalizes, but `MetricEntry` has **no `Source` field** — so wearable ingestion needs an ingestion *writer* **plus** either a `Source`-column migration or a `Type`-naming convention (e.g. `"wearable:hrv"`) to tag it (§11). **Never fabricate a readiness score.** |

### Phase 1 — ✅ Ready to Implement
- **Why:** every metric is computable from existing columns; all blocking decisions resolved (§2). One new self-scoped read, no migration, no cache.
- **Remaining blockers:** none.
- **Backend:** `GetMyProgressOverviewQuery` + handler + DTOs; `MeController` action; auth-exemption entry; goal lookup vs `Modules.WorkoutPlan`. No migration, no cache.
- **Frontend:** rebuild `progress_screen.dart`; new repo + DTOs + provider; `_Sparkline`/`_Heatmap` `CustomPaint`. No dependency, no routing change.
- **Depends on:** nothing.
- **Risks / debt:** on-read e1RM bounded to top-3 lifts — watch p95; **must not** reuse all-status `ProgressWeekDto.Sessions` for adherence; honesty gate (`Reps ≤ 12`, `Working`, `Strength|Bodyweight`) must be in the query, not the UI.

### Phase 2 — 🟡 Ready After Minor Decisions
- **Why:** queries only, still zero required migrations; but two calls needed first (D4 surfacing; optional e1RM index).
- **Backend:** `GET /api/me/exercises/{id}/e1rm-series`; `GET /api/me/progress/metrics/series` (bodyweight, new repo method, Nutrition chain, **no column**); coach `GET /api/clients/progress/roster` + `GET /api/clients/{id}/progress/strength` (**separate tenant-scoped handlers** — [FEASIBILITY R2](FEASIBILITY.md)); optional `IX_PerformedSets_Working_E1rm` (only if profiling demands — that index *is* a migration).
- **Frontend:** per-lift detail route `/progress/lift/:exerciseId` (full-screen, `_rootKey`) with a `CustomPaint` trend chart (**D11**, no `fl_chart`); make home lift rows tappable; Body section (bodyweight trend); muscle-balance + volume drill-downs; coach roster screen.
- **Depends on:** Phase 1 (overview + e1RM logic reused server-side — extract a shared e1RM calculator).
- **Risks / debt:** **R2 tenant isolation is the single most dangerous item** — coach series must be a separate handler with the tenant filter ON. R5 (coach 20-session cap → windowed query).

### Phase 3 — ✅ Done (no migration); vs-target deferred
- **Key reframing (vs the original "requires migrations" estimate):** goal-weight rides the existing free-text `MetricEntry` as `Type="goal_weight"` (**D12**) and nutrition adherence reads the existing `DailyNutritionLog.AdherencePct` (**D13**) — both **query-only, zero schema change**.
- **Backend (done):** `GET /api/me/progress/nutrition-adherence` (self-scoped; planned-days; current-week avg; `HasPlan`); goal-weight via `LogMetricEntryCommand` type `"goal_weight"` (no endpoint). Contracts frozen ([API-CONTRACTS §5](API-CONTRACTS.md)). 516 tests green.
- **Frontend (done):** Body section is goal-aware (goal line + distance-to-goal + set-goal sheet; goal read uses a far-past `from` so an old goal never drops out of the 12-week window); nutrition-adherence card (empty-invite when `HasPlan=false`, loads independently). Audit caught + fixed a Critical wire-key mismatch; 138 tests green.
- **Deferred (the genuinely architectural part):** calorie/macro **vs-target** needs a daily-target entity (a migration the **nutrition program** owns); Progress consumes it later via a documented seam — never fabricates a target.
- **Depends on:** Phase 2 (bodyweight series).

### Phase 4 — 🟠 Requires Additional Architecture
- **Why:** roster-level stall + kudos need precompute/new infra; motivation layer needs none.
- **Backend:** `lastE1rmAdvanceAt`-style read model (migration + `SessionCompletedEvent` handler); acute/chronic (volume-based) windowed query; opt-in kudos on the membership graph.
- **Depends on:** Phase 2 coach surface.
- **Risks / debt:** read-model invalidation; motivation ethics — **no cross-user leaderboards** (design constraint).

### Phase 5+ — ⚪ Future / Deferred
- **Why:** readiness/recovery, adaptive TDEE, relative strength, sleep/mood correlations all need a data source we don't collect.
- **Gate:** wearable ingestion + a `MetricType` lookup + (for TDEE) Phase-3 macros. Hard guardrails never lift: no fabricated readiness, no MEV/MAV/MRV bands on 6 coarse groups, no strength percentile without normative data.

## 2. Decisions register

All resolved unless flagged. Where a metric/doc previously said "Open question," it now points here.

| ID | Decision | Resolution | Phase |
|---|---|---|---|
| **D1** | Multi-gym adherence goal denominator | Authoritative goal = the active `PlanAssignment` with the **most completed sessions this week**, tie-broken by **latest `StartDate`**. No active assignment → `Goal: null`, hide ring, show raw count. | 1 |
| **D2** | PR teaser vs PR markers (two sources) | `/api/me/records` is the **single source** for the PR teaser + Records page (current best per lift). PR **markers on the trend line** are derived from the e1RM **series** (Phase 2), never from `/records`. | 1 / 2 |
| **D3** | Bodyweight trend sequencing | It's **P2** and needs a new `MetricEntry` range endpoint → ships in **Phase 2/3**, not Phase 1. Until then the Body section is **hidden** (no faked line). | 2/3 |
| **D4** | Coach roster "Stalled" | Roster chip = **3 cheap states** (`onTrack`/`drifting`/`quiet`). "Stalled" is resolved on **client open**, not per roster row. A `lastE1rmAdvanceAt` precompute is **deferred to Phase 4**, only if the lazy path proves slow. | 2 |
| **D5** | Coach trend depth | Coach progress endpoints get their **own `from`/`to`-windowed** query — never the 20-row monitor page (else 8-week trends truncate). | 2 |
| **D6** | e1RM compute-on-read vs precompute | **Compute on read** for v1, bounded to top-3 lifts. A `PrCount`-style denormalized series is **deferred** until profiling proves the on-read query slow. | 1 (read) / 4 (precompute) |
| **D7** | Bullet/target-vs-actual viz | **Dropped from v1** — no banded-target metric exists; degrade to ring/sparkline. | 1 |
| **D8** | Caching | **No caching in Phase 1** (matches the existing un-cached self-scoped convention). If profiling shows need: `IDistributedCache` + event-driven eviction on `SessionCompletedEvent` ([FEASIBILITY §4](FEASIBILITY.md)). | 1 / later |
| **D9** | Empty result status | `200 OK` + empty-but-valid DTO. **No `204`** for `/api/me/*` reads. | 1 |
| **D10** | `ConsistencyPct` denominator | "weeks observed" = weeks from the **first completed session in the 12-week window** through the current week (capped at 12) — **not** a flat 12. Forgiving for newcomers (2 weeks, both hit → 100%), honest for laggards (a gap reads as misses). Null when no goal or no sessions. | 1 |
| **D11** | Per-lift trend chart library | **No `fl_chart`.** The Phase 2 per-lift trend chart uses `CustomPaint` (line + faint raw points + PR markers), matching the app's hand-rolled-chart convention and avoiding a network-fetched dependency / supply-chain surface. Revisit only if interactive tooltips genuinely demand a lib. | 2 |
| **D12** | Goal-weight storage | **No migration** — goal-weight rides the existing free-text `MetricEntry` as `Type="goal_weight"` (latest entry = current goal), written via the existing `LogMetricEntryCommand`, read via the Phase-2a metric-series. `MetricEntry` is explicitly designed for this (*"a new signal is a new value, never a migration"*). | 3 |
| **D13** | Phase 3 scope | Ships **goal-aware bodyweight trend** + **nutrition adherence %** (from existing `DailyNutritionLog.AdherencePct` — query-only). Calorie/macro **vs-target** is **deferred to the nutrition program** (it needs a daily-target entity = a migration that program should own); Progress consumes it later via a documented seam, never fabricates a target. | 3 |
| **D14** | Phase 4 buildable slice | Acute-vs-chronic **load** (coach per-client detail): 7-day acute volume vs 28-day chronic weekly-average, tenant-scoped, shown as **two separate bars — NEVER an ACWR ratio** (R10), volume-based (RPE too sparse/integer). Forgiving streak + genuine-PR celebration **already ship** (overview `CurrentStreakWeeks` + PR teaser + in-session `PrCount`). **Roster-stall precompute** (migration) and **kudos** (new infra) are **deferred** with documented seams (D4 + motivation ethics: no cross-user leaderboards). | 4 |
| **D15** | Ad-hoc nutrition counting (honest) | Self-logged (no-plan) nutrition is **counted** — like ad-hoc workout sessions already are — **without inflating adherence**. The adherence trend (`Days`/`CurrentWeekAvgPct`) stays **plan-only**: an ad-hoc day is 100% by convention, so folding it into the % would **fake a perfect record**. Instead `NutritionAdherenceDto` gains a separate **tracking** signal: `LoggedDaysThisWeek` (current local-week days with ≥1 logged item, **any source**) + `HasAnyLogging` (ever logged, bounded `EXISTS`), via a second ALL-SOURCES read over `QueryOwnAcrossGyms` (no `Source` filter; `Status ∈ {Completed,Substituted}` = a real logged item, so a touched-empty day doesn't count). A plan-less self-logger thus reads `HasPlan:false`/empty `Days` yet `LoggedDaysThisWeek>0`/`HasAnyLogging:true`. **Query-only, NO migration, NO cache, 200 (no 204).** | 3 |

### Remaining decisions that genuinely want a product owner (small)
These have a **chosen default** so implementation is not blocked — flag only to confirm/override:
1. **D1 tie-break** — "most completed this week, then latest `StartDate`" is an edge-case rule for multi-gym trainees. *Default chosen; PO may override.*
2. **New-user home** — show a motivational first-run hero vs. minimal empty cards. *Default: hero.*
3. **D4 coach surfacing** — confirm the roster ships with 3 states and "Stalled" only on client-open (vs. pulling the precompute into Phase 2). *Default: 3 states.*

## 3. Implementation order (Phase 1)

```
B1 DTOs ─┬─ B2 Query+Handler ─ B3 Controller+exemption ─ B4 Backend tests
         └─ (D1) goal lookup vs Modules.WorkoutPlan
                                   │
F1 DTO models ─ F2 repo+provider ──┴─ F3 screen rebuild ─ F4 widget tests
                                                          │
                                                  V verify + rollout
```
Backend (B*) and frontend (F*) can proceed in parallel once the **contract (API-CONTRACTS §1) is frozen** — it is. F2 can mock the contract until B3 lands.

## 4. Backend tasks (Phase 1)

- [x] **B1** Add `ProgressOverviewDto`, `WeekAdherenceDto`, `ConsistencyDto`, `ConsistencyDayDto`, `LiftDirectionDto`, `LiftTrendDirection` to `Application/DTOs/PersonalTrainingDtos.cs`.
- [x] **B2** `GetMyProgressOverviewQuery` (parameterless) + `GetMyProgressOverviewHandler`: completed-session window scan; Monday-week adherence (D1 goal via `Modules.WorkoutPlan`'s new self-scoped `GetOwnActiveAssignmentsQuery`); daily consistency; top-3 honesty-gated e1RM series → direction/stall/spark; top-3 PRs via internal `GetMyPersonalRecordsQuery`. `Result<ProgressOverviewDto>`. One bounded read, then in-memory aggregation (mirrors `GetMyProgressHandler`).
- [x] **B3** `MeController` action `[HttpGet("progress/overview")]` (thin); added the `GetMyProgressOverviewQuery` exemption entry (`ImperativeGuarded`) in `Tests/Authorization/TenantAuthorizationExemptions.cs`.
- [x] **B4** Tests: `Tests/Commands/GetMyProgressOverviewHandlerTests.cs` (14 mocked unit tests) + 2 self-scoped/IDOR facts in `Tests/Integration/UnifiedPersonalTrainingTests.cs`.

## 5. Frontend tasks (Phase 1)

- [x] **F1** `data/models/progress_models.dart` — hand-written DTOs with `core/utils/json.dart` coercers, enum `.parse`.
- [x] **F2** `progressRepositoryProvider` (`GET /api/me/progress/overview`, 404-graceful) + `progressOverviewProvider` (`FutureProvider.autoDispose`).
- [x] **F3** Rebuild `features/progress/progress_screen.dart`: Sections 1–4, all states (PHASE-1 §5), `RefreshIndicator`, `GbSkeletonList` loading, `ErrorRetry`. New `_Sparkline` + `_Heatmap` `CustomPaint`. Tokens via `context.gb`/`AppSpacing`/`AppText`. Delete the vanity tiles + 2-bar chart.
- [x] **F4** Widget tests (§6) — `test/widgets/progress_screen_test.dart` (12 tests, `progressOverviewProvider` overridden, no network).

## 6. Testing checklist

**Backend** — all green (unit tests time-relative to `UtcNow`; integration validated against real Postgres)
- [x] Adherence counts **completed-only**, current Monday week in client TZ; abandoned/in-progress excluded.
- [x] D1 goal selection (multi-assignment) + ad-hoc user → `Goal: null`, `HasActivePlan: false`.
- [x] e1RM honesty gate: excludes `Reps > 12`, non-`Working`, non-`Strength|Bodyweight`, null reps/weight.
- [x] One point per session = `MAX`; drop-cluster (`ParentSetId`) not double-counted/excluded wrongly — also asserted end-to-end against Postgres (the nested `Where`+`Max` over `e.Sets` ran in SQL, not just translated).
- [x] Top-lift selection: ≥4 sessions, top-3 by count; `< 4` → omitted; new user → empty.
- [x] Stall: no new best in last 3 exposures → `Stalled`, correct `StallSessions`.
- [x] `ConsistencyPct` (D10): weeks-observed = first session in window → current week (capped 12); a specific case locks `100`; null when no goal **or** no sessions.
- [x] PR teaser = top-3 from `/records`, never `PrCount`.
- [x] `200` + empty DTO for a new user (not `204`); self-scoped (no tenant header); IDOR — never reads another user's data (ClientA vs ClientB have **distinct** completed counts, so the assertion discriminates rather than `0 == 0`).
- [x] Auth-exemption test green.

**Frontend** — covered by `test/widgets/progress_screen_test.dart` (12 widget tests, green)
- [x] Loading → skeleton; error → retry (+ Retry action); data → 4 sections.
- [x] No-plan → ring hidden, raw count shown; new-user → hero; `TopLifts` empty → strength invite; no PRs → quiet placeholder; `ConsistencyPct` null → no % caption.
- [x] Direction tags map up/flat/down (incl. `Flat N×`); `down` is the only red (asserted `Slipping` == `gb.danger`); headline never red.
- [ ] Pull-to-refresh invalidates; tokens only (no hardcoded colors); dark mode renders. *(tokens/refresh verified at code level in the audit; not yet asserted in a gesture/dark-mode widget test — app is light-only, so "dark mode" is N/A.)*

## 7. Conventions (verified — match these)

- **CQRS / DTO / controller / errors / versioning / 200-empty:** see [API-CONTRACTS §0](API-CONTRACTS.md). Self-scoped queries take **no validator** and **no cache** (mirror `GetMyProgress`).
- **Namespaces:** `Modules.WorkoutSessionModule.<Layer>.<Subfolder>`. DTOs in `Application/DTOs`, query in `Application/Queries`, handler in `Application/Queries/Handlers`.
- **Tenant bypass:** `QueryOwnAcrossGyms(currentUser.UserId)` only — `.IgnoreQueryFilters()` + manual soft-delete. Never with a client-supplied id.
- **Flutter:** Riverpod hand-declared `FutureProvider.autoDispose`; `.when()`/`AsyncValueView` for loading/error; tokens via `context.gb` + `AppSpacing`/`AppText`/`AppRadius`; reuse `GbCard`/`GbRing`/`GbSectionTitle`/`GbTappableRow`/`GbIconTile`/`Eyebrow`/`GbSkeletonList`/`ErrorRetry`/`EmptyState`. **No chart library** — all charts are hand-rolled `CustomPaint` (Phase 1 sparkline/heatmap; Phase 2 per-lift trend, **D11**).
- **Two migration chains** exist (AppDbContext + Nutrition) — Phase 1 touches neither.

## 8. Progress checklist (update as work lands)

**Discovery & design** — ✅ complete
- [x] Research, spec, metrics, priority, dashboard, drill-downs, coach/trainee, visualization, feasibility, roadmap
- [x] Architecture revalidation (backend + frontend conventions, precise query feasibility)
- [x] Decisions resolved (§2); contracts frozen ([API-CONTRACTS](API-CONTRACTS.md)); Phase 1 frozen ([PHASE-1](PHASE-1.md))

**Phase 1 — implementation** — 🟡 backend done (audit-hardened); frontend F1–F4 done (widget tests green), device-verify + rollout pending
- [x] B1 DTOs · [x] B2 query+handler · [x] B3 controller+exemption · [x] B4 backend tests
- [x] Backend audit hardening: `ConsistencyPct` now implements **D10** (weeks-observed from the first session in the window, not a flat 12; null with no goal **or** no sessions); unit tests are **time-relative** to `UtcNow` (no calendar-date time-bomb); the integration suite seeds a real completed-session graph and asserts the EF→SQL top-lift e1RM projection against Postgres + a **discriminating** ClientA-vs-ClientB IDOR check.
- [x] F1 DTO models · [x] F2 repo+provider · [x] F3 screen rebuild · [x] F4 widget tests (`test/widgets/progress_screen_test.dart`)
- [ ] Verify on device/emulator · [ ] Rollout (§9)

**Phase 2 — ✅ COMPLETE** (2a trainee diagnostics + 2b coach surface; backend + frontend, adversarially audited, all tests green; one pre-existing order-dependent integration test being made deterministic)
- [x] Extracted the shared `E1rmSeriesCalculator` (Modules.WorkoutSession/Application) — the Direction/Stall/Delta/Spark math, now used by BOTH `GetMyProgressOverviewHandler` and the new drill-down (existing ProgressOverview tests stay green).
- [x] `GET /api/me/exercises/{id}/e1rm-series` (`GetMyExerciseE1rmSeriesQuery`/Handler) — self-scoped via `QueryOwnAcrossGyms`; §1 honesty gate in SQL; MAX-per-session + top-set capture; `IsPr` = running-max; from/to default 12 wk; 200 + empty Points for an unknown lift; reuses the calculator. Exemption added (`ImperativeGuarded`).
- [x] `GET /api/me/progress/metrics/series` (`GetMyMetricSeriesQuery`/Handler, **Nutrition**) + `MetricEntryRepository.GetOwnSeriesAsync` (IgnoreQueryFilters + TraineeId + soft-delete, case-insensitive type) — latest-per-local-day; default 12 wk; 200 + empty Points; **no column, no migration**. Exemption added (`ImperativeGuarded`).
- [x] `GET /api/me/exercises/strength-lifts?weeks={4..52,12}&muscleGroup=` (`GetMyStrengthLiftsQuery`/Handler) — the **UNCAPPED** sibling of the overview's top-3 strip: ALL strength lifts the caller performed in the window (never capped at 3), self-scoped via `QueryOwnAcrossGyms`, sorted by current e1RM desc, 200 + empty list for a new user. Shares the windowed e1RM **gathering** (new `StrengthLiftSeries` — extracted from `GetMyProgressOverviewHandler`, which now reduces top-3 through it) + the `E1rmSeriesCalculator` math (no duplication). The §1 honesty gate is applied as a **FLAG**: `hasTrend` ⇔ ≥4 qualifying sessions; below that `direction/stalled/stallSessions/sparkE1rmKg` stay default/empty (client shows e1RM + sessionCount only — **never a fabricated direction**). Each lift is enriched with `primaryMuscleGroup` (camelCase one of chest|back|legs|shoulders|arms|core, null when unresolved) via the new cross-module `ResolveExerciseMuscleGroupsQuery`; optional `muscleGroup` filter narrows by PRIMARY match in memory after resolution (controller tolerant-parses the 6 known groups, ignores unknown). Exemptions added (`GetMyStrengthLiftsQuery` `ImperativeGuarded`; `ResolveExerciseMuscleGroupsQuery` `InternalLookup`). **NO migration.**
- [x] Cross-module muscle contract: `ResolveExerciseMuscleGroupsQuery(IReadOnlyList<Guid>)` → `Result<IReadOnlyDictionary<Guid,string>>` in `Modules.Exercise/Application/Queries` (mirrors `ResolveExerciseTrackingTypesQuery`): one batched `AsNoTracking` read projecting `Exercise.Muscles`, reduces to the PRIMARY group per id, returns it as a **camelCase string** (not the `MuscleGroup` entity enum) so WorkoutSession never references `Modules.Exercise.Entities` — `ModuleBoundaryConventionTests` stays green.
- [x] `MeController` actions for the series + strength-lifts (thin, `ToFailureResult`); the e1rm-series/metrics contracts **FROZEN** ([API-CONTRACTS §2, §3](API-CONTRACTS.md)).
- [x] Tests: `E1rmSeriesCalculatorTests`, `GetMyExerciseE1rmSeriesHandlerTests`, `GetMyMetricSeriesHandlerTests`, `ResolveExerciseMuscleGroupsHandlerTests` (primary group per id as camelCase string; six-group coverage; empty-set short-circuit; unknown-id absent), `GetMyStrengthLiftsHandlerTests` (uncapped — 5 lifts not 3; `hasTrend=false` with NO fabricated direction for a <4-session lift; muscle enrichment + primary-group filter narrows; null filter returns all; self-scoped IDOR) — all mocked + time-relative — plus e1rm/metric integration facts in `UnifiedPersonalTrainingTests` (real Postgres: MAX-per-session projection, latest-per-day, case-insensitive, discriminating IDOR). All green.
- [x] Frontend trainee diagnostics (gymbroapp): per-lift detail route `/progress/lift/:exerciseId` (full-screen via `_rootKey`, `CustomPaint` e1RM trend + PR markers + stall callout, **D11** no `fl_chart`); home lift rows tap through to it; Body section (independent `bodyweightSeriesProvider`, EMA `CustomPaint` on a labeled non-zero axis, empty-invite / quiet-on-error, never blocks §1–4). `autoDispose(.family)` providers + 404-graceful repo reads; defensive model coercers. Widget tests `lift_detail_test.dart` (8), `body_section_test.dart` (3), `progress_lift_nav_test.dart` (1) — all providers mocked, no network. `flutter analyze` clean (touched files), full suite green.
- [x] Coach surface backend (Phase 2b): new `ClientsController` (`api/clients`, tenant-scoped, `[Authorize]`, thin) with **two separate tenant-scoped handlers** that read the **tenant-filtered** session `Query()` (EF filter **ON**, **never** `QueryOwnAcrossGyms` — R2). `GET /api/clients/progress/roster` (`GetClientRosterQuery`/Handler) → `RosterDto { Items: ClientStatusDto[] }`: gated on `WorkoutLogViewAll`; per-member LastActiveAt=MAX(StartedAt), CompletedThisWeek + AdherencePct bucketed in the **client's** zone, in-gym goal via the new WorkoutPlan `ResolveActiveAssignmentGoalsQuery` (tenant-filtered), `RosterStatus` (onTrack/drifting/quiet — **D4** cheap signals: 10-day quiet gap, 75% band, NO roster "Stalled"), sorted at-risk-first; member list + names via the new shared `ResolveTenantMemberNamesQuery` (BuildingBlocks contract, handled in User module); 200 + empty Items. `GET /api/clients/{id}/progress/strength?take=6` (`GetClientStrengthQuery`/Handler) → `LiftTrendDto[]` (trimmed §2 shape): `ResourceAccessGuard` binds the coach to their own gym **and** `ITenantRoleResolver` verifies the trainee is a member of the active tenant (non-member → **404**, never a silent rescope to self); reuses `E1rmSeriesCalculator`. Exemptions added (roster/strength `ImperativeGuarded`; the two internal lookups `InternalLookup`). Contracts **FROZEN** ([API-CONTRACTS §4](API-CONTRACTS.md)).
- [x] Coach backend tests: `GetClientRosterHandlerTests` (status classification at thresholds, auth-forbidden, tenant-filtered-not-cross-gym), `GetClientStrengthHandlerTests` (R2 tenant-scoping, non-member 404, auth, honesty gate, ≥4-session gate, calculator parity), and the **R2 cross-gym isolation** integration suite `CoachClientProgressIsolationTests` (a client training in gym A **and** gym B; the gym-A coach's roster counts + e1RM series exclude the gym-B PR, asserted absent; the client's own self-view still sees both gyms, proving the exclusion is the tenant filter; non-member → 404; plain member forbidden) — all green against real Postgres. Convention tests (`TenantScopedRequestConventionTests`, `ModuleBoundaryConventionTests`) stay green.
- [x] Frontend coach surface (gymbroapp, Phase 2b): tenant-scoped roster screen `/coach-progress` + per-client strength detail `/coach-client/:id` (both full-screen via `_rootKey`, reachable off the Coach hub). `coachRosterProvider`/`clientStrengthProvider` watch `activeTenantIdProvider` → refetch on gym switch; return empty when no active tenant (no tenant-less read). `CoachProgressRepository` goes through `apiDioProvider` so `AuthInterceptor` attaches the membership-validated `X-Tenant-Id` (**R2** — separate tenant-scoped path, never `QueryOwnAcrossGyms`); `roster()` 404-graceful, `clientStrength()` surfaces 403/404 as a real error (never masks an access-boundary leak as empty). At-risk-first triage (Quiet → Drifting → On track, most-stale first); mandatory "This gym only" caption. Per-client detail reuses the **shared** `TrendChart` (`features/progress/trend_chart.dart`, extracted from the Phase-2a lift detail — no duplicate painter) and renders **no body-metric card** (another user's `MetricEntry` is private/self-scoped, COACH-VS-TRAINEE §3). Tokens only (`context.gb.*`); **no new pub dependency**. Widget tests `coach_progress_roster_test.dart` (8), `client_strength_detail_test.dart` (6), `coach_progress_tenant_switch_test.dart` (3) — all providers mocked, no network. `flutter analyze` error/warning-free; full suite green (126 tests).

**Phase 3 — ✅ backend (P0/P1, NO migration) + frontend done**
- [x] **Nutrition adherence read** — `GET /api/me/progress/nutrition-adherence?from=&to=` (`GetMyNutritionAdherenceQuery`/Handler, **Nutrition module**): self-scoped via `IDailyNutritionLogRepository.QueryOwnAcrossGyms(currentUser.UserId)`; **planned days only** (`Source == FromAssignment` — ad-hoc days excluded); per-day adherence reuses the SQL count projection (`SummaryRowProjection` → `ToSummaryDto`: finalized `AdherencePct` on closed days, live recompute on open days); default window **4 weeks** (nutrition is daily); `HasPlan` = a bounded `EXISTS` over all planned days ever (never-planned ⇒ `HasPlan:false`/empty/null-avg); `CurrentWeekAvgPct` = mean over the current Monday-week's planned days in the trainee's zone (null if none); **200 + empty**, never 204/404. `NutritionAdherenceDto`/`DailyAdherenceDto` in `Application/DTOs/DailyNutritionLogDtos.cs`. **NO migration, NO cache.** `MeController` action `[HttpGet("progress/nutrition-adherence")]` (thin, `ToFailureResult`); exemption `GetMyNutritionAdherenceQuery` (`ImperativeGuarded`). Contract **FROZEN** ([API-CONTRACTS §5](API-CONTRACTS.md)).
- [x] **Ad-hoc nutrition COUNTED (D15) — NO migration, NO cache.** `NutritionAdherenceDto` extended to `(HasPlan, Days, CurrentWeekAvgPct, LoggedDaysThisWeek, HasAnyLogging)` — the first three **unchanged** (adherence trend stays plan-only so an ad-hoc 100%-by-convention day never inflates the %). `GetMyNutritionAdherenceHandler` now runs a **second ALL-SOURCES read** over `QueryOwnAcrossGyms(currentUser.UserId)` (no `Source` filter): `LoggedDaysThisWeek` = `CountAsync` of current Monday-week days (caller zone) with a logged item, `HasAnyLogging` = bounded `AnyAsync` ever-logged — both keyed on the new `NutritionMapping.HasLoggedItem` predicate (`Status ∈ {Completed,Substituted}`, any source — confirms ad-hoc Completed items + ticked planned items, excludes touched-empty days). Wires camelCase `loggedDaysThisWeek`/`hasAnyLogging` (global JSON policy). Self-logger now reads `HasPlan:false`/empty `Days` yet is counted — mirrors ad-hoc workout sessions. 200, never 204. [API-CONTRACTS §5](API-CONTRACTS.md) + **D15** updated.
- [x] **Goal-weight (D12) — verified, NO migration, NO endpoint.** Confirmed the existing `LogMetricEntryCommandValidator` has no type whitelist, so `LogMetricEntryCommand(Type:"goal_weight", …)` passes and persists as a free-text `MetricEntry`, and `GetMyMetricSeriesQuery(type:"goal_weight")` reads it back (latest-per-day = current goal). The frontend reuses the existing metric write + the Phase-2a metric-series read; no new backend surface. Documented in [API-CONTRACTS §5](API-CONTRACTS.md).
- [x] **Tests** (time-relative; integration uses explicit from/to ranges to avoid shared-DB contamination): `Tests/Commands/GetMyNutritionAdherenceHandlerTests.cs` (per-day adherence, current-week avg incl. null, ad-hoc exclusion, HasPlan=false for never-planned + brand-new, self-scope/IDOR, explicit-range bound, **+D15: self-logged-only → HasPlan=false/empty Days yet LoggedDaysThisWeek=N/HasAnyLogging=true; plan user adherence unchanged + completed-day count any source; touched-empty + planned-uncompleted days NOT logged days; prior-week-only → 0/true**), `Tests/Commands/GoalWeightMetricRoundTripTests.cs` (validator accepts the free-text type; LogMetricEntry → GetMyMetricSeries round-trip; latest-per-day wins), and four facts in `Tests/Integration/NutritionFlowTests.cs` (planned-day adherence self-scoped against real Postgres, discriminating ClientA-vs-ClientB IDOR, goal-weight round-trip with cross-user isolation, **+D15: Owner ad-hoc day counted by the tracking signals yet absent from the trend, real Postgres**). All green; convention/authorization suites stay green.
- [x] Frontend (gymbroapp): Body section graduates from empty-invite to **goal-aware** bodyweight trend — `bodyweightSeriesProvider` (`type=weight`) overlaid with a dashed goal line + goal-aware axis bounds + distance-to-goal caption from `goalWeightProvider` (`type=goal_weight`, latest point = current goal); a "set a goal weight" affordance when none is set, whose sheet writes `goal_weight` via `POST /api/me/nutrition/metrics` (`setGoalWeight`, **not** 404-shimmed — a real write surfaces failure) and invalidates `goalWeightProvider`. New **nutrition-adherence card** consumes `GET /api/me/progress/nutrition-adherence` via `nutritionAdherenceProvider` (`autoDispose`, 404-graceful): `hasPlan:false` → "follow a meal plan" invite, plan-but-no-days → "log a day" nudge, data → `GbRing` (current-week avg) + hand-rolled `_AdherenceStripPainter` bar strip; loads independently and stays quiet on loading/error so it never blocks §1–4. Model `NutritionAdherence`/`DailyAdherence.fromJson` read the **frozen camelCase wire keys** `days`/`localDate`/`adherencePct` (§5 `NutritionAdherenceDto`/`DailyAdherenceDto`); defensive coercers + `pct` clamped 0–100. Tokens only (`context.gb.*`/`AppSpacing`/`AppRadius`/`AppText`); **no new pub dependency**. Widget tests `body_section_test.dart` (goal/no-goal/at-goal, set-goal invalidation) + `nutrition_adherence_test.dart` (invite / no-days nudge / data ring+strip / no-rollup / quiet-on-error) **and** `fromJson` round-trip parse tests against the literal frozen payload (guards the wire-key contract). `flutter analyze` error/warning-free; full suite green.

**Phase 4 — ✅ Done (acute-vs-chronic load, NO migration)**
- [x] **Backend** `GET /api/clients/{traineeId}/progress/load` (`GetClientLoadQuery`/Handler, Modules.WorkoutSession) — tenant-scoped (EF filter **ON**, **never** `QueryOwnAcrossGyms` — R2), gated like the strength endpoint (ResourceAccessGuard + member check; non-member → 404). `AcuteChronicLoadDto(AcuteVolumeKg, ChronicWeeklyVolumeKg, LoadTrend)` — 7-day acute Σ working volume vs 28-day ÷ 4 chronic weekly average; `LoadTrend` is a SOFT band (Ramping/Steady/Detraining) computed internally — **the ratio is NEVER exposed** (R10); volume-only (RPE too sparse). 200 + zeros when no sessions. Exemption added. Contract **FROZEN** ([API-CONTRACTS §4.3](API-CONTRACTS.md)).
- [x] **Backend tests** `GetClientLoadHandlerTests` + R2 cross-gym isolation extended in `CoachClientProgressIsolationTests` (gym-A coach's load excludes gym-B volume). **Full backend suite 529 green, 0 failed.**
- [x] **Frontend** Workload card on the coach per-client detail: two separate zero-baseline `CustomPaint` bars (acute 7-day vs chronic weekly-avg, kg-labeled) + a **soft** trend chip (Ramping up / Steady / Easing off) — no ratio, no injury claim; "this gym only" caption. `clientLoadProvider` (`autoDispose.family`, watches `activeTenantIdProvider`); surfaces 403/404 as a real error. **Restructured the detail so the Workload card is a permanent sibling** (the strength section resolves its async state inline) — fixes a double-`/load`-fetch on gym switch the original nesting caused. Widget tests `workload_card_test.dart` + updated `coach_progress_tenant_switch_test.dart`. **151 FE tests green.**
- [x] **Note:** forgiving streak + genuine-PR celebration already ship (Phase 1/2: overview `CurrentStreakWeeks` + PR teaser + in-session `PrCount`); roster-stall precompute (migration) + kudos (infra) deferred (D14). The Phase-4 *workflow* hit a hard session limit mid-run; the code it had written compiled, and the remaining verification (tests, the double-fetch fix, R2 + no-ratio self-audit) was finished in the main loop.

**Phase 3 (calorie/macro vs-target) + Phase 5+** — ⬜ deferred (see §1, §11); vs-target → nutrition program's daily-target entity (D13); Phase 5 readiness/TDEE is data-gated (groundwork §11).

## 9. Rollout plan

1. **Branch off `dev`** (never commit to `main` directly); one PR for backend, one for frontend (or a single coordinated PR). Per-repo, since API and app are separate repos.
2. **Backend first** to a deployed environment so the app can hit a real `/api/me/progress/overview`; the app's 404-graceful repo shim keeps older clients safe until then.
3. **Feature-flag the new screen** if a flag mechanism exists; otherwise ship the rebuilt `progress_screen.dart` directly (it degrades to empty states for thin-data users).
4. **No DB migration, no reseed** for Phase 1.
5. **Verify** against acceptance criteria (§10) on device, then `dev → main` (squash) per the team flow.

## 10. Acceptance criteria (Phase 1 done)

- [x] Opening the Progress tab shows the four sections, verdict-first, within the 7-element budget — no vanity tiles. *(widget tests)*
- [x] `GET /api/me/progress/overview` returns the frozen shape; `200` + empty DTO for a new user. *(unit + integration)* — p95 < 350 ms not yet load-measured (pending a perf run).
- [x] Adherence is completed-only against the D1 goal; ad-hoc users see no ring. *(unit + integration + widget)*
- [x] Top-lift direction/stall obey the honesty gate; lifts with < 4 points are absent. *(unit + integration)*
- [x] PR teaser names the lift (never a count); consistency heatmap + % render; all empty/error states behave per PHASE-1 §5. *(widget tests)*
- [x] No new dependency, no migration, no cache added; all tests (§6) green; docs/checklists updated.
- [ ] On-device/emulator smoke + p95 load measurement (pending a running environment).

## 11. Phase 5+ — wearable / readiness / TDEE (groundwork & honest deferral)

There is **no code to build** here yet — these metrics need a data source GymBro does not collect. The honest deliverable is the seam + the non-fabrication stance, not a fake number.

**What already generalizes (no new read infra):** `GET /api/me/progress/metrics/series?type=X` reads **any** `MetricEntry` type (free-text by design). Weight and sleep flow through it today; HRV, RHR, body-fat, mood — any future signal — flow through the *same* endpoint the moment rows of that type exist. No new query, no migration. (Goal-weight, D12, already proves the pattern.)

**What's missing — an ingestion writer AND a way to tag its source:** a wearable/AI ingestion adapter that writes `MetricEntry` rows of the appropriate types (Apple Health / Garmin / Whoop), plus the OAuth/device-link surface — owned by a wearable-integration workstream, **not** a Progress-page change. Note the read endpoint generalizes, but `MetricEntry` has **no `Source` field** today (its columns are `TraineeId`/`Type`/`Value`/`Unit`/`LocalDate`/`LoggedAtUtc`). So distinguishing wearable-written rows from self-logged ones needs **either** a migration that adds a `Source` column **or** a `Type`-naming convention (e.g. `Type="wearable:hrv"` / `"ai:tdee"`) that rides the existing free-text column — no `Source=wearable` value can be written until one of those exists. When ingestion lands, the readiness/body sections light up by consuming the existing series endpoint.

**What stays NOT buildable — and why (never fabricate):**

| Metric | Blocked on | Guardrail |
|---|---|---|
| Readiness / recovery score | HRV + RHR + sleep-contribution (none collected; RPE is integer-only) | No ANS/recovery index the data can't back — it reads as a clinical claim |
| Adaptive (measured) TDEE | daily macro rollup (nutrition program) + bodyweight series + logging-completeness | No self-correcting calorie target until intake is summed server-side |
| Relative strength (lift ÷ bodyweight) | a dense canonical weight series | per-session `BodyweightKg` is too sparse to chart honestly |
| Strength-Level percentile | a licensed normative population dataset | no invented percentile |

**Phase-5 readiness checklist (ship a metric only when ALL true):** (1) its data source writes real `MetricEntry`/rollup rows; (2) coverage is dense enough to be non-misleading; (3) the value is a measurement or an honestly-labeled estimate, never a fabricated index. Until then the sections show an empty-state invite, never a faked chart — the same discipline as Phases 1–4.
