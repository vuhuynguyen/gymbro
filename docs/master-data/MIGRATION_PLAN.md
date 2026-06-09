# Architecture Review & Migration Plan

> **Phase covered:** 8 (audit the current Exercise module; produce an enterprise migration plan). **No code
> here** — this is the phased, reversible path from today's module to the target in
> [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md).

---

## 1. Current-state audit (as-built)

The Exercise module is clean and well-factored for *strength/conditioning logging*, but thin for
*master-data at commercial scale*. Findings are file-cited.

### 1.1 The `Exercise` aggregate

`Exercise` (`Modules.Exercise/Entities/Exercise.cs:10`) — `AggregateRoot`, `ISharedEntity` (TenantId null =
global, else tenant-scoped), `ISoftDelete`. Fields: `DefaultName`, `DefaultDescription`, `Type`,
`TrackingType` (`Exercise.cs:17`, shared kernel), `MovementType`, `Difficulty`, `Equipment`,
`EstimatedCaloriesBurn?`, `AverageDurationSeconds?`, `ImageUrl`. Child collections: `Muscles`, `Instructions`,
`Tags`, `Media`, `Warnings`.

### 1.2 The enums — the core limitation

`Modules.Exercise/Entities/MuscleGroup.cs` defines all classification enums, and they are **coarse**:

- `MuscleGroup`: **6 values** — Chest, Back, Legs, Shoulders, Arms, Core. *(Hevy ships 20.)*
- `ExerciseType`: Strength, Cardio, Mobility, Stretching.
- `MovementType`: **Compound, Isolation only** — no force, no plane, no movement pattern, no kinetic chain.
- `Equipment`: **5 values** — Bodyweight, Dumbbell, Barbell, Machine, ResistanceBand. *(Hevy ships 9; no
  cable/kettlebell/smith/etc.)*
- `DifficultyLevel`: Beginner, Intermediate, Advanced.

### 1.3 Related entities

- `ExerciseMuscle` (`ExerciseMuscle.cs`): `{ Muscle: MuscleGroup, IsPrimary: bool }` — **boolean role, no
  contribution weight, no stabilizer/synergist distinction.** Unique `(ExerciseId, Muscle)`. Domain enforces
  ≥1 muscle + ≥1 primary.
- `ExerciseInstruction`: ordered `{ StepOrder, Content(≤1000) }` — **flat steps; no setup/breathing/tempo/
  cues/mistakes structure.**
- `ExerciseMedia`: `{ Type: string "Image"|"Video", Url(≤500) }` — **single URL per asset, no renditions,
  posters, placeholders, durations, or license metadata.**
- `ExerciseTag`: free-text keywords (case-insensitive dedupe).
- `ExerciseWarning`: free-text safety note (≤500) — **no structured contraindication/spotter model.**

### 1.4 Master data, persistence, API

- **Seed:** `WebApi/Composition/ExerciseCatalogSeeder.cs` — **30 hardcoded exercises**, idempotent by
  `DefaultName`, via `Exercise.CreateGlobal(...)` (`Exercise.cs:43`).
- **DbContext:** `AppDbContext`; relevant migrations
  `20260329170956_Init_Exercise_With_Translation` → `20260406171752_Exercise_Profile_And_Muscles`
  (pivoted single muscle → `ExerciseMuscles`) → `20260606100751_Remove_Translation_Layer`
  (**a `Translations` table was created then removed — i.e. localization was attempted and dropped**) →
  `20260609050213_Exercise_TrackingType_And_Metrics`.
- **API:** `ExerciseController` CRUD (admin-only writes); `SearchExercisesQuery` filters by name (LIKE),
  muscle, type, movement type, difficulty, equipment + pagination; in-memory `ExerciseCatalogCache`.
- **Clients:** Angular `exercise.model.ts` (`ExerciseDto`/`ExerciseDetailDto`) and Flutter
  `exercise_models.dart` + `ExerciseTrackingType` enum mirror the API DTOs.
- **Denormalization:** `PerformedExercise` snapshots `ExerciseName` + `TrackingType` at log time so
  renames/deletes don't break history — **a strength to preserve.**

### 1.5 Gap summary (current → commercial bar)

