# Exercise Master-Data Seeding

How the global exercise catalog is seeded from structured files — and how to clear and refresh it safely.

> **Implements** the research/architecture proposal in this folder, constrained to the **current** schema.
> Seed data lives in files (never hardcoded in C#). The richer fields the proposal calls for are preserved in
> the files and documented below under [Known schema gaps](#known-schema-gaps).

---

## TL;DR

```bash
# Non-destructive: add catalog exercises that don't yet exist (this also runs automatically at startup).
dotnet run --project Presentations/WebApi -- --seed-exercises

# Destructive full refresh: upsert every seed exercise in place + soft-delete entries no longer in the files.
dotnet run --project Presentations/WebApi -- --reseed-exercises
```

Both verify EF migrations first (run `dotnet ef database update --context AppDbContext` beforehand, or set
`Database:AutoMigrate=true`, if the DB isn't migrated). The current library seeds **57 exercises** across
**13 categories**, **6 muscle groups**, and **5 equipment codes**.

---

## Where the seed files live

```
Modules/Modules.Exercise/Infrastructure/SeedData/
  exercises.json     # the exercise library (the data)
  muscles.json       # valid MuscleGroup codes (validation lookup)
  equipment.json     # valid Equipment codes (validation lookup)
  categories.json    # valid library categories (validation lookup)
```

They ship as **embedded resources** (see `Modules.Exercise.csproj`), so seeding works identically in local dev
and in containers — there is no working-directory or content-file dependency.

## The seeding code (a reusable, layered service)

| Type | Project | Responsibility |
|---|---|---|
| `ExerciseSeedDataLoader` | `Modules.Exercise` (`Infrastructure/Seeding`) | Read + deserialize the embedded JSON. Pure I/O, no DB. |
| `ExerciseSeedDataValidator` | `Modules.Exercise` | Validate the whole set; collect **all** errors; fail fast. Pure, no DB. |
| `ExerciseSeedFactory` | `Modules.Exercise` | Map a validated entry onto the `Exercise` aggregate (create / apply-in-place) via the domain factory only. |
| `ExerciseMasterDataSeeder` | `WebApi` (`Composition`) | Orchestrate load → validate → apply in one atomic `SaveChanges` → invalidate cache. The only DB-touching piece. |

The pure pieces live in the Exercise module (no `AppDbContext` dependency) and are unit-tested in
`Tests/Seeding/ExerciseSeedDataTests.cs`. The legacy hardcoded `ExerciseCatalogSeeder.cs` has been removed.

---

## How to add a new exercise

1. Add an object to the `exercises` array in `exercises.json`. Required fields:
   `slug`, `name`, `description`, `category`, `type`, `primaryMuscle`, `equipment`, `difficulty`,
   `mechanics`, and at least one `instructions` step.
2. Use only valid codes:
   - `category` ∈ `categories.json` (e.g. `chest`, `biceps`, `quadriceps`, `cardio`, `mobility` …).
   - `primaryMuscle` / `secondaryMuscles` ∈ `muscles.json` (`Chest`, `Back`, `Shoulders`, `Arms`, `Legs`, `Core`).
   - `equipment` ∈ `equipment.json` (`Bodyweight`, `Barbell`, `Dumbbell`, `Machine`, `ResistanceBand`).
   - `type` ∈ `Strength|Cardio|Mobility|Stretching`; `mechanics` ∈ `Compound|Isolation`;
     `difficulty` ∈ `Beginner|Intermediate|Advanced`.
   - Optional `trackingType` ∈ `Strength|Bodyweight|Cardio|Timed|Hiit|Mobility|Custom` (omit to derive it
     from `type`+`equipment`).
3. Give it a **unique** `slug` and `name`, and **globally unique** `aliases`.
4. For real equipment the schema lacks (kettlebell, cable, cardio machine), set `equipment` to the closest
   code and put the real name in `equipmentDetail` (it becomes a search tag) — see [gaps](#known-schema-gaps).
5. Run `dotnet test --filter Seeding.ExerciseSeedDataTests` — the suite re-validates the embedded data, so a
   bad entry fails the build before it can reach a database. Then `--reseed-exercises` to apply.

### What each field maps to

**Persisted now:** `name`→DefaultName, `description`→DefaultDescription, `type`, `trackingType`,
`primaryMuscle`/`secondaryMuscles`→ExerciseMuscle (IsPrimary), `equipment`, `difficulty`,
`mechanics`→MovementType, `estimatedCalories`, `averageDurationSeconds`, `instructions`→ExerciseInstruction,
`safetyNotes`→ExerciseWarning, and `tags` (merged with `category`/`movementPattern`/`forceType`/
`equipmentDetail`, slugified) →ExerciseTag.

**Preserved in the files but NOT yet persisted** (see [gaps](#known-schema-gaps)): `slug`, `aliases`,
`commonMistakes`, and the structured `category`/`movementPattern`/`forceType`/`equipmentDetail` (the last four
are folded into search tags so the catalog stays searchable by them).

---

## How validation works

`ExerciseSeedDataValidator` runs **before any DB write** and returns **every** problem at once (fail fast — no
partial import). It enforces:

- required fields present (`slug`, `name`, `description`, `category`, `type`, `primaryMuscle`, `equipment`,
  `difficulty`, `mechanics`, ≥1 instruction);
- **unique** `slug` and **unique** canonical `name`;
- valid `category` / muscle / equipment lookup codes and valid enum values;
- primary muscle not duplicated in `secondaryMuscles`; secondaries distinct;
- no empty instruction steps;
- **no duplicate aliases** (within an exercise, globally unique, and not colliding with another exercise's name);
- non-negative calories/duration.

On any failure the seeder logs each error and throws — the database is left untouched.

---

## How to clear and reseed

Two modes (see `ExerciseSeedMode`):

| Mode | CLI | Startup | Behaviour |
|---|---|---|---|
| **InsertMissing** | `--seed-exercises` | ✅ runs every boot | Insert exercises whose name doesn't exist yet. Never touches existing rows. Idempotent, non-destructive. |
| **Reseed** | `--reseed-exercises` | ❌ explicit only | Upsert every seed exercise **by name, in place** (Id preserved; reactivated if soft-deleted) **and soft-delete** any global exercise not in the files. |

Both run inside a single atomic `SaveChanges` (EF wraps it in a transaction → **rollback on failure, no
partial import**) and then invalidate the exercise catalog cache. Each run logs counts:
`inserted, updated, reactivated, skipped-existing, skipped-inactive, pruned-obsolete, pruned-but-referenced`.

### What gets cleared

- Only the **global** catalog (`TenantId IS NULL`). Tenant-scoped (gym-custom) exercises are never touched.
- "Clear" = **soft-delete** (`IsDeleted = true`), not a physical delete — the same semantics as deleting an
  exercise through the API. Reseed resets the global catalog to exactly the seed set; entries no longer in the
  files are soft-deleted and disappear from the catalog.

### What is protected (and why it can't be destroyed)

- **User workout logs (`PerformedExercise`) and workout plans (`PlanWorkoutExercise`) are never deleted.**
- Their `ExerciseId` foreign keys use `OnDelete(Restrict)`, so the database itself **blocks** physically
  deleting a referenced exercise. The seeder is built around this: it never hard-deletes — it upserts in place
  (preserving Ids) and soft-deletes obsolete entries. A soft-deleted-but-still-referenced exercise keeps its
  row (FK stays valid) and is logged as `pruned-but-referenced`; logged history keeps its denormalized name +
  tracking-type snapshot regardless.

> **Environment note.** Destructive reseed is allowed in any environment (local and production), but only via
> the explicit `--reseed-exercises` entrypoint — it never runs at normal startup. Because of the soft-delete +
> FK-restrict design above, it is safe to run against production: it cannot destroy user data.

---

## Media gap

No exercise media is seeded. The data model supports a single `ImageUrl` plus `ExerciseMedia` rows, but we do
**not** ship images/GIFs/video, to avoid shipping unlicensed/copyrighted media. `ImageUrl` is seeded empty and
no `ExerciseMedia` rows are created. The production media strategy (formats, CDN, licensing) is in
[MEDIA_STRATEGY.md](MEDIA_STRATEGY.md); wiring real assets is future work.

---

## Known schema gaps

These fields are authored in `exercises.json` (so the data is production-quality and forward-compatible) but
the **current** schema cannot persist them as structured columns. They are either folded into search tags or
preserved for a future migration. TODO list for the schema upgrade (tracked in
[MIGRATION_PLAN.md](MIGRATION_PLAN.md)):

| Seed field | Current handling | Future schema (target) |
|---|---|---|
| `slug` | Used for validation/dedup; **not stored** | Add a stable `Slug` column (language-neutral identity) |
| `category` (biceps, quads, glutes, …) | Folded into search **tags** | First-class library category + finer `Muscle` lookup (~20) |
| `movementPattern` (Hinge, Horizontal Push, …) | Folded into search **tags** | First-class `MovementPattern` column |
| `forceType` (Push/Pull/Static) | Folded into search **tags** | First-class `ForceType` column |
| `equipmentDetail` (Kettlebell, Cable, Cardio Machine, …) | Mapped to nearest `Equipment` code + folded into **tags** | Expand `Equipment` lookup (~17) + category |
| `commonMistakes` | **Not stored** | Structured instruction sub-fields |
| `aliases` | Validated unique; **not stored** | `aliases` rows + full-text search |
| `secondaryMuscles` granularity | Coarse 6-group enum (e.g. biceps→Arms) | ~20-muscle lookup + role + contribution weight |

Search is also a known gap: the current catalog search is a **case-sensitive `LIKE`** on the name and does not
match aliases. Faceted filters (muscle/type/equipment/difficulty/movementType) work today. The target
(PostgreSQL full-text + trigram + alias search) is in
[MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §Search.

---

## Verification (last run)

- `dotnet build` — succeeds (no new warnings).
- `dotnet test --filter Seeding.ExerciseSeedDataTests` — **7/7 pass** (incl. a regression guard that the
  embedded data validates clean and covers every category/equipment code).
- `--reseed-exercises` against local dev → **57 active global exercises**, 116 instructions, 123 muscle links,
  58 warnings; the renamed legacy entry (`Rowing Machine (Distance)`) soft-deleted; **20 logged
  performed-exercises and 27 plan-exercise rows untouched.**
- API smoke check: `/health/ready` Healthy; `GET /api/exercises` filters return seeded rows; detail returns
  muscles/instructions/warnings/tags.
