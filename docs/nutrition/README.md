# GymBro Nutrition Tracking — Discovery & Architecture Proposal

> **Status:** Architecture proposal + **Phase 1–3 implementation in progress.** These documents define the
> full forward-looking design; the **as-built status** below records what is actually implemented today (a
> subset). This mirrors the [`master-data/`](../master-data/) precedent — design first, then build.

> **Implementation status (Phase 1–3, as built).** The backend **domain model + persistence** are
> implemented to the existing GymBro conventions and the full test/convention suite is green:
> - **`Modules.Food`** — `Food` catalog aggregate (`ISharedEntity`, global + tenant-custom), admin CRUD +
>   tenant-custom create, search/get, and the cross-module `ResolveFoodSummariesQuery`/`ValidateFoodIdsQuery`
>   contracts. **Wired end-to-end** (controller, DI, validators) and **seeded**: a ~39-item starter catalog
>   (common foods + beverages + supplements) sourced from **USDA FoodData Central (public domain / CC0)** ships
>   as an embedded file with `--seed-foods` / `--reseed-foods` + Development auto-seed — see
>   [FOOD_SEEDING.md](FOOD_SEEDING.md). To match the *current* `Exercise` catalog (which carries no
>   slug/provenance), the proposal's slug, provenance/license columns, and the normalized
>   `FoodNutrient`/`FoodServing`/`FoodTranslation` tables remain **deferred** — headline macros are denormalized
>   onto the `Food` row for MVP.
> - **`Modules.Nutrition`** — `NutritionPlan` (versioned), `PlanMeal`/`PlanMealItem`,
>   `NutritionPlanAssignment`, `DailyNutritionLog` (snapshot-on-touch) + `LoggedItem`
>   (Planned/Completed/Skipped/Substituted/Missed, denormalized snapshot), `Close()` → `DailyLogClosedEvent`
>   via the outbox, and basic adherence. **Domain + EF persistence + migration AND the full application/API
>   layer are built**: coach plan CRUD + structure-replace (immutable versioning) + assignment create/list on
>   tenant-scoped `/api/nutrition/*`; the trainee daily-log surface on self-scoped `/api/me/nutrition/*`
>   (snapshot-on-touch `today` with lazy day-close, completion-first set-status / substitute / add-ad-hoc,
>   history); and the coach client-adherence read. The plan snapshot is serialized to `jsonb` at assign time
>   and the daily log seeds from it — the same snapshot+denormalize integrity as workout sessions.
>   **MVP boundary:** a daily log is anchored to a gym via the active assignment, so logging requires an
>   active nutrition assignment; assignment-less self-logging is a later enhancement.
> - **Deferred to later phases (unchanged from the proposal):** `MetricEntry`, offline sync, reminders, push,
>   advanced analytics/AI, plan archive/pause-resume/apply-latest, visibility redaction (the mode + flags are
>   stored but not yet redacted on read), DayApplicability training/rest-day filtering, and both **clients**
>   (Flutter/Angular).

This folder is the deliverable for **Meal, Supplement & Daily Nutrition Tracking** — the next major GymBro
feature. The brief: *not just another food-logging module*, but a foundation that makes daily logging
**extremely fast** while generating **rich long-term data** for adherence, coaching, and future analytics/AI.

## The one-paragraph thesis

GymBro already has a battle-tested triad for "a coach prescribes structured work, a trainee performs it day by
day, and the system measures planned-vs-actual": **`WorkoutPlan` (versioned template) → `PlanAssignment` (pins a
version to a trainee with a snapshot + visibility) → `WorkoutSession` (snapshots the plan, logs performed work,
denormalizes for durable history, rolls up read-model metrics via the outbox)**. Nutrition is the *same shape* —
a meal plan is prescribed, eaten daily, and measured for adherence — so the feature is overwhelmingly a **reuse
exercise, not a new paradigm**. We add two genuinely new capabilities the platform has never had — **reminders/
notifications** and **offline-first logging** — and one new master-data catalog (**Foods & Supplements**) that is
a direct structural sibling of the existing **Exercise** catalog.

## Documents

| Doc | Owns | Deliverables covered |
|---|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Recommended architecture, module boundaries, the major design decisions + alternatives | 2, 15 |
| [DOMAIN_MODEL.md](DOMAIN_MODEL.md) | Business model, domain model, aggregates, entity relationships, supplements, the extensibility spine | 3, 4, 5, 14 |
| [DATABASE.md](DATABASE.md) | Schema, markers, indexes, constraints, ownership, migrations | 6 |
| [API_AND_PERMISSIONS.md](API_AND_PERMISSIONS.md) | Endpoints, contracts, `/api/me` vs tenant-scoped, permissions, tenancy, wire format | 7, security |
| [CLIENT_UX.md](CLIENT_UX.md) | UI/UX flow, navigation, state, reusable components (Flutter + Angular) | 8, reusable components |
| [REMINDERS_AND_OFFLINE.md](REMINDERS_AND_OFFLINE.md) | Reminder/recurrence strategy, notification strategy, offline sync | 9, 10, scheduling |
| [ANALYTICS_AND_COACHING.md](ANALYTICS_AND_COACHING.md) | Reporting, analytics opportunities, coach/client workflow | 11, 12, 13 |
| [FOOD_SEEDING.md](FOOD_SEEDING.md) | **Implemented** — the USDA-sourced food catalog seed (run, sourcing, data safety) | — |
| [ROADMAP.md](ROADMAP.md) | Risks & trade-offs, future extensibility, phased implementation roadmap | 14, 15, 16 |

