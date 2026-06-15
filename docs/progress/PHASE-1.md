# Progress Page — Phase 1 (Frozen)

> **Status:** **FROZEN — ready for implementation.** Another engineer can build this without interpreting the design.
> Scope, cards, IA, flow, states, refresh, and performance are final; the contract is in
> [API-CONTRACTS.md §1](API-CONTRACTS.md). Decisions are recorded in [IMPLEMENTATION.md §Decisions](IMPLEMENTATION.md).
> This elaborates the canonical [SPEC.md §D](SPEC.md) and the P0 rows of [PRIORITY-MATRIX.md §A](PRIORITY-MATRIX.md);
> it does not contradict them.

**Related:** [API-CONTRACTS.md](API-CONTRACTS.md) · [IMPLEMENTATION.md](IMPLEMENTATION.md) · [MOBILE-DASHBOARD.md](MOBILE-DASHBOARD.md) · [VISUALIZATION.md](VISUALIZATION.md)

---

## 1. Final scope

**In:** the trainee Progress **tab home** (mobile, Flutter), rebuilt as a single-call glance layer of four sections.
**Out (later phases):** per-lift trend chart drill-down (Phase 2, needs `fl_chart`), bodyweight/body section
(Phase 2/3), nutrition (Phase 3), the entire coach surface (Phase 2), muscle balance / volume / effort drill-downs
(Phase 2), motivation/kudos (Phase 4).

- **Backend:** **one** new self-scoped read — `GET /api/me/progress/overview`. No migration. No caching.
- **Frontend:** rebuild the existing Progress tab. **No new dependency** (sparkline via `CustomPaint`, ring via the
  existing `GbRing`, heatmap via a small `CustomPaint`/grid, rows via `GbTappableRow`).
- **Lift rows are display-only in Phase 1** — no tap-through (the tappable chart is Phase 2).

## 2. Final card list

Every card states the decision it enables; a card that enables none is not here.

| # | Card | Content | Source (API-CONTRACTS §1) | Visualization | Decision |
|---|---|---|---|---|---|
| 1a | **Headline state line** | One sentence, e.g. *"Bench & squat up · 3 of 4 this week"*. Green or neutral, **never red**. | Composed client-side from `ThisWeek` + `TopLifts` | Single line of weighted text | The 5-second verdict — must I act this week? |
| 1b | **Weekly adherence ring** | `CompletedSessions/Goal` (e.g. "3/4"); caption "2 days left". Forgiving. | `ThisWeek` | `GbRing` (sweep) | Train again before the week resets, or rest easy |
| 2 | **Strength strip** | Top 1–3 lifts: name · `CurrentE1rmKg` · mini-sparkline · direction tag (↑ up / flat N× / ↓ slipping). | `TopLifts[]` | Row + `CustomPaint` sparkline + tag | Push / hold / change **this** lift |
| 3 | **Consistency heatmap** | ~12-week calendar of completed sessions; caption "78% · 5-week streak". | `Consistency` | Calendar heatmap | Re-engage after a gap / protect a routine |
| 4 | **PR teaser** | Top 2–3 PRs naming the lift: *"Deadlift · 140 kg × 3 (e1RM 153) · Tue"*. | `RecentPrs[]` | `GbTappableRow` + amber `GbIconTile` (display-only) | Reinforce effort; which lift is climbing |

## 3. Final IA (top → bottom)

```
[GbAppHeader: "Progress" + bell]
Section 1 — This week        → headline state line + adherence ring         (the glance verdict)
Section 2 — Strength         → top-lift e1RM direction strip
Section 3 — Consistency      → heatmap + consistency % / streak
Section 4 — Personal records → PR teaser (top 2–3)
[bottom nav: Coach · Log · Start(＋) · Progress · Profile]
```
Working-memory budget ≤ 7 elements; reading order is verdict-first. No body/nutrition/coach sections render in Phase 1.

## 4. Final user flow

1. User taps the **Progress** tab → `progressOverviewProvider` (`FutureProvider.autoDispose`) fires `GET /api/me/progress/overview`.
2. **Loading** → `GbSkeletonList(count: 4)`.
3. **Data** → render Sections 1–4 inside a `RefreshIndicator`.
4. **Pull-to-refresh** → `ref.invalidate(progressOverviewProvider)` then `await ref.read(provider.future)`.
5. **Tab re-entry** → `autoDispose` refetches naturally (cheap, uncached, always fresh).
6. **Taps:** lift rows = none (Phase 1); PR teaser rows = none (display-only); ring/heatmap = none. (All drill-downs are Phase 2.)

## 5. States (all of them)

| State | Condition | What renders |
|---|---|---|
| **Loading** | request in flight | `GbSkeletonList(count: 4)` |
| **Error** | request failed | `ErrorRetry(message, onRetry: invalidate)` |
| **New user** | no completed sessions ever | First-run hero: *"Start your first session to begin tracking"*; no empty cards below |
| **No active plan** | `HasActivePlan == false` | **Hide the ring**; show raw *"3 sessions this week"*; consistency still renders; `ConsistencyPct` hidden |
| **Lift < 4 points** | lift omitted from `TopLifts` by the gate | Lift simply doesn't appear; if `TopLifts` empty → strength card shows *"Log a few working sets to see your strength trend"* |
| **No PRs** | `RecentPrs` empty | PR section shows *"Your PRs will appear here"* (quiet, not an error) |
| **Slipping lift** | `Direction == down` | Red tag is allowed **here only** — it's an honest per-lift signal, never applied to the whole page |

Empty states are deliberate (no `0/0` rings, no lines through noise) — the honest absence is the design.

## 6. Refresh & freshness

- Always live (uncached); pull-to-refresh + `autoDispose` cover staleness. A just-finished session appears on next
  tab entry. **Optional nicety (not required for Phase 1):** invalidate `progressOverviewProvider` after a session
  completes if a completion signal is available client-side.

## 7. Performance expectations

- **Server:** p95 < 350 ms (API-CONTRACTS §1). No N+1; bounded to top-3 lifts and a 12-week window.
- **Client:** first paint = skeleton immediately; one round-trip to data. Sparklines/heatmap are cheap `CustomPaint`.
  No jank — no chart library, no heavy layout.
- **Payload:** small (≤ ~3 lifts × ~8 points + ≤ ~36 active days + 3 PRs).

## 8. Build notes

**Backend** (`Modules.WorkoutSession`)
- New `GetMyProgressOverviewQuery` + handler + the DTOs in `Application/DTOs/PersonalTrainingDtos.cs`.
- Reuse `IWorkoutSessionRepository.QueryOwnAcrossGyms(currentUser.UserId)`; internal mediator call to
  `GetMyPersonalRecordsQuery` for the teaser; a goal lookup against `Modules.WorkoutPlan` (D1).
- New `MeController` action `GET progress/overview`; add the auth exemption entry. No migration, no cache.

**Frontend** (`gymbroapp`)
- New `progress_models.dart` (hand-written DTOs, `core/utils/json.dart` coercers), `progressRepositoryProvider`,
  `progressOverviewProvider`.
- Rebuild `features/progress/progress_screen.dart` with `GbCard`/`GbRing`/`GbSectionTitle`/`GbTappableRow`/
  `GbIconTile`/`Eyebrow`; tokens via `context.gb` + `AppSpacing`/`AppText`. New `_Sparkline`/`_Heatmap`
  `CustomPaint` private widgets. Replace the vanity `GbStatTile` row + 2-bar chart.
- No routing change (Progress tab already exists). No new dependency.

See [IMPLEMENTATION.md](IMPLEMENTATION.md) for the task checklist, tests, acceptance criteria, and rollout.
