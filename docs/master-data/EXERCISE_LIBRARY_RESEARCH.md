# Exercise Library Research

> **Phases covered:** 1 (how production platforms structure exercise data) and 5 (instruction quality).
> **Mandate:** no exercise data is invented; every non-obvious claim is cited in *Sources*.

This document establishes the **field vocabulary** and the **competitive quality bar** for the GymBro
exercise library. It answers: what does a production exercise record contain, how do the leading platforms
differ, what are the gaps worth exploiting, and what does a "production-quality instruction" actually mean.

---

## 1. How production platforms structure exercise data

We surveyed 12 platforms across three confidence tiers:

- **Documented schema (high confidence):** wger, Hevy, ExRx (published scraper schema + classification
  pages), Garmin (community-extracted official list), StrengthLevel.
- **Feature-described, schema not public (medium):** Strong, Trainerize, Fitbod, Nike Training Club, Apple
  Fitness+, Technogym/mywellness.

### 1.1 wger (open-source) — the cleanest reference implementation

Its REST schema is fully public and is effectively a reference model for a normalized exercise DB.

- **Exercise record** (`GET /api/v2/exercise/`): `id`, `uuid`, `created`, `last_update`, `category`,
  `muscles`, `muscles_secondary`, `equipment`, `variation_group` (links exercise variations),
  `license_author`. **Name/description are not on the exercise row** — they live in a related *translation*
  sub-object, multi-language by design.
- **Categories** (`/exercisecategory/`): flat 8-value, body-region oriented — Abs, Arms, Back, Calves,
  Cardio, Chest, Legs, Shoulders.
- **Muscles** (`/muscle/`): `id`, `name` (Latin anatomical, e.g. "Biceps brachii"), `name_en` (common),
  `is_front` (anterior vs posterior — drives the body-map render), `image_url_main`, `image_url_secondary`.
- **Equipment** (`/equipment/`): 11 values (Barbell, SZ-Bar, Dumbbell, Gym mat, Swiss Ball, Pull-up bar,
  none/bodyweight, Bench, Incline bench, Kettlebell, Resistance band).
- **Media**: separate `/exerciseimage/` endpoint; images are license-tracked first-class resources
  (CC authorship is a field).

**Takeaway:** name/description as a *translation* sub-object and a `variation_group` linking variants are the
two structural ideas worth stealing.

### 1.2 Hevy — the cleanest commercial enum set (public API)

- **Endpoints:** `GET /v1/exercise_templates` (paged), `GET /v1/exercise_templates/{id}` (Pro-only, `api-key`
  header).
- **ExerciseTemplate:** `id`, `title`, `type` (logging classification — weight_reps / reps_only / duration…),
  `primary_muscle_group`, `secondary_muscle_groups[]`, `is_custom`. Equipment appears only on the
  custom-create body as `equipment_category`.
- **MuscleGroup enum (20):** abdominals, shoulders, biceps, triceps, forearms, quadriceps, hamstrings,
  calves, glutes, abductors, adductors, lats, upper_back, traps, lower_back, chest, cardio, neck, full_body,
  other.
- **EquipmentCategory enum (9):** none, barbell, dumbbell, kettlebell, machine, plate, resistance_band,
  suspension, other.

**Takeaway:** Hevy's `type` field is GymBro's existing `TrackingType` concept — validation. The 20-muscle and
9-equipment enums are the commercial granularity bar (GymBro currently has 6 and 5).

### 1.3 ExRx.net — the richest taxonomy (kinesiology-grade)

ExRx is the deepest classification model and the source for our "differentiator" field set. ~2,100+ exercises.

- **Per-exercise fields** (from the published scraper schema): `display_name`,
  `instructions_preparation`, `instructions_execution`, `instructions_comments`, `muscles_target`,
  `muscles_synergists`, `muscles_stabilizers`, `muscles_dynstabilizers`, `muscles_antagonist_stabilizers`,
  `utility`, `mechanics`, `force`, `video_url`.
- **Four classification axes most platforms lack:**
  - **Muscle roles (5, not 2):** Target, Synergists, Stabilizers, Dynamic Stabilizers, Antagonist
    Stabilizers — everyone else collapses to primary/secondary.
  - **Utility:** Basic vs Auxiliary.
  - **Mechanics:** Compound vs Isolated.
  - **Force:** Push vs Pull.
