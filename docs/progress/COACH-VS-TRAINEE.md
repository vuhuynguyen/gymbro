# Coach vs Trainee — Two Surfaces, Two Data Paths

The Progress page is **one redesign with two readers**. A trainee asks *"am I stronger, am I on track?"* — self-reflection across every gym they belong to. A coach asks *"who do I message today, and what do I change?"* — triage and management-by-exception inside the one gym they own. Same metric *catalogue*, opposite *jobs*, and — the load-bearing distinction — **opposite data-access paths that must never be crossed**.

**Related:** [Metrics catalogue](METRICS-CATALOGUE.md) · [Priority matrix](PRIORITY-MATRIX.md) · [Trainee dashboard IA](MOBILE-DASHBOARD.md) · [Drill-down pages](DRILL-DOWNS.md) · [Visualization map](VISUALIZATION.md) · [Feasibility audit](FEASIBILITY.md) · upstream: [PERMISSIONS.md](../PERMISSIONS.md) · [DATABASE.md](../DATABASE.md)

---

## 1. The dimension table

| Dimension | TRAINEE | COACH |
|---|---|---|
| **Core job** | Self-reflection — *am I stronger / on track?* | Triage / management-by-exception — *who needs me, what do I change?* |
| **Data path** | Self-scoped `/api/me/*` (`MeController`), `QueryOwnAcrossGyms(UserId)`, **no `X-Tenant-Id`** | Tenant-scoped (`X-Tenant-Id` **required**), gated by `WorkoutLogViewAll`, bounded to the coach's own gym via `ResourceAccessGuard` |
| **EF tenant filter** | **OFF** (deliberate cross-gym bypass) | **ON** (normal tenant-scoped query, `ws.TenantId = activeTenant`) |
| **Scope of truth** | Unified personal history across **every** gym | **One gym only** — cross-gym training is invisible by design (caption *"this gym only"*) |
| **Subject** | Always `self` (`currentUser.UserId`) | Always *another* user, by `traineeId`, who **must** be a member of the active tenant |
| **Body metrics** (weight/sleep) | Reads **own** `MetricEntry` via `/api/me/nutrition/metrics` | **No endpoint exists** — `MetricEntry` is not `ITenantEntity`; private/self-scoped by design |
| **Week anchoring** | Monday-anchored in the **trainee's** `ClientTimezone` | Same — bucket in the **client's** zone, never the coach's |
| **Authorization** | `WorkoutLogViewOwn` (implicit self) | `WorkoutLogViewAll` + membership-validated middleware + handler row-level check |
| **Entry surface** | Progress tab (Log · Plan · **Progress** · Profile) | Client roster → per-client detail |