**How to read:** ARCHITECTURE establishes the shape and the load-bearing decisions; DOMAIN_MODEL is the single
most important document (the data is the moat); the rest are supporting subsystems. Every major decision carries a
**Why / How it aligns / Alternatives / Why preferred** block, per the brief.

## Executive summary

### What we are building (MVP scope)

A trainee opens GymBro and sees **today's nutrition** as a checklist of planned meals and supplements with their
prescribed times. Each item is one tap to **complete**, **skip**, or **swap**. Off-plan eating is logged as a
**one-off** in the same two taps. A reminder fires before each scheduled item. At day's end the trainee sees a
single **adherence ring** (meals hit / planned). A coach prescribes the plan once and watches each client's
**daily completion + streak** roll up — exactly as they already watch workout volume and PR counts.

The MVP deliberately tracks **completion, not calories**: "did you eat the planned meal?" is the fast, high-signal
daily question. Macros/calories ride along **invisibly** (denormalized from the food catalog at log time) so the
historical data is rich from day one — the analytics, macro dashboards, and AI insights of later phases read data
that was captured but never demanded of the user.

### Headline recommendations

1. **Two new bounded contexts, mirroring the existing split.** `Modules.Food` (the global Food/Supplement
   catalog — sibling of `Modules.Exercise`) and `Modules.Nutrition` (plan + daily routine + daily log — the
   `WorkoutPlan`+`WorkoutSession` analogue). Logging references foods only through MediatR contracts + `FoodId`
   FKs, honouring the build-enforced module-boundary rule. *(See [ARCHITECTURE.md](ARCHITECTURE.md) §2.)*

2. **Reuse the plan→assignment→snapshot→log spine verbatim.** `NutritionPlan` (versioned `TemplateId`+`Version`),
   `NutritionPlanAssignment` (pins a version, stores `SnapshotJson`, carries visibility flags), and a
   per-date **`DailyNutritionLog`** that snapshots the *applicable* planned day on first touch — exactly as
   `WorkoutSession.Start` snapshots the planned workout. No new persistence philosophy. *(DOMAIN_MODEL §3–5.)*

3. **Log completion first; capture nutrition silently.** The fast path is *check off the planned item*. Each
   `LoggedItem` **denormalizes the food's name + per-serving nutrition at log time** (the exact `ExerciseName`+
   `TrackingType` durability pattern), so renaming/retiring a food never rewrites history and macro analytics
   are available later with zero extra user effort. *(DOMAIN_MODEL §6.)*

4. **A flexible measurement spine for "think beyond logging."** A single additive `MetricEntry` series
   (`MetricType` lookup → value/unit/note/photo) absorbs body weight, body fat, water, fiber, sleep, energy,
   digestion, mood, custom notes, photos, and future wearable/AI signals **without schema churn** — the same
   "additive over destructive" principle the master-data architecture is built on. *(DOMAIN_MODEL §8.)*

5. **Offline-first for logging — a deliberate, justified divergence from the workout module.** Workout logging is
   online-only because it is real-time, coach-monitored, and one-session-at-a-time. Nutrition logging is
   high-frequency, all-day, and happens in low-signal places (kitchen, commute, gym floor). We introduce a
   **local persisted mutation queue with client-generated GUIDs + idempotent server upserts** in the Flutter
   client. The web portal stays online-first. *(REMINDERS_AND_OFFLINE §3.)*

6. **Reminders are client-local-first; server push is a later phase.** The daily schedule is *deterministic and
   known on-device*, so MVP reminders are scheduled **local notifications** (no new server infrastructure, works
   offline, privacy-preserving). Server-driven push (FCM/APNs/Web-Push + a timezone-aware dispatch hosted service
   + a device-token table) is fully designed but **deferred** to the coach-nudge / cross-device phase.
   *(REMINDERS_AND_OFFLINE §1–2.)*

7. **Nutrition is personal — lean on the `/api/me/*` precedent.** A person eats once a day regardless of which
   gym they belong to, so trainee-facing reads/writes use the **self-scoped, cross-gym** `/api/me/nutrition/*`
   surface (the sanctioned tenant-filter bypass), while the **coach** monitors a gym's clients through the
   tenant-scoped `/api/nutrition/*` surface. This is the identical dual-surface model the session feature already
   ships. *(API_AND_PERMISSIONS §3.)*

