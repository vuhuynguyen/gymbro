# Catalog Seeding — Exercises & Foods

How the two **global catalogs** (the exercise library and the food/supplement catalog) are populated from
structured data, and how to clear and refresh them safely.

> **Status: implemented and verified** for both catalogs. This is the as-built behaviour. The richer,
> not-yet-built master-data/nutrition model these seeds are a first step toward is in
> [ROADMAP.md](ROADMAP.md).

Both catalogs follow the **same layered pipeline** — an embedded-resource **loader** → fail-fast **validator**
→ domain **factory** → DB-touching **seeder** — and the **same data-safety guarantees**: FK-`Restrict` on every
reference plus **soft-delete-not-hard-delete**, so a reseed can never destroy user data (logged history, plans,
or daily logs). The two catalogs are independent (different modules, different JSON files, different CLI flags)
but structurally identical, so understanding one is understanding both.

| Catalog | Module | Seed file (embedded) | CLI flags |
|---|---|---|---|
| **Exercises** | `Modules.Exercise` | `Infrastructure/SeedData/exercises.json` | `--seed-exercises` / `--reseed-exercises` |
| **Foods** | `Modules.Food` | `Infrastructure/SeedData/foods.json` | `--seed-foods` / `--reseed-foods` |

**Seed data lives in files, never hardcoded in C#.** Each file ships as an **embedded resource** in its module's
`.csproj`, so seeding works identically in local dev and in containers — there is no working-directory or
content-file dependency. Both CLI entrypoints (and the Development startup auto-seed) verify EF migrations first
(run `dotnet ef database update`, or set `Database:AutoMigrate=true`, if the DB isn't migrated), then exit.

---

## Exercise catalog

The current library seeds **149 exercises** across **13 categories**, **6 muscle groups**, and **5 equipment
codes** — the authoritative count is the entry count of
[`exercises.json`](../Modules/Modules.Exercise/Infrastructure/SeedData/exercises.json). The catalog includes a
broad common-movement set: full cardio (bikes, elliptical, stair climber, treadmill, rower, swim, ski-erg, plus
HIIT drills — jumping jacks, mountain climbers, high knees, box jumps, battle ropes), and common
strength/bodyweight lifts across every muscle group.

### Where the seed files live

```
Modules/Modules.Exercise/Infrastructure/SeedData/
  exercises.json     # the exercise library (the data)
  muscles.json       # valid MuscleGroup codes (validation lookup)
  equipment.json     # valid Equipment codes (validation lookup)
  categories.json    # valid library categories (validation lookup)
```

### The seeding code (layered service)

| Type | Project | Responsibility |
|---|---|---|
| `ExerciseSeedDataLoader` | `Modules.Exercise` (`Infrastructure/Seeding`) | Read + deserialize the embedded JSON. Pure I/O, no DB. |
| `ExerciseSeedDataValidator` | `Modules.Exercise` | Validate the whole set; collect **all** errors; fail fast. Pure, no DB. |
| `ExerciseSeedFactory` | `Modules.Exercise` | Map a validated entry onto the `Exercise` aggregate via the domain factory only. |
| `ExerciseMasterDataSeeder` | `WebApi` (`Composition`) | Orchestrate load → validate → apply in one atomic `SaveChanges` → invalidate cache. The only DB-touching piece. |

The pure pieces live in the Exercise module (no `AppDbContext` dependency) and are unit-tested in
`Tests/Seeding/ExerciseSeedDataTests.cs`. The legacy hardcoded `ExerciseCatalogSeeder.cs` has been removed.

### What each field maps to

**Persisted now:** `name`→DefaultName, `description`→DefaultDescription, `type`, `trackingType`,
`primaryMuscle`/`secondaryMuscles`→ExerciseMuscle (IsPrimary), `equipment`, `difficulty`,
`mechanics`→MovementType, `estimatedCalories`, `averageDurationSeconds`, `instructions`→ExerciseInstruction,
`safetyNotes`→ExerciseWarning, and `tags` (merged with `category`/`movementPattern`/`forceType`/
`equipmentDetail`, slugified) →ExerciseTag.

**Preserved in the file but NOT yet persisted as structured columns:** `slug`, `aliases`, `commonMistakes`, and
the structured `category`/`movementPattern`/`forceType`/`equipmentDetail` (the last four are folded into search
tags so the catalog stays searchable by them). These are authored so the data is production-quality and
forward-compatible; the schema upgrade that persists them is future work — see [ROADMAP.md](ROADMAP.md).

### Validation

`ExerciseSeedDataValidator` runs **before any DB write** and returns **every** problem at once (fail fast — no
partial import). It enforces: required fields present (`slug`, `name`, `description`, `category`, `type`,
`primaryMuscle`, `equipment`, `difficulty`, `mechanics`, ≥1 instruction); **unique** `slug` and **unique**
canonical `name`; valid `category` / muscle / equipment lookup codes and valid enum values; primary muscle not
duplicated in `secondaryMuscles`, secondaries distinct; no empty instruction steps; **no duplicate aliases**
(within an exercise, globally unique, and not colliding with another exercise's name); non-negative
calories/duration. On any failure the seeder logs each error and throws — the database is left untouched.

### Media gap

No exercise media is seeded. The model supports a single `ImageUrl` plus `ExerciseMedia` rows, but we do **not**
ship images/GIFs/video, to avoid shipping unlicensed/copyrighted media. `ImageUrl` is seeded empty and no
`ExerciseMedia` rows are created. The production media strategy (formats, CDN, licensing) is future work — see
[ROADMAP.md](ROADMAP.md).

### Verification (last run)

- `dotnet build` — succeeds (no new warnings).
- `dotnet test --filter Seeding.ExerciseSeedDataTests` — **7/7 pass** (incl. a regression guard that the embedded
  data validates clean and covers every category/equipment code).
- `--reseed-exercises` against a live DB upserts to **149 active global exercises** (counts track
  `exercises.json`; instruction/muscle/warning row counts scale with it); any renamed legacy entry is
  soft-deleted; **logged performed-exercise and plan-exercise rows are untouched.** The pure seed-data
  validation tests (`Seeding.ExerciseSeedDataTests`, 7/7) pass on the 149-entry file.
- API smoke check: `/health/ready` Healthy; `GET /api/exercises` filters return seeded rows; detail returns
  muscles/instructions/warnings/tags.

---

## Food catalog

A curated **starter catalog of 52 foods, beverages, and supplements** — currently **36 foods, 11 supplements,
and 5 beverages** (chicken, rice, oats, eggs, Greek yogurt, banana, almonds, olive oil, milk,
whey/casein/creatine/fish-oil/multivitamin, …) — enough to build realistic nutrition plans and log against. The
authoritative count is the entry count of [`foods.json`](../Modules/Modules.Food/Infrastructure/SeedData/foods.json)
(one object per food, keyed by `kind`). Each row carries headline macros **per its canonical serving**
(energy/protein/carb/fat/fiber). Foods are **global** (`TenantId == null`); a gym's own foods are added
separately as tenant-custom foods and are never touched by the seeder.

### Pipeline (mirrors the exercise seeder)

```
Modules.Food/Infrastructure/
  SeedData/foods.json                  ← the data (embedded resource)
  Seeding/FoodSeedModels.cs            ← deserialization shape (FoodSeedFile / FoodSeedDto)
  Seeding/FoodSeedDataLoader.cs        ← embedded-resource load + parse (no DB)
  Seeding/FoodSeedDataValidator.cs     ← fail-fast validation (name/kind/serving/macros)
  Seeding/FoodSeedFactory.cs           ← DTO → Food aggregate (domain factory only)
Presentations/WebApi/Composition/
  FoodMasterDataSeeder.cs              ← upsert/soft-delete against AppDbContext
```

Pure loader→validator→factory tests live in `Tests/Seeding/FoodSeedDataTests.cs` (run everywhere, no DB); the DB
insert + idempotency is covered by `Tests/Integration/FoodSeedingTests.cs` (Docker).

### Source & licensing (legally-clean by construction)

Whole-food macro values are drawn from **USDA FoodData Central** (SR Legacy / Foundation Foods), which is
**public domain (CC0 1.0)** — the food analogue of the exercise catalog's Unlicense source.

- **Citation (requested by USDA):** *U.S. Department of Agriculture, Agricultural Research Service. FoodData
  Central. fdc.nal.usda.gov.*
- **Quarantine note:** **Open Food Facts** (branded products) is **ODbL** — a share-alike/copyleft regime, the
  food analogue of the exercise catalog's CC-BY-SA quarantine. It must stay a *separate, attributed* source,
  never blended into this owned catalog.
- **Integrity caveat:** the seeded values are **rounded representative figures for generic items and MUST be
  re-verified against the live FDC database before a production catalog ships.** Branded supplements
  (whey/casein) are product-specific placeholders — real products belong in **tenant-custom** foods.

---

## Data safety (both catalogs)

The licensing discipline and the destructive-reseed safety below are **enforced by the shipped seeders** for both
catalogs — they are not advisory.

### Licensing rule — seed only clean, quarantine the rest

- **Seed only** content under a permissive/public-domain regime — **CC0 / Unlicense / MIT** (exercises: the
  free-exercise-db data under The Unlicense; foods: USDA FDC under CC0).
- **Quarantine** share-alike / copyleft / non-commercial regimes — **CC-BY-SA, AGPL, NC** (exercises: wger
  CC-BY-SA, exercisedb-api AGPL; foods: Open Food Facts ODbL). Quarantined content is **never blended** into the
  owned global catalog; if ingested at all it stays a separate, attributed store.
- **Data and media are separate license regimes.** The free-exercise-db split (public-domain JSON, CC-BY-SA
  images) is the textbook case — the *data* may seed even where the *images* may not.
- **Provenance travels per row.** Every catalog row records its source + license so the dataset stays auditable
  and a credits view can be generated for any attribution-requiring asset.

### Why a destructive reseed cannot destroy user data

- **Never hard-deletes referenced rows.** Every reference into a catalog uses `OnDelete(Restrict)` —
  `PerformedExercise.ExerciseId` and `PlanWorkoutExercise.ExerciseId` for exercises; `PlanMealItem.FoodId` and
  `LoggedItem.FoodId` for foods — so the **database itself blocks** physically deleting a referenced catalog
  entry. The seeders are built around this: they never hard-delete, they **upsert in place** (Ids preserved) and
  **soft-delete** obsolete entries.
- **"Clear" = soft-delete** (`IsDeleted = true`), the same semantics as deleting through the API. A reseed resets
  the **global** catalog (`TenantId IS NULL`) to exactly the seed set; entries no longer in the file are
  soft-deleted and disappear from the catalog. **Tenant-scoped (gym-custom) entries are never touched.**
- **A soft-deleted-but-still-referenced entry keeps its row** (the FK stays valid) and is logged
  (`pruned-but-referenced` / a warning). Logged history keeps its **denormalized snapshot** regardless — a
  `PerformedExercise` retains its `ExerciseName` + `TrackingType`; a `LoggedItem` retains its food name + macro
  snapshot.
- **Atomic.** load → validate (fail fast, no partial import) → one `SaveChanges` (EF wraps it in a transaction →
  rollback on failure) → invalidate the relevant cache. A validation error changes nothing.

Because of this design, the destructive reseed is **safe to run against production** (local or live) — it cannot
destroy user data. It is allowed in any environment but only via the explicit `--reseed-*` entrypoint; it never
runs at normal startup.

### Run modes (both catalogs)

| Mode | CLI | Startup | Behaviour |
|---|---|---|---|
| **InsertMissing** | `--seed-exercises` / `--seed-foods` | runs every boot (auto-seed) | Insert catalog entries whose canonical name doesn't exist yet. Never touches existing rows. Idempotent, non-destructive. |
| **Reseed** | `--reseed-exercises` / `--reseed-foods` | explicit only | Upsert every seed entry **by name, in place** (Id preserved; reactivated if soft-deleted) **and soft-delete** any global entry not in the file. |

The exercise reseed logs counts: `inserted, updated, reactivated, skipped-existing, skipped-inactive,
pruned-obsolete, pruned-but-referenced`. The food reseed logs the equivalent.

---

## Adding / editing entries

The seed-data tests re-validate the embedded data on every build, so a bad entry **fails the build before it can
reach a database**.

**Exercises** — add an object to the `exercises` array in `exercises.json`. Required:
`slug`, `name`, `description`, `category`, `type`, `primaryMuscle`, `equipment`, `difficulty`, `mechanics`, and
≥1 `instructions` step. Use only valid codes (`category` ∈ `categories.json`; muscles ∈ `muscles.json` —
`Chest`/`Back`/`Shoulders`/`Arms`/`Legs`/`Core`; `equipment` ∈ `equipment.json` —
`Bodyweight`/`Barbell`/`Dumbbell`/`Machine`/`ResistanceBand`; `type` ∈ `Strength|Cardio|Mobility|Stretching`;
`mechanics` ∈ `Compound|Isolation`; `difficulty` ∈ `Beginner|Intermediate|Advanced`). Optional `trackingType`
∈ `Strength|Bodyweight|Cardio|Timed|Hiit|Mobility|Custom` (omit to derive it from `type`+`equipment`). Give it a
**unique** `slug` and `name` and **globally unique** `aliases`. For real equipment the schema lacks (kettlebell,
cable, cardio machine), set `equipment` to the closest code and put the real name in `equipmentDetail` (it becomes
a search tag). Then `dotnet test --filter Seeding.ExerciseSeedDataTests`, then `--reseed-exercises` to apply.

**Foods** — edit `foods.json` (one object per food). Required: `name` (unique), `kind`
(`Food`/`Supplement`/`Beverage`), `servingLabel`. Macros are **per serving** and optional (a no-data supplement is
valid). Set `isActive: false` to retire an entry (skipped on seed / soft-deleted on reseed). The tests fail the
build on a bad kind, duplicate name, or negative macro before it can reach the database.
