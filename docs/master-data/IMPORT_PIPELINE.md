# Import Pipeline (ETL)

> **Phase covered:** 9 (how production data is imported). Runs **only after** the target model and legal-clean
> guardrails exist ([MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md), migration Phase H in
> [MIGRATION_PLAN.md](MIGRATION_PLAN.md)). Grows the catalog from 30 hand-curated exercises → 5,000–10,000+.

The pipeline's job: take raw datasets ([DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md)), normalize them
onto the target model, **reject anything legally or qualitatively unfit**, deduplicate, attach media, version
the result, and make every run reversible.

---

## 1. Principles

1. **Quarantine by license at the gate.** The very first filter rejects rows whose `LicenseCode` is in the
   copyleft/NC set (`CC-BY-SA-*`, `CC-*-NC-*`, `AGPL`) from entering the owned master table
   ([DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §4.5). Legal-clean is not a review step — it's a
   precondition.
2. **Idempotent & re-runnable.** Re-importing the same source produces no duplicates and no spurious version
   bumps (matches the existing seeder's idempotency-by-name approach, generalized to slug + fuzzy match).
3. **Staging before production.** Nothing writes straight to `Exercise`. Data lands in a staging schema,
   passes validation, is reviewed, then is promoted.
4. **Human-in-the-loop for safety content.** Imported rows are `Status=Draft`/`InReview`; safety/cue text
   needs sign-off before `Published` ([MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §11.3).
5. **Versioned & reversible.** Every import run is a labelled batch; promotion and rollback operate on the
   batch.

---

## 2. Pipeline stages

```
1 Ingest ─▶ 2 License gate ─▶ 3 Normalize/map ─▶ 4 Validate ─▶ 5 Dedup/match ─▶
6 Media fetch+derive ─▶ 7 Stage ─▶ 8 Review ─▶ 9 Promote ─▶ (10 Reconcile updates)
                                                                     ▲
                                                            rollback any batch
```

### Stage 1 — Ingest

Pull a source into raw staging: free-exercise-db JSON (873, Unlicense), an exercisedb.io export if licensed,
or in-house CSV. Record `source`, `source_version`/snapshot date, and the import `batch_id`. Sources are
**versioned snapshots** so a run is reproducible.

### Stage 2 — License gate (hard reject)

Stamp `LicenseCode`/`Source` from the source manifest; **reject** quarantined licenses before any further
work. Data and media are evaluated separately (free-exercise-db: data passes, images would be flagged for
replacement — [MEDIA_STRATEGY.md](MEDIA_STRATEGY.md) §6). Rejected rows are logged, not silently dropped.

### Stage 3 — Normalize & map

Map source fields onto the target model:

- **Muscles:** source muscle strings → `Muscle` lookup IDs; assign `Role` (source "primary"→PrimeMover,
  "secondary"→Synergist) and a **default `ContributionWeight`** (flagged for expert review — source data
  rarely has weights).
- **Equipment:** source equipment → `Equipment` lookup + `Requirement`.
- **Classification:** infer `MovementPattern`/`ForceType`/`Mechanics`/`Plane` where the source provides
  them (free-exercise-db has `force`, `mechanic`, `category`); leave the rest null → flagged for enrichment.
- **Instructions:** source step list → `Execution[]`; other structured fields (setup/breathing/tempo/cues/
  mistakes) start empty → authoring backlog.
- **Locale:** import into the canonical `en` `ExerciseTranslation`; `ContentVersion=1`, `SourceVersion=1`.

A **mapping table** (source vocabulary → GymBro lookups) is maintained per source; unmapped values raise a
warning, never an auto-create.

### Stage 4 — Validate

Run the publish-gate rules as **import-time validation**
([MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §11): required fields, ≥1 PrimeMover, contribution
weights ~1.0, enum FK integrity, slug uniqueness, no duplicate aliases, license present. Failures **don't kill
the row** — they downgrade it to `Draft` with a machine-readable checklist of what's missing (enrichment
queue). Only fully-valid rows are eligible for `Published` at promotion.

### Stage 5 — Deduplicate & match

The same exercise recurs across sources and against the existing catalog. Match by, in order:

1. exact `Slug`;
2. normalized-name exact (lowercase, strip punctuation/equipment qualifiers);
3. **fuzzy** (trigram similarity on name + alias overlap + same primary muscle + same equipment) above a
   threshold → **merge candidate** (human-confirmed), not auto-merge.

On match: enrich the existing record (fill nulls, add aliases/media) rather than insert a duplicate. New
aliases discovered during dedup feed search. This generalizes the current seeder's idempotency-by-`DefaultName`.

### Stage 6 — Media fetch & derive

For accepted assets: download original → license-check → generate renditions/posters/placeholders → upload to
the derivatives bucket → record `AssetKey` + license metadata on `ExerciseMedia`
([MEDIA_STRATEGY.md](MEDIA_STRATEGY.md) §7). CC-BY-SA/missing-license images are **not** imported into the core
set — queued for replacement (commission / in-house / exercisedb.io). Media failures don't block the
exercise row (it stays `Draft` pending an image, per the publish gate).

### Stage 7 — Stage

Write normalized, validated, deduped rows to a **staging schema** mirroring the target tables, tagged with
`batch_id`. Nothing in production changes yet.

### Stage 8 — Review

Curators/coaches review the batch: confirm merge candidates, fix mappings, author missing structured
instructions, sign off safety text (`ReviewedBy`). A batch dashboard shows counts: ready-to-publish vs
blocked-by-checklist. **AI may pre-draft** structured instructions/cues from the imported facts + cited
references, but a qualified human reviews before publish
([EXERCISE_LIBRARY_RESEARCH.md](EXERCISE_LIBRARY_RESEARCH.md) §5.3).

### Stage 9 — Promote

Promote the batch from staging → production in a transaction: insert new `Exercise`+children, enrich matched
ones, set eligible rows `Published`. Bust the `ExerciseCatalogCache` and rebuild `ExerciseSearchDoc`. The
batch is recorded as an immutable promotion event.

### Stage 10 — Reconcile updates (re-import of a changed source)

On a later source snapshot: diff against the last imported version of that source; for changed exercises bump
`Exercise.ContentVersion` (→ marks translations `Outdated`,
[MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §8.5) and re-run validation; **never overwrite
in-house edits blindly** — field-level provenance decides precedence (in-house authored > imported). New
exercises in the snapshot flow through stages 2–9 as a fresh batch.

---

## 3. Versioning & rollback

- **Batch is the unit.** Every promotion is a labelled batch with full lineage (source, snapshot, mappings,
  row outcomes). 
- **Rollback** un-promotes a batch: newly-inserted rows are soft-deleted (existing `ISoftDelete`), and
  enrichments to matched rows are reverted from the pre-batch field snapshot captured at promotion. Because
  imports are additive and field-provenance-tracked, rollback is deterministic.
- **Schema vs data versioning are separate.** Structural changes go through `dotnet ef` migrations
  ([MIGRATION_PLAN.md](MIGRATION_PLAN.md), [[gymbro-ef-migrations-workflow]]); content batches go through this
  pipeline. Don't conflate the two.

---

## 4. Where it runs (implementation shape, not yet built)

- A **separate console/worker tool** (not the WebApi request path) under the API solution — long-running,
  offline-capable, reuses the domain `Exercise.CreateGlobal(...)` factory and FluentValidation rules so
  import obeys the same invariants as the API. Mirrors how `ExerciseCatalogSeeder` already reuses the factory,
  scaled up with staging + review.
- Media derivation shells out to **ffmpeg** + **ImageSharp/NetVips**
  ([MEDIA_STRATEGY.md](MEDIA_STRATEGY.md) §7).
- Runs against the same `AppDbContext` (run both migration chains first).
- **Observability:** per-stage counts (ingested / license-rejected / validation-failed / merged / published),
  and **no silent caps** — if a run bounds coverage, it logs what was dropped and why.

---

## 5. First production run (concrete)

1. Migration Phases A–G complete; model + guardrails live.
2. Ingest free-exercise-db **data** (Unlicense) → license gate passes the data, flags the images.
3. Normalize 873 → target model; classification/instruction gaps become the enrichment backlog.
4. Dedup against the 30 hand-curated seed (they win on quality; imports enrich aliases/fields).
5. Replace flagged images with in-house/commissioned/exercisedb.io media.
6. Stage → review → author missing instructions → sign off safety → promote.
7. Iterate with additional licensed sources toward the 5,000–10,000+ target, each as its own reversible batch.

No code is written until the architecture and this pipeline are approved.