The two rows that decide every other behaviour are **Data path** and **EF tenant filter**. They are an architectural constraint, not a preference — see [§4](#4-the-isolation-trap-r2) for the exact failure mode if they're crossed.

---

## 2. Coach multi-client triage (the roster — this *is* the coach home)

A busy coach opens this view to answer **one** question: *which client do I message today?* So the roster leads with the verdict, sorted **at-risk first** — management by exception, not a wall of green.

### Row anatomy

Each row is a single client, and every element on it earns its place by driving the "message / don't message" decision:

| Element | Source (tenant-scoped) | Decision it enables |
|---|---|---|
| **Status chip** — `On track` / `Drifting` / `Quiet` / `Stalled` | composite (below) | The whole verdict in one glance — who to open first |
| **Frequency mini-ring** — `done/goal` | completed sessions this ISO week ÷ `PlanAssignment.FrequencyDaysPerWeek` | *Is the plan being run as written?* |
| **Last-active** — relative ("3d ago") | `MAX(WorkoutSession.StartedAt)`, tenant-scoped | Leading churn signal — re-engage before they quit |
| **Client name** | membership graph | Identify / open |

### The needs-attention verdict

The chip is a triage classifier, computed per client:

| Chip | Condition | Coach reads it as |
|---|---|---|
| **Quiet** | no completed session in **N days** (`now − MAX(StartedAt)`) | At risk of churning — re-engage today |
| **Drifting** | weekly adherence **below band** (default 75–90%) | Slipping off plan — nudge or re-program |
| **Stalled** | a key lift's e1RM flat ≥ K exposures (see caveat) | Programming problem — deload / swap / change scheme |
| **On track** | none of the above | Skip — spend your attention elsewhere |

**Roster cost discipline (audit R6).** The chip is computed from **cheap signals only at list level** — last-active gap + adherence band. The **`Stalled` leg is NOT computed per roster row**: a per-lift e1RM stall across the whole roster is an *N-clients × M-lifts* fan-out that walks every working set of every member on each load. `Stalled` is resolved **lazily, when the coach opens a client** (or later via a precomputed `lastE1rmAdvanceAt`-style read model — a migration, deferred). The roster row promises only *Quiet* and *Drifting* until a client is opened.

**Scope caption is mandatory.** The roster carries a literal *"this gym only"* caption. A client who trains 5×/week split across two gyms can read *Quiet* here — that is **correct**: the coach's gym genuinely saw no sessions. Cross-gym work is invisible by design; the caption prevents the coach from misreading silence as inactivity.

---

## 3. Coach per-client detail (lead with the verdict, not a chart)

Tapping a roster row opens the client. The detail page **leads with the verdict**, then supports it — a coach should know the recommendation before any axis loads.

| # | Section | Source | Decision it enables |
|---|---|---|---|
| 1 | **Verdict header** — needs-attention chip + adherence ring + 6–8 wk compliance trendline | completed ÷ `FrequencyDaysPerWeek` per ISO week, tenant-scoped | Re-program vs. progress vs. leave alone |
| 2 | **Per-lift e1RM sparklines + stall badges** | `MAX(EstimatedOneRepMaxKg)` over `Working` sets per session, **tenant-scoped** (own gym) | *Which lift* to intervene on — the programming decision |
| 3 | **Acute vs. chronic load** — two **separate** bars + soft "ramping fast / fading" label | 7-day vol sum vs. 28-day avg, tenant-scoped | Add/cut volume; spot a too-fast ramp |
| 4 | **Per-lift PR detail** — the actual lift, not a count | session-detail per-lift e1RM PR | *"Hit a 5 kg deadlift PR Tuesday"* — concrete praise + push signal |
| 5 | **Assignment card** — version status + Apply-latest / pause / resume | `PlanAssignment.{PlanVersion, IsActive}` + newer-published check | Is the client on the current plan version? |

### Hard rules for the per-client detail

- **Delete the hardcoded `—` body-data card.** There is **no coach endpoint** for another user's `MetricEntry` (weight/sleep) — it is private and self-scoped. An apologetic placeholder reads as *broken*; absence is the honest design. Remove the tile, do not render an empty one.
- **Stall and PR come from the e1RM series / `/api/me/records` logic, never from `PrCount` (audit R4).** `WorkoutSession.PrCount` is per-session (not per-lift) and **0 on Abandoned** sessions. It is valid **only** as an in-session celebration count — never as a progress, stall, or "did they PR" input on this page.
- **Acute-vs-chronic is volume-based by default (audit R10).** `RpeOverall` is integer-only and frequently null, and `DurationSeconds` is wall-clock (includes rest). Show **acute and chronic as separate bars** with a soft nudge — **never the ACWR ratio as an injury predictor**, and only offer the RPE-weighted load when RPE coverage is high.
- **Compute lazily.** Per-lift e1RM and stall are resolved when this page opens for *this* client — not pre-warmed across the roster.

---

## 4. The isolation trap (R2) — the single most dangerous item

The trainee e1RM-series query uses `QueryOwnAcrossGyms(UserId)`, which **deliberately turns the EF tenant filter OFF** so a trainee sees every gym at once. If the coach's per-client series **reuses that path keyed by a `traineeId`**, it returns that client's sessions from **every gym they belong to** — a cross-gym data leak that violates the documented *"coach sees own gym only"* boundary ([PERMISSIONS.md](../PERMISSIONS.md), `ListSessionsHandler` + `ResourceAccessGuard`).

**The rule:**

- **Trainee reads** — self path, filter **OFF**, subject is always `currentUser.UserId`. Never parameterized by someone else's id.
- **Coach reads** — a **separate handler**, filter **ON** (`ws.TenantId = activeTenant`), gated by `WorkoutLogViewAll`, with `ResourceAccessGuard` confirming `traineeId` is a member of the active tenant. On a non-member id: **403 / 404 — never silently rescope to self.**

The two paths share *formulas* (e1RM, adherence, stall) but **must not share a handler**. The shared logic lives in a scope-agnostic calculator that takes an already-scoped session set; the scoping is decided by the entry handler, not the math.

> Coach caches must include `tenantId` in the key (`coach:client:{tenantId}:{traineeId}`). A tenant-less coach cache key would leak one gym's result into another gym's view — the same isolation rule, one layer up.

---

## 5. Who-sees-what

`✓` = visible · `self` = own data only · `gym` = own-gym clients only · `✗` = no path exists.

| Metric / surface | Trainee (self) | Coach (own-gym client) | Why the difference |
|---|:---:|:---:|---|
| Weekly frequency adherence ring | ✓ self, all gyms | ✓ gym | Same formula; coach denominator is the client's in-gym assignment goal |
| Per-lift e1RM trend + sparkline | ✓ self, all gyms | ✓ gym | **Separate handler** per [§4](#4-the-isolation-trap-r2); coach filter ON |
| e1RM stall flag | ✓ self | ✓ gym (lazy, on open) | Roster shows it only after opening the client (R6) |
| Per-lift PR detail / timeline | ✓ self (`/api/me/records`) | ✓ gym (session-detail) | Coach reads the lift, never `PrCount` (R4) |
| Consistency heatmap + forgiving % | ✓ self | — | Trainee habit story; coach uses the compliance trendline instead |
| Weekly volume trend | ✓ self | ✓ gym | Supporting context both sides; never the headline |
| Acute vs. chronic load | — | ✓ gym | A coach workload-ramp call; not a trainee glance metric |
| Assignment status / Apply-latest | view own plan | ✓ gym (act on it) | Trainee sees the plan; coach owns version control |
| Bodyweight / sleep trend | self (needs range endpoint) | **✗** | **No coach endpoint** — `MetricEntry` is private/self-scoped by design |
| Cross-gym totals | ✓ (that's the point) | **✗** | Coach is bounded to own gym; cross-gym is invisible |
| Roster triage chip | — | ✓ gym | The coach home; trainee has no roster |

---

## 6. What the coach view must NOT do

A short list of failure modes, each a direct consequence of the constraints above:

- **Don't duplicate the trainee's thin volume chart as the coach headline.** The coach headline is the **adherence verdict + per-lift stall**, not a volume bar (volume is lifting-only and reads as regression for a calisthenics/cardio client — audit R7).
- **Don't imply cross-gym totals.** Own-gym only; caption it.
- **Don't show ACWR as an injury predictor.** Separate bars, soft nudge (R10).
- **Don't render body-metric placeholders.** No coach `MetricEntry` path exists — delete the card.
- **Don't fabricate periodization / deload recommendations.** GymBro is descriptive by design — there is no progression/deload/periodization engine. The page surfaces honest trends; **prescription stays with the human coach.**
- **Don't reuse `QueryOwnAcrossGyms` for any coach read** (R2). This is the leak.

---

## Open questions

1. **Coach trend depth vs. the 20-session page cap (audit R5).** The coach client panel is fed by `ListSessionsHandler` at `pageSize: 20`. A 4–6×/week client exceeds 20 sessions in ~3–5 weeks, so an *"8-week compliance trend"* and the *28-day chronic* leg of acute-vs-chronic silently truncate if they piggyback that page. The coach progress endpoints should get their **own date-windowed / weekly-rollup query** (`from`/`to` bounds, server-side weekly aggregation) rather than client-side grouping of 20 rows. Flagged as a sequencing decision, not a contradiction of the spec.
2. **Multi-assignment adherence denominator (audit R1).** `FrequencyDaysPerWeek` lives on `PlanAssignment`; a client may hold multiple active assignments. The coach roster is **own-gym**, so it should bind to the **active in-gym assignment** (tie-broken by latest `StartDate`) — but this should be stated explicitly server-side rather than re-derived per client, to avoid two surfaces disagreeing on the goal.