- **Filter dimensions in their tool:** Name, Utility, Lateral Pattern, Movement Pattern, Muscle Group,
  Mechanics, Force, Apparatus.

> ExRx content is **proprietary** (see [DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md)) — we model
> *like* ExRx, we do not copy ExRx.

### 1.4 Garmin Connect — movement-pattern categorization

Closed DB (no free-text logging). Community-extracted structure: **Exercise | Category | Primary Muscles |
Secondary Muscles**. Crucially, its **categories are movement-pattern based**, not body-region: Bench Press,
Squat, Deadlift, Curl, Row, Pull Up, Push Up, Lunge, Lateral Raise, Leg Curl, Carry, Chop, Hip Raise, Olympic
Lift, Plyo, etc. Renders a targeted muscle map on devices.

**Takeaway:** validates that *movement pattern* is a distinct, useful categorization axis — orthogonal to
body region.

### 1.5 StrengthLevel — a *standards* model, not a library

Dimensioned for normative comparison: per (exercise × lift) it holds 1RM (Epley-converted from multi-rep),
bodyweight banding, gender, and a derived experience level (Beginner→Elite) + percentile, built from 150M+
logged lifts. The "exercise" is a key into a percentile table.

**Takeaway:** strength-standards distribution data is a future analytics layer GymBro could build *on top of*
its own logged-set data — not part of the exercise master record, but a related dataset.

### 1.6 Strong, Trainerize, Fitbod, NTC, Apple Fitness+, Technogym (feature-level)

- **Strong:** 200+ built-in + custom; categorized by muscle group + an exercise *type/category* that drives
  which metrics are logged (barbell, dumbbell, machine, weighted/assisted bodyweight, reps-only, cardio,
  duration). Users have requested movement-pattern categorization.
- **Trainerize:** coach-oriented **multi-dimensional tagging** — muscle targets, equipment, movement type,
  mechanics, and level — the same tags filter the library. Custom activity: name, type, tags, instructions,
  video (MP4/MOV ≤500 MB/≤5 min or YouTube link).
- **Fitbod:** differentiator is a **per-muscle recovery state machine** (0–100%, ~7-day full recovery).
  Implies the exercise record carries primary/secondary muscle mappings *with contribution weighting* +
  equipment — the recovery model couldn't work otherwise.
- **Nike Training Club / Apple Fitness+:** master-data unit is the **workout/session**, not an atomic
  exercise. Filter by muscle group, focus, equipment, length, intensity, level, trainer. Useful as a model
  for *content/session* metadata, not exercise metadata.
- **Technogym (mywellness):** device-telemetry model — the "exercise" is a machine activity/result record;
  partnership-gated API, not a content library.

---

## 2. Common fields vs. differentiators

### 2.1 The de-facto standard exercise record (table stakes — present on most platforms)

1. Name / title — universal.
2. Primary muscle(s) — universal.
3. Secondary muscle(s) — wger, Hevy, ExRx, Garmin, Fitbod, Trainerize.
4. Equipment — near-universal.
5. Category — universal, but split between **body-region** (wger, Strong, Hevy) and **movement-pattern**
   (Garmin, ExRx) philosophies.
6. Exercise type / logging mode — Hevy `type`, Strong category, Trainerize `type`. Determines what is logged.
7. Instructions (text) — wger, ExRx, Strong, Trainerize.
8. Media — near-universal, varied (images, animated video, Lottie-style, device renders).
9. Custom/built-in flag — Hevy `is_custom`, Strong, Trainerize.
10. Stable ID + variation linkage — wger `uuid` + `variation_group` is the cleanest.

### 2.2 Fields most platforms are MISSING — GymBro's opportunity

| Field | Who has it | Why it matters for GymBro |
|---|---|---|
| Mechanics (compound/isolation) | ExRx, Trainerize | Programming, volume accounting |
| Force (push/pull) | ExRx, Trainerize | Push/pull/legs split generation, balance analytics |
| Utility (basic/auxiliary) | ExRx only | Distinguish main lifts from accessories |
| 5-role muscle model (incl. stabilizers) | ExRx only | Injury prevention, accurate volume per muscle |
| Plane of motion / movement pattern as a field | Garmin (implicit) | AI programming, movement balance |
| Per-muscle contribution weighting + recovery | Fitbod | AI coaching, fatigue/recovery modeling |
| MET / energy cost | Technogym (telemetry only) | Calorie analytics |
| Strength-standards distribution | StrengthLevel | Benchmarking analytics (downstream) |
| Difficulty on the *exercise* | Trainerize (tag) | Beginner-safe substitution |
| Progression / regression links | ~nobody (wger `variation_group` closest) | Adaptive programming, AI substitution |
| Aliases / synonyms | ~nobody | **Search** — the biggest practical gap |
| Contraindications / safety / injury flags | ~nobody | Injury prevention, enterprise/clinical trust |
| Structured multi-language name/instructions | wger only | Localization without record duplication |

