# GymBro Exercise Master Data — Research & Architecture

> **Status:** Research & architecture proposal (Phase 0). **No code has been written.** These documents
> define *what should exist* before any migration is implemented. Implementation is gated on sign-off.

This folder is the deliverable for the **Exercise Master Data strategy** — a production-grade plan for an
exercise library at the quality bar of Strong, Hevy, Fitbod, and Technogym, designed to scale to
**5,000–10,000+ exercises**, multiple languages, AI coaching, analytics, and enterprise customers.

## Documents

| Doc | Owns | Covers task phases |
|---|---|---|
| [EXERCISE_LIBRARY_RESEARCH.md](EXERCISE_LIBRARY_RESEARCH.md) | How commercial platforms structure exercise data; instruction-quality standards | Phase 1, Phase 5 |
| [DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) | Trusted datasets, licensing, recommended sourcing strategy | Phase 2 |
| [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) | The proposed master-data model, localization, data-quality standards | Phase 3, 6, 7 |
| [MEDIA_STRATEGY.md](MEDIA_STRATEGY.md) | Image/animation/video formats, CDN, storage, licensing | Phase 4 |
| [MIGRATION_PLAN.md](MIGRATION_PLAN.md) | Audit of the current module + enterprise migration plan | Phase 8 |
| [IMPORT_PIPELINE.md](IMPORT_PIPELINE.md) | ETL, validation, dedup, media, versioning, rollback | Phase 9 |
| [EXERCISE_SEEDING.md](EXERCISE_SEEDING.md) | **Implemented** file-based seed + safe clear/reseed flow (how to run it, what's protected) | — |

> **Implementation status:** the file-based seeding flow described in [EXERCISE_SEEDING.md](EXERCISE_SEEDING.md)
> is **built and verified** (57-exercise starter library, `--seed-exercises` / `--reseed-exercises`). The
> remaining documents are the still-pending architecture proposal that seeding is a first step toward.

## How to read this

1. **EXERCISE_LIBRARY_RESEARCH** establishes the field vocabulary and the competitive bar.
2. **DATA_SOURCE_COMPARISON** decides where the data legally comes from (this constrains everything else).
3. **MASTER_DATA_ARCHITECTURE** is the target model — the single most important document.
4. **MEDIA_STRATEGY** and **IMPORT_PIPELINE** are the supporting subsystems.
5. **MIGRATION_PLAN** maps today's 30-exercise module to the target, phased and reversible.

## Headline recommendations (executive summary)

- **Seed legally clean.** Base the catalog on `yuhonas/free-exercise-db` **data** (The Unlicense / public
  domain, 873 exercises). Treat its **images** separately — they trace to Everkinetic CC-BY-SA 3.0, so
  replace or independently license them. Never blend CC-BY-SA (wger) or AGPL (exercisedb-api) content into
  the owned master table. See [DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md).
- **Model the moat.** The "table stakes" fields (name, muscles, equipment, instructions) are everywhere; the
  competitive differentiators GymBro should model are the ones almost nobody has: ExRx-grade
  mechanics/force/utility + 5-role muscle modeling, plane of motion, movement pattern as a first-class axis,
  progression/regression graph, aliases for search, and per-muscle contribution weighting for AI/recovery.
- **Separate master data from translations** with a normalized `ExerciseTranslation` table keyed on BCP-47
  locale, with source-version tracking so stale translations are flagged, never silently shipped.
- **Never serve media from the app server.** Object storage (R2/S3) behind a CDN, content-hashed immutable
  URLs, signed URLs only for premium video. No animated GIFs — Lottie for vector demos, muted looping
  MP4/WebM for filmed clips.
- **Cite, don't copy.** Numeric ranges (rep/%1RM/rest), MET values, the RPE↔RIR scale, and field schemas are
  factual and reusable with citation. Position-stand prose, the NSCA table layout, and ETM instructions are
  copyrightable — paraphrase or license.

## A note on sourcing integrity

Per the task mandate, **no exercise information was invented.** Every scientific value, taxonomy, and
licensing claim is attributed to a source in the relevant document's *Sources* section. Where a fact could
not be verified (e.g. ExRx pages block automated fetch, the ACSM PDF returned as binary), the document says
so explicitly and points to the authoritative record to confirm before shipping.