| Capability | Today | Target | Gap severity |
|---|---|---|---|
| Muscle granularity | 6-enum + bool primary | ~20 muscle lookup + 5 roles + contribution weight | **High** |
| Classification axes | MovementType (compound/iso) only | + force, plane, movement pattern, kinetic chain, utility, laterality | **High** |
| Equipment | 5-enum | ~17 lookup + category + required/optional | Medium |
| Instructions | flat steps | structured (setup/exec/breathing/tempo/cues/mistakes/safety) | **High** |
| Localization | none (table removed) | `ExerciseTranslation` per BCP-47 + staleness | **High** |
| Aliases / search | none / LIKE-on-name | aliases + FTS + trigram fuzzy | **High** |
| Media | 1 URL, type=string | renditions, posters, placeholders, durations, **license** | **High** |
| Safety | free-text warning | structured contraindications/spotter | Medium |
| Programming data | none | per-goal rep/%1RM/rest/RPE | Medium |
| Analytics (MET) | single nullable calorie int | MET light/vigorous + formula | Medium |
| Relationships | none | variation/progression/regression/alternative graph | **High** (AI) |
| Provenance/license | none | `Source` + `LicenseCode` per row + per media | **High** (legal) |
| Scale | 30 seeded | 5,000–10,000+ via ETL | **High** |

### 1.6 Performance & normalization concerns at scale