**Architecture takeaway:** the table-stakes 10 fields are necessary but not differentiating. The moat is the
missing list — and the two category philosophies (body-region vs movement-pattern) are a real modeling fork.
**Supporting both as orthogonal axes** (body region + movement pattern + mechanics + force + plane, all
independent) is what separates a serious model from a single flat `category` enum. This directly informs
[MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md).

---

## 3. Search strategy findings

- Most platforms expose **faceted filtering** on the structured fields (muscle, equipment, mechanics, force,
  level, movement pattern). ExRx's tool exposes all of these as facets simultaneously.
- **Aliases/synonyms are the missing search primitive everywhere.** Users search "pec fly", "chest fly",
  "machine fly", "butterfly" for one exercise. Without an alias table, name-substring search (GymBro's
  current approach) fails most of these.
- **Recommendation:** combine (a) full-text search over name + aliases + tags + localized name, (b) faceted
  filters over the structured enums, and (c) typo tolerance / fuzzy match. PostgreSQL `tsvector` + `pg_trgm`
  covers (a) and (c) without a separate search engine until scale demands one. Detailed in
  [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §Search.

---

## 4. Normalization & categorization best practices

- **Closed sets → enums/lookup tables, referenced by ID** (muscles, equipment, mechanics, force, plane,
  movement pattern, difficulty). This keeps them filterable and language-independent; their display labels
  are localized in the app i18n layer, **not** duplicated per exercise.
- **Open prose → localized translation rows** (name, aliases, instructions, cues, mistakes).
- **Many-to-many with role/attributes → join tables with payload** (e.g. ExerciseMuscle carrying *role* and
  *contribution weight*, not just a flag).
- **Orthogonal axes stay separate.** Do not fold movement pattern into category, or mechanics into type.
  Each axis answers a different query and a different algorithm.
- **Stable, language-neutral identity:** every exercise needs an immutable `Id` (GUID) **and** a human/URL
  `slug` that never changes even when the display name is edited or localized (wger's `uuid` lesson).

---

## 5. Instruction quality (Phase 5)

### 5.1 What a production-quality instruction contains

Per the **NSCA *Exercise Technique Manual for Resistance Training* (4th ed.)** and **NASM**, a high-quality
instruction is **structured**, not a paragraph blob:

1. **Overview** — one-line what/why.
2. **Setup / preparatory position** — stance, grip, bar/equipment position.
3. **Execution** — ordered concentric/eccentric steps.
4. **Breathing** — exhale on exertion; controlled Valsalva ≤1–2 s only near the sticking point.
5. **Tempo** — eccentric/concentric timing where relevant.
6. **Coaching cues** — short verbal/visual/tactile prompts.
7. **Common mistakes / faults** — and their corrections.
8. **Safety & spotting** — including belt guidance.
9. **Advanced notes** — variations, loading nuances.

> **Copyright boundary:** the field *schema* above is a citable coaching standard (reusable). The NSCA
> manual's actual step text and photos are **copyrighted** — author original prose or license content; cite
> NSCA/NASM only for the structure. See [DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §8.

### 5.2 Modeling implication

GymBro currently stores instructions as an ordered list of free-text steps (`ExerciseInstruction.Content`).
That collapses all nine fields into one stream. The target model stores **discrete, individually localizable
fields**: `Overview`, `Setup`, `Execution[]`, `Breathing`, `Tempo`, `Cues[]`, `CommonMistakes[]`,
`SafetyNotes`, `AdvancedNotes`. This enables per-section rendering, per-section translation, and
section-level AI grounding. See [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §Instructions.

### 5.3 Authoring standard ("no AI-generated generic descriptions")

- **Source of truth:** author against NSCA/NASM technique standards; for safety-relevant cues use
  human/expert review (a CSCS/CPT) before publish — never auto-publish AI-drafted safety text.
- **AI is a drafting assistant, not the author.** AI may draft from the structured fields + cited references;
  a qualified human reviews and signs off (`reviewed_by`, `reviewed_at`, `status=published`). This is
  enforced by the data-quality gates in [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §Quality.

---

## 6. Summary: what this means for the architecture

1. Model body-region, movement-pattern, mechanics, force, plane, and kinetic chain as **orthogonal axes**.
2. Upgrade muscle granularity from 6 → ~20 (Hevy bar) and model **muscle role + contribution weight**, not a
   boolean primary flag.
3. Add **aliases** (the #1 search gap) and a **progression/regression graph**.
4. Make name/instructions **structured and localizable** (wger's translation-subobject lesson + NSCA's
   instruction schema).
5. Add **safety** (contraindications, spotter), **programming** (rep/%1RM/rest by goal), and **analytics**
   (MET) fields grounded in NSCA/ACSM/Compendium — see
   [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md).

---

## Sources

Platform schemas and features:
- wger API — exercise/category/muscle/equipment/image endpoints: https://wger.de/api/v2/exercise/ ,
  https://wger.de/api/v2/exercisecategory/ , https://wger.de/api/v2/muscle/ , https://wger.de/api/v2/equipment/ ,
  https://wger.de/en/software/api , https://wger.readthedocs.io/
- Hevy API — ExerciseTemplate schema, MuscleGroup (20) & EquipmentCategory (9) enums: https://api.hevyapp.com/docs/ ,
  https://raw.githubusercontent.com/chrisdoc/hevy-mcp/refs/heads/main/openapi-spec.json ,
  https://www.hevyapp.com/features/exercise-library/
- ExRx — classification axes (utility/mechanics/force/5 muscle roles) and field schema:
  https://exrx.net/Questions/ExerciseClassAnalyses , https://github.com/flaviostutz/exrx-loader ,
  https://exrx.net/Lists/Directory , https://exrx.net/WorkoutWebApp/Exercises
- Garmin official exercise list (movement-pattern categories, primary/secondary muscles):
  https://github.com/mrnabilnoh/workout-plan-garmin-connect/blob/main/garmin_connect_exercise_list.md ,
  https://developer.garmin.com/gc-developer-program/training-api/
- StrengthLevel standards model: https://strengthlevel.com/strength-standards ,
  https://strengthlevel.com/one-rep-max-calculator
- Strong custom exercises / library: https://help.strongapp.io/article/97-create-custom-exercises ,
  https://www.strong.app/
- Trainerize tags & custom activity: https://help.trainerize.com/hc/en-us/articles/208689206 ,
  https://www.trainerize.com/blog/trainerize-update-interval-workouts-video-drive-new-workout-tags/
- Fitbod recovery/algorithm: https://fitbod.me/blog/fitbod-algorithm/ , https://fitbod.me/blog/muscle-recovery/ ,
  https://help.fitbod.me/hc/en-us/articles/360004429814
- Nike Training Club: https://www.nike.com/ntc-app
- Apple Fitness+ filters: https://support.apple.com/guide/fitness-plus/find-workouts-and-meditations-apdcd80997be/ios ,
  https://www.myhealthyapple.com/apple-fitness-adds-workout-filters-by-body-focus-trainer-music-and-equipment-in-ios-15/
- Technogym ecosystem / APIs: https://www.technogym.com/us/newsroom/technogym-ecosystem-open-platform/ ,
  https://openplatformdocs.mywellness.com/ , https://apidocs.mywellness.com/

Instruction-quality standard:
- NSCA *Exercise Technique Manual for Resistance Training*, 4th ed. (Human Kinetics):
  https://us.humankinetics.com/products/exercise-technique-manual-for-resistance-training-4th-edition-ebook-with-hkpropel-online-video
  (instruction schema citable for structure; text is copyrighted — do not copy)
- NASM safe & effective training methods: https://blog.nasm.org/training-benefits/implementing-safe-and-effective-training-methods

**Caveat:** ExRx pages and the Strong help article return HTTP 403 to automated fetches; ExRx specifics come
from its own classification page and the published open-source scraper schema (which mirrors the live page
structure), not a direct body fetch. wger/Hevy enum values are pulled directly from live API responses / the
OpenAPI spec (high confidence). Strong's field list is feature-described, not schema-confirmed.