8. **Source the food catalog legally-clean, exactly as the exercise catalog is.** Seed from **USDA FoodData
   Central** (US-Government public domain) for the core catalog; treat **Open Food Facts** (ODbL — a share-alike
   /copyleft regime, the food analogue of the exercise catalog's CC-BY-SA quarantine) as a *separate, attributed*
   source, never blended into the owned master table. Provenance + license travel with every row.
   *(DOMAIN_MODEL §7; reuses the [master-data](../master-data/DATA_SOURCE_COMPARISON.md) discipline. Licensing
   specifics must be re-verified at implementation time — see the integrity note below.)*

### Reuse at a glance — nothing here is invented from scratch

| New nutrition concept | Existing GymBro pattern it mirrors | Source of truth |
|---|---|---|
| `Modules.Food` catalog (global + tenant-custom, admin-write, cached) | `Modules.Exercise` (`ISharedEntity`, `IPlatformAdminRequest`, distributed cache) | [ARCHITECTURE.md](../ARCHITECTURE.md) |
| Food master data (nutrients, translations, provenance, search) | The `master-data/` exercise model (translations, `ContentVersion`, FTS, licensing) | [master-data/](../master-data/) |
| `NutritionPlan` (versioned `TemplateId`+`Version`, immutable edits) | `WorkoutPlan` lifecycle | [BUSINESS_RULES.md](../BUSINESS_RULES.md) |
| `NutritionPlanAssignment` (pin version, snapshot, visibility flags) | `PlanAssignment` | [BUSINESS_RULES.md](../BUSINESS_RULES.md) |
| `DailyNutritionLog` (snapshot-on-touch, denormalized history, status per item) | `WorkoutSession` (snapshot-on-start, `PerformedExercise`/`PerformedSet`, status) | [BUSINESS_RULES.md](../BUSINESS_RULES.md) |
| Per-item `Completed`/`Skipped`/`Substituted`/`Missed` | `ExercisePerformStatus` + substitution provenance | [DATABASE.md](../DATABASE.md) |
| Adherence/streak read-model finalized via outbox event | `PrCount` + `SessionCompletedEvent` + `OutboxProcessor` | [DATABASE.md](../DATABASE.md) |
| `MealGroupId` / substitution / one-off vs planned | `SupersetGroupId` / `SubstitutedFromExerciseId` / `Adhoc` source | [BUSINESS_RULES.md](../BUSINESS_RULES.md) |
| Trainee cross-gym personal views | `/api/me/sessions`, `/api/me/progress` (`QueryOwnAcrossGyms`) | [PERMISSIONS.md](../PERMISSIONS.md) |
| New permissions (`NutritionPlan*`, `NutritionLog*`) | The `Plan*` / `WorkoutLog*` permission family | [PERMISSIONS.md](../PERMISSIONS.md) |
| Flutter: Riverpod providers, tolerant enums, pure-Dart metrics, `StatefulShellRoute` tab | The session/log feature in `gymbroapp` | [gymbroapp ARCHITECTURE](../../../gymbroapp/docs/ARCHITECTURE.md) |
| Angular: signal service, `shared/ui/` wrappers, `computed()` week grouping | `SessionService` + the Workout-Log timeline | [Portal ARCHITECTURE](../../../GymBroPortal/docs/ARCHITECTURE.md) |

### What is genuinely new (and therefore where the risk is)

| New capability | Why GymBro has never needed it | Where designed |
|---|---|---|
| **Recurring daily schedule** (meals repeat by time-of-day, weekday, training/rest day) | Workouts have only a `frequencyDaysPerWeek` integer — no recurrence engine | [REMINDERS_AND_OFFLINE §1](REMINDERS_AND_OFFLINE.md) |
| **Reminders / notifications** | No local-notification, push, or service-worker code exists in *any* repo today | [REMINDERS_AND_OFFLINE §1–2](REMINDERS_AND_OFFLINE.md) |
| **Offline-first logging** | The Flutter app is online-only (no `drift`/`isar`/`sqflite`); workouts are real-time | [REMINDERS_AND_OFFLINE §3](REMINDERS_AND_OFFLINE.md) |
| **Food/Supplement master data + nutrient model** | Distinct catalog, distinct licensing, distinct nutrient axes | [DOMAIN_MODEL §6–7](DOMAIN_MODEL.md) |

## A note on sourcing integrity

Per the platform's documentation discipline (see the [master-data integrity note](../master-data/README.md)),
**no nutrition science or licensing claim in these documents should ship unverified.** Calorie/macro reference
values, the Mifflin-St Jeor / Katch-McArdle BMR formulae, the Atwater factors (4/4/9 kcal per g), and the exact
licensing terms of USDA FDC and Open Food Facts are cited where used and **must be confirmed against the
authoritative source before implementation**. This proposal models *structure and approach*; it does not seed
data.