- `LIKE '%name%'` search won't scale to 5–10k × multi-language and can't match aliases → needs FTS + trigram
  ([MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §10).
- In-memory `ExerciseCatalogCache` is fine single-instance but must be revisited for multi-instance (cache
  invalidation across nodes) — aligns with the existing memory-provider/Redis fallback work
  ([[gymbro-cache-memory-provider-di-bug]]).
- Coarse enums baked into the DB as `int` conversions (`ExerciseConfiguration`) make adding values cheap, but
  **replacing** `MuscleGroup`/`Equipment` with lookups is a real migration (§3).
- No content versioning → no way to detect stale translations once localization lands.

---

## 2. Migration principles

1. **Additive first, destructive last.** Mirror the existing `TrackingType` migration philosophy — add
   columns/tables and backfill; drop old shapes only after every reader is cut over.
2. **Preserve the denormalization invariant.** `PerformedExercise.ExerciseName/TrackingType` snapshots must
   keep working throughout — never break logged history.
3. **Two migration chains exist — run both** (`gymbro/CLAUDE.md`, [DATABASE.md](../DATABASE.md)); these
   changes are on `AppDbContext`. Use `dotnet ef` tooling, never hand-write ([[gymbro-ef-migrations-workflow]]).
4. **Every step is independently shippable and reversible** (a `Down()` exists; feature-flag readers).
5. **Result<T> + FluentValidation + explicit DTO mapping** conventions hold throughout (`gymbro/CLAUDE.md`).
6. **Re-introduce localization deliberately** — the earlier `Translations` table was generic and got removed;
   the target `ExerciseTranslation` is exercise-specific and versioned (don't repeat the generic approach).

---

## 3. Phased migration

> Phases are ordered by dependency and risk. Each is a separate EF migration + PR. Clients
> (Angular/Flutter) update DTOs only when a phase exposes new API fields.

### Phase A — Provenance & versioning (foundation, low risk)

Add `Slug` (unique, backfilled from `DefaultName`), `Source`, `LicenseCode`, `ContentVersion`, `Status` to
`Exercise`. Additive, no reader breaks. Establishes the legal-clean guardrail and version anchor **before**
any bulk import. Backfill existing 30 as `Source=in-house`, `License=in-house`, `Status=Published`.

### Phase B — Classification axes (additive enums)

Add `MovementPattern`, `ForceType`, `PlaneOfMotion`, `KineticChain`, `Utility`, `Laterality`,
`PrimaryBodyRegion` to `Exercise` (nullable initially). Backfill the 30 seeded exercises by hand (they're
known lifts). Keep existing `MovementType` until §G. Expose read-only in DTOs; clients add optional fields.

### Phase C — Muscle model upgrade (the big structural one)

1. Introduce `Muscle` lookup (~20) + map each to a coarse `MuscleGroup` (keeps existing 6-value filtering
   working).
2. Add `Role` (enum) + `ContributionWeight` to `ExerciseMuscle`; backfill `Role = IsPrimary ? PrimeMover :
   Synergist`, weights as a sensible default (e.g. primary 0.6, split remainder), flagged for review.
3. Keep `Muscle: MuscleGroup` column during transition; add `MuscleId` FK; dual-write; cut readers over;
   drop the enum column last. Preserve the unique-muscle + ≥1-primary domain invariants (now "≥1 PrimeMover").

This is the highest-value change (unlocks volume/recovery/AI) and the highest-effort — schedule it as its own
milestone with data review.

### Phase D — Structured instructions

Add structured instruction fields. **Do this together with localization (Phase E)** since both live on the
translation row — author the new structure directly in `ExerciseTranslation` rather than migrating the flat
`ExerciseInstruction` twice. Backfill: map existing flat steps → `Execution[]`, leave other fields empty
(flagged Draft until authored/reviewed).

### Phase E — Localization (`ExerciseTranslation`)

Create `ExerciseTranslation(ExerciseId, LanguageCode, Name, Aliases[], <structured instructions>, Keywords[],
SourceVersion, Status, ReviewedBy/At)` (PK `(ExerciseId, LanguageCode)`). Backfill `en` rows from
`DefaultName`/`DefaultDescription`/instructions. Then **deprecate** `DefaultName`/`DefaultDescription` on the
master row once readers use the canonical-locale translation (keep `Slug` as the language-neutral key).
Vietnamese + French follow via TMS ([[gymbro-mobile-design-system]] / portal i18n). Implements the staleness
gate (`SourceVersion < ContentVersion → Outdated`).

> **Note:** this re-introduces the localization the earlier migration removed — but exercise-specific and
> versioned, not the generic `Translations` table that was dropped.

### Phase F — Aliases & search

Aliases land in `ExerciseTranslation.Aliases[]` (Phase E). Add `ExerciseSearchDoc` (materialized
`tsvector` + `pg_trgm`) per locale, GIN-indexed; replace `LIKE`-on-name in `SearchExercisesHandler` with FTS +
trigram fuzzy + faceted filters. Closes the #1 search gap.

### Phase G — Equipment, media, safety, programming, analytics, relationships

- **Equipment:** `Equipment` lookup (~17) + `EquipmentCategory` + `ExerciseEquipment(Requirement)`; migrate
  the 5-enum to lookup rows; drop the enum last.
- **Media:** expand `ExerciseMedia` to the rendition/poster/placeholder/duration/**license** model
  ([MEDIA_STRATEGY.md](MEDIA_STRATEGY.md) §8); migrate existing `ImageUrl`/media rows to `AssetKey` after the
  media pipeline exists.
- **Safety:** `ExerciseSafety` (spotter/contraindications); migrate free-text `ExerciseWarning` → structured +
  notes.
- **Programming/analytics:** `ExerciseProgramming` per goal; `MetLight`/`MetVigorous`; deprecate the single
  `EstimatedCaloriesBurn`.
- **Relationships:** `ExerciseRelationship` graph.
- **Cleanup:** retire `MovementType` (subsumed by Mechanics) and any dual-written legacy columns.

### Phase H — Bulk import & scale-up

Only now (model complete, guardrails live) run the ETL in [IMPORT_PIPELINE.md](IMPORT_PIPELINE.md) to grow
from 30 → thousands. The 30 hand-curated exercises become the quality-reference set.

---

## 4. Risk register

| Risk | Mitigation |
|---|---|
| Muscle/equipment enum→lookup breaks filters/clients | Dual-write + keep coarse `MuscleGroup` mapping; cut readers over behind a flag; drop last |
| Breaking logged history | `PerformedExercise` snapshots are independent; never FK-cascade into history |
| Re-introducing localization repeats the removed generic table | Use exercise-specific, versioned `ExerciseTranslation`, not a generic `Translations` table |
| Two migration chains drift | Run both; CI gates on migrations ([[gymbro-live-deployment]]) |
| Bulk import contaminates with copyleft data | `LicenseCode` quarantine guardrail (Phase A) blocks bad rows before import (Phase H) |
| ImageSharp commercial license | Decide ImageSharp-vs-NetVips before the media pipeline (Phase G) — [MEDIA_STRATEGY.md](MEDIA_STRATEGY.md) §7 |
| Scope/time | Phases A/B/F are quick wins; C/E/G are milestones — ship incrementally, each adds value alone |

---

## 5. Sequencing summary

```
A Provenance+version ─┐
B Classification axes ─┼─ (independent, quick) ── ship early
F Search (needs E for aliases) ─────────────────┐
C Muscle model (milestone) ─────────────────────┤
D Structured instructions ─┐                     │
E Localization ────────────┴─ (do D+E together) ─┤
G Equipment/media/safety/programming/relationships┤
H Bulk import (after model complete + guardrails)─┘
```

Each phase is a `dotnet ef` migration + PR, additive-first, reversible, with clients updated only when new API
fields are exposed. No code is written until this plan is approved.
