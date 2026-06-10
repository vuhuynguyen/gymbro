# Food Catalog Seeding

> **Status: implemented.** A file-based seed populates the global Food/Supplement catalog, mirroring the
> exercise seeding flow ([master-data/EXERCISE_SEEDING.md](../master-data/EXERCISE_SEEDING.md)). This is the
> "Phase 1 — catalog foundation" data so the next test/build round (plan authoring, daily logging, the Flutter
> Today screen) has real foods to pick from.

## What it seeds

A curated **starter catalog of ~39 common foods, beverages, and supplements** (chicken, rice, oats, eggs,
Greek yogurt, banana, almonds, olive oil, milk, whey/casein/creatine/fish-oil/multivitamin, …) — enough to
build realistic nutrition plans and log against. Each row carries headline macros **per its canonical serving**
(energy/protein/carb/fat/fiber). Foods are **global** (`TenantId == null`); a gym's own foods are added
separately as tenant-custom foods and are never touched by the seeder.

## Source & licensing (legally-clean by construction)

Whole-food macro values are drawn from **USDA FoodData Central** (SR Legacy / Foundation Foods), which is
**public domain (CC0 1.0)** — the food analogue of the exercise catalog's Unlicense source.

- **Citation (requested by USDA):** *U.S. Department of Agriculture, Agricultural Research Service. FoodData
  Central. fdc.nal.usda.gov.*
- **Quarantine note:** **Open Food Facts** (branded products) is **ODbL** — a share-alike/copyleft regime, the
  food analogue of the exercise catalog's CC-BY-SA quarantine. It must stay a *separate, attributed* source,
  never blended into this owned catalog.
- **Integrity caveat (carried from the master-data mandate):** the seeded values are **rounded representative
  figures for generic items and MUST be re-verified against the live FDC database before a production catalog
  ships.** Branded supplements (whey/casein) are product-specific placeholders — real products belong in
  **tenant-custom** foods.

## How to run

The seed file is an **embedded resource** (`Modules.Food/Infrastructure/SeedData/foods.json`), so it ships
inside the image — no working-directory assumptions.

| Trigger | Mode | Effect |
|---|---|---|
| **App startup** (any environment, via `DbSeeder`) | `InsertMissing` | Adds catalog foods that don't yet exist; never touches existing rows (admin edits + a prior reseed are preserved). |
| `dotnet run --project Presentations/WebApi -- --seed-foods` | `InsertMissing` | Same, as a one-off CLI run (seed-and-exit). |
| `dotnet run --project Presentations/WebApi -- --reseed-foods` | `Reseed` | Destructive refresh: upserts every seed food **in place** (Id preserved) and **soft-deletes** any global food not in the seed set. |

Both CLI entrypoints verify migrations first (same policy as normal startup), then exit.

## Data safety

- **Never hard-deletes.** `PlanMealItem.FoodId` and `LoggedItem.FoodId` are `OnDelete(Restrict)`, so the DB
  itself blocks physical deletion of a referenced food. Reseed **soft-deletes** obsolete foods (Id preserved)
  and **logs a warning** for any that are still referenced by a plan/log — their row stays (FK-safe) and logged
  items keep their **denormalized food snapshot** regardless.
- **Upsert by canonical `Name`** preserves the food's `Id`, so existing plan items / logged items stay valid
  across a reseed.
- **Atomic:** load → validate (fail fast, no partial import) → one `SaveChanges`. A validation error changes
  nothing.

## Pipeline (mirrors the exercise seeder)

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

Pure loader→validator→factory tests live in `Tests/Seeding/FoodSeedDataTests.cs` (run everywhere, no DB); the
DB insert + idempotency is covered by `Tests/Integration/FoodSeedingTests.cs` (Docker).

## Adding / editing foods

Edit `foods.json` (one object per food). Required: `name` (unique), `kind` (`Food`/`Supplement`/`Beverage`),
`servingLabel`. Macros are **per serving** and optional (a no-data supplement is valid). Set `isActive: false`
to retire an entry (skipped on seed / soft-deleted on reseed). The seed-data tests fail the build on a bad
kind, duplicate name, or negative macro before it can reach the database.
