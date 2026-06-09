# Exercise Master Data Architecture

> **Phases covered:** 3 (data model), 6 (localization), 7 (data-quality standards). This is the target model
> — *what should exist*, unconstrained by today's schema. The mapping from today → target and the phased
> rollout live in [MIGRATION_PLAN.md](MIGRATION_PLAN.md).

Design goals: scientific accuracy, scalability to **5,000–10,000+ exercises**, multi-language, AI-coaching
readiness, analytics, search, and licensing-clean provenance. Every scientific value is grounded in
NSCA/ACSM/Compendium sources (cited in *Sources*); none is invented.

---

## 1. Design principles

1. **Orthogonal axes, not one `category`.** Body region, movement pattern, mechanics, force, plane of motion,
   and kinetic chain each answer a different query and a different algorithm — keep them independent
   ([EXERCISE_LIBRARY_RESEARCH.md](EXERCISE_LIBRARY_RESEARCH.md) §2.2).
2. **Closed sets are lookup tables referenced by ID** (muscle, equipment, etc.); their display labels are
   localized in the app i18n layer, never duplicated per exercise.
3. **Open prose is localized in a separate translation table** — master data and translations never mix.
4. **Identity is immutable and language-neutral** — a GUID `Id` plus a stable `Slug`; the display name can
   change or be localized without breaking references, deep links, or analytics.
5. **Provenance and license travel with every row and every media asset** (legal-clean by construction).
6. **Versioned content** — a `ContentVersion` on the master row lets translations detect staleness and lets
   the import pipeline reconcile updates ([IMPORT_PIPELINE.md](IMPORT_PIPELINE.md)).
7. **Additive over destructive.** The model must extend (new muscle, new equipment, new language, new media
   type) without schema churn or data migration.

---

## 2. Entity overview

```
Exercise (invariant master)
 ├─ ExerciseTranslation        (name, aliases, instructions … per BCP-47 locale)   [LOCALIZED]
 ├─ ExerciseMuscle             (muscle role + contribution weight)                 [join + payload]
 ├─ ExerciseEquipment          (required / optional / accessory)                   [join + payload]
 ├─ ExerciseMedia              (asset refs: image/animation/video/lottie/3d)       [media regime]
 ├─ ExerciseProgramming        (per-goal rep/%1RM/rest/RPE; MET)                    [reference values]
 ├─ ExerciseRelationship       (variation/progression/regression/alternative/…)    [self-graph]
 ├─ ExerciseSafety             (contraindications, spotter, injury risk)            [safety]
 └─ ExerciseSearchDoc          (materialized tsvector + trigram, per locale)        [search, derived]

Lookups (closed sets, ID-referenced, app-i18n labels):
  Muscle · MuscleGroup · Equipment · EquipmentCategory · MovementPattern · ForceType ·
  Mechanics · PlaneOfMotion · KineticChain · Utility · DifficultyLevel · TrackingType · TrainingGoal
```

> **Bounded-context note (GymBro convention):** this all lives in `Modules.ExerciseModule`. Other modules
> integrate via shared MediatR contracts and `ExerciseId` FKs only — no cross-module entity references
> (per [ARCHITECTURE.md](../ARCHITECTURE.md) / `gymbro/CLAUDE.md`). The `ExerciseTrackingType` and
> tracking-metric matrix stay in `BuildingBlocks.Shared.Tracking` as today.

---

## 3. The master `Exercise` (invariant)

Only language-neutral, structured facts live here. **No display text.**

| Field | Type | Notes / source |
|---|---|---|
| `Id` | GUID | PK, immutable |
| `Slug` | string, unique | stable URL/identity key (e.g. `barbell-bench-press`); never changes on rename |
| `CanonicalLocale` | BCP-47 | source-of-truth language (e.g. `en`) |
| `Type` | enum `ExerciseType` | Strength / Cardio / Mobility / Stretching (existing) |
| `TrackingType` | enum `ExerciseTrackingType` | drives logged metrics (existing shared kernel) |
| `MovementPattern` | enum | Squat, Hinge, Lunge, HorizontalPush, VerticalPush, HorizontalPull, VerticalPull, Carry, Rotation, Gait, Isometric — *Dan John / functional-training literature* |
| `Mechanics` | enum | Compound / Isolation — *kinesiology* |
| `ForceType` | enum | Push / Pull / Static — *kinesiology* |
| `PlaneOfMotion` | enum (multi) | Sagittal / Frontal / Transverse — *NASM / anatomy* |
| `KineticChain` | enum | Open / Closed — *Physiopedia / kinesiology* |
| `Utility` | enum | Basic / Auxiliary — *ExRx classification (modeled, not copied)* |
| `Laterality` | enum | Bilateral / Unilateral / Alternating |
| `Difficulty` | enum `DifficultyLevel` | Beginner / Intermediate / Advanced — exercise *technical demand* (distinct from user training-age); *NSCA 2021 SCJ + ACSM* |
| `PrimaryBodyRegion` | enum | UpperBody / LowerBody / Core / FullBody (fast region facet) |
| `IsBilateralLoadable` | bool | supports per-side load (informs analytics) |
| `Status` | enum | Draft / InReview / Published / Deprecated |
| `IsCustom` | bool | user/coach-created vs platform catalog (Hevy `is_custom` parallel) |
| `OwnerTenantId` | GUID? | null = global; value = tenant-scoped custom (existing ISharedEntity pattern) |
| `Source` | string | provenance: `free-exercise-db` / `in-house` / `exercisedb.io` / … |
| `LicenseCode` | enum | `Unlicense` / `CC-BY-4.0` / `proprietary` / `in-house` … (legal-clean guardrail) |
| `ContentVersion` | int | bumped when localizable content changes (drives translation staleness) |
| `PopularityScore` | int? | derived from logged usage (analytics, ranking) — nullable, recomputed |
| `CreatedOnUtc` / `ModifiedOnUtc` / soft-delete | audit | existing `AggregateRoot` / `ISoftDelete` |

**Why these axes (and not a flat category):** body-region, movement-pattern, mechanics, force, and plane are
independent — a barbell bench press is *UpperBody / HorizontalPush / Compound / Push / Sagittal+Transverse /
Closed-ish*. Folding any into a single category loses query power for AI programming, balance analytics, and
substitution.

---

## 4. Muscles — role + contribution weight (the Fitbod/ExRx lesson)

Upgrade from a 6-value `MuscleGroup` enum with a boolean `IsPrimary` to a **20+-muscle lookup** with a
**5-role model and a contribution weight**.

**`Muscle` lookup** (closed set, ID-referenced, anterior/posterior flag for the body map — wger's `is_front`):
~20 muscles at the Hevy granularity bar — chest, anterior/lateral/posterior deltoid, biceps, triceps,
forearms, lats, traps, rhomboids/upper-back, lower-back/erectors, abs, obliques, glutes, quadriceps,
hamstrings, adductors, abductors, calves, neck — each mapped to a coarse `MuscleGroup` for backward-compatible
filtering.

**`ExerciseMuscle`** (join with payload):

| Field | Type | Notes / source |
|---|---|---|
| `ExerciseId` | GUID | FK |
| `MuscleId` | int | FK → Muscle |
| `Role` | enum `MuscleRole` | PrimeMover / Synergist / Stabilizer / DynamicStabilizer / Antagonist — *OpenStax A&P, NASM, ExRx* |
| `ContributionWeight` | decimal(3,2) 0–1 | share of work for this muscle (powers per-muscle volume, recovery/fatigue, AI) — *modeled after Fitbod recovery* |

This single change unlocks: accurate **per-muscle volume**, **recovery/fatigue** modeling (Fitbod-style),
**balance analytics** (push/pull, agonist/antagonist), and **injury-aware substitution**. `IsPrimary` is
derived (`Role == PrimeMover`) for any legacy consumer.

---

## 5. Equipment — typed, with accessories

**`Equipment` lookup** at the commercial bar (≈ Hevy's 9 + GymBro additions): bodyweight, barbell, dumbbell,
kettlebell, machine, cable, plate, resistance_band, suspension, smith_machine, ez_bar, trap_bar, medicine_ball,
swiss_ball, bench, pull_up_bar, sled, other — each tagged with an `EquipmentCategory` (FreeWeight / Machine /
Cable / Bodyweight / Accessory).

**`ExerciseEquipment`** (join): `ExerciseId`, `EquipmentId`, `Requirement` enum (Required / Optional /
Alternative), so "goblet squat" = dumbbell *or* kettlebell, and "bench press" requires barbell + bench. (An
optional future `MachineBrand`/asset-mapping table supports gym-specific machine inventories — out of scope
for MVP, but the join model leaves room.)

---

## 6. Instructions — structured & localized (Phase 5 model)

Instructions are **not** a flat step list. Per the NSCA technique standard
([EXERCISE_LIBRARY_RESEARCH.md](EXERCISE_LIBRARY_RESEARCH.md) §5), they are **discrete fields, each
individually localizable**, stored on `ExerciseTranslation` (§8):

`Overview`, `Setup`, `Execution[]` (ordered steps), `Breathing`, `Tempo`, `Cues[]`, `CommonMistakes[]`,
`SafetyNotes`, `AdvancedNotes`.

This enables per-section rendering (web/mobile), per-section translation, per-section AI grounding, and
section-level review sign-off. Authoring follows the "AI drafts, qualified human signs off safety text" rule
in §11.

---

## 7. Programming, analytics & safety reference data

### 7.1 `ExerciseProgramming` — per-goal prescription (factual, citable values)

Stored per `TrainingGoal`, grounded in the **NSCA load/rep/rest continuum** and **ACSM Position Stand on
Progression Models** (values are facts; do not copy table layout — see
[DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §3.8):

| Goal | Load (%1RM) | Reps | Sets | Rest |
|---|---|---|---|---|
| Strength | ≥ 85% | ≤ 6 | 2–6 | 2–5 min |
| Power (single-effort) | 80–90% | 1–2 | 3–5 | 2–5 min |
| Power (multiple-effort) | 75–85% | 3–5 | 3–5 | 2–5 min |
| Hypertrophy | 67–85% | 6–12 | 3–6 | 30 s–1.5 min |
| Muscular endurance | ≤ 67% | ≥ 12 | 2–3 | ≤ 30 s |

Schema: `ExerciseProgramming(ExerciseId, Goal, LoadPctMin, LoadPctMax, RepMin, RepMax, RestSecMin, RestSecMax,
RpeMin, RpeMax)`. Note **ACSM novice strength is 60–70%**, differing from NSCA's ≥85% — store the source label
rather than blending the two.

**RPE↔RIR** (GymBro stores RPE as **integer 1–10**, per [[gymbro-rpe-integer-decision]] — fully compatible
with the **Zourdos et al. 2016** integer scale):

| RPE | RIR | Meaning |
|---|---|---|
| 10 | 0 | true failure |
| 9 | 1 | 1 rep left |
| 8 | 2 | 2 reps left |
| 7 | 3 | 3 reps left |
| 5–6 | 4–6 | "a few more" |
| 1–4 | — | warm-up effort |

> **Caveat to surface in-app:** Zourdos et al. state RPE→%1RM "should not be viewed as an absolute
> conversion" — reps at a given %1RM vary between people and day to day. Map RPE→RIR (robust); treat
> RPE→%1RM as an estimate.

### 7.2 Analytics — MET (estimated, never precise)

`Exercise.MetLight` / `Exercise.MetVigorous` (decimal). Standard formula (cite, but author our own MET
reference — the Compendium table is **CC-BY-NC-ND**, blocked; see
[DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §3.9):

```
kcal/min = MET × 3.5 × bodyMassKg / 200
```

Reference values (facts): resistance training ≈ **3.5 MET light–moderate**, **6.0 MET vigorous**; cardio
e.g. jogging ≈ 7, running 6.7 mph ≈ 11 MET. **All calorie figures must be labelled "estimated"** — MET ignores
age/sex/body-comp/fitness, the 3.5 mL/kg/min RMR overestimates for many adults, and resistance EE (intermittent
+ EPOC) is especially hard to estimate. Replaces today's single nullable `EstimatedCaloriesBurn` with a
defensible computed estimate.

### 7.3 `ExerciseSafety`

| Field | Type | Source |
|---|---|---|
| `RequiresSpotter` | bool | NSCA spotting |
| `SpotterTechnique` | enum | None / OverFace / Overhead / Behind / Dumbbell — *NSCA* |
| `NeverSpot` | bool | ballistic/Olympic lifts are never spotted — *NSCA* |
| `Contraindications` | tags (localized note) | e.g. uncontrolled hypertension, acute low-back injury — *ACSM*; framed as "consult a professional", not diagnosis |
| `InjuryRiskNotes` | localized text | pattern-typical risk sites (lumbar/shoulder/knee) |

---

## 8. Localization (Phase 6)

### 8.1 Pattern: separate normalized translation table

Evaluated three approaches — per-language columns (schema churn per language), JSON blob (RDBMS-specific
queries, unwieldy at scale), and a **separate translation table** (add languages with *no* schema change,
normalized, no duplication of invariant fields). **The translation table wins** for an app targeting real
multi-language support at scale.

```
ExerciseTranslation
  ExerciseId    GUID  FK → Exercise.Id
  LanguageCode  BCP-47 ('en','vi','fr','zh-Hans',…)      ── PK: (ExerciseId, LanguageCode)
  Name          text
  Aliases       text[]    -- synonyms/misspellings in that language (powers search)
  Overview      text
  Setup         text
  Execution     text[]    -- ordered steps
  Breathing     text
  Tempo         text
  Cues          text[]
  CommonMistakes text[]
  SafetyNotes   text
  AdvancedNotes text
  Keywords      text[]    -- search synonyms
  SourceVersion int       -- the Exercise.ContentVersion this was translated against
  Status        enum(Draft, Translated, Outdated, Published)
  ReviewedBy    GUID?      ReviewedAt timestamptz
  PRIMARY KEY (ExerciseId, LanguageCode)
```

### 8.2 Locale codes & fallback

Store **BCP-47** tags (ISO 639-1 base: `en`, `vi`, `fr`; add script/region only to disambiguate, e.g.
`zh-Hans`, `pt-BR`). Resolution & fallback: **requested tag → base language → canonical English**
(`fr-CA → fr → en`). A missing specific translation must never render blank.

### 8.3 Localize vs invariant

- **Localize (DB → ExerciseTranslation):** name, aliases, all instruction fields, cues, mistakes, keywords —
  human-authored prose.
- **Invariant (lookup enums/IDs; labels via app i18n bundles, not data):** muscle, equipment, mechanics,
  force, plane, difficulty, type, movement pattern. Store the value once; ship the per-language label in
  Angular/Flutter i18n resources. Avoids duplicating "Chest"/"Barbell" across every exercise and keeps the
  value filterable independent of language.

### 8.4 Plurals, units, RTL

- **Pluralization** via ICU MessageFormat / CLDR — never hand-rolled (languages range from Japanese's single
  `other` to Arabic's six categories).
- **Units** (kg/lb) are a *user preference* distinct from locale; store canonical (kg) and format on display.
  RPE stays `int` (no unit conversion on stored numerics).
- **RTL** future-proofing: use logical (start/end) layout properties so adding Arabic/Hebrew is a flip, not a
  redesign.

### 8.5 Translation workflow & staleness

English (canonical) is the single source of truth and is versioned (`Exercise.ContentVersion`). A translation
is **stale when `ExerciseTranslation.SourceVersion < Exercise.ContentVersion`** → flagged `Outdated`, never
silently published. Use a TMS (Lokalise / Crowdin / Translated) with change detection + versioning + rollback;
**human review for safety-relevant cue text**.

---

## 9. Relationships — the exercise graph (Phase 3 "Relationships" + AI readiness)

`ExerciseRelationship(FromExerciseId, ToExerciseId, RelationType, Note?)`, `RelationType` enum:

- `Variation` — same pattern, different setup (close-grip bench ↔ bench).
- `ProgressionOf` / `RegressionOf` — harder/easier variant (push-up ladder; encodes a difficulty graph per
  pattern — *NASM OPT / progressive overload*).
- `Alternative` / `Substitute` — same training effect, different equipment (powers AI substitution +
  "no equipment? try…").
- `Antagonist` — opposing-pattern pairing (supersets/balance).
- `SimilarTo` — for "you might also like" / search.

This is the structural backbone for **AI coaching, adaptive programming, and injury-aware substitution** —
the field almost no competitor models ([EXERCISE_LIBRARY_RESEARCH.md](EXERCISE_LIBRARY_RESEARCH.md) §2.2).

### 9.1 Future AI fields (forward-compatible, nullable)

- `MovementEmbedding` (vector) — similarity/clustering; store in a `pgvector` column or sidecar table.
- `AiCoachingTags` (text[]) — curated tags for prompt grounding.
- These are **additive and nullable** — no impact on the core model if AI work slips.

---

## 10. Search (Phase: search/filtering)

- **Faceted filters** over the structured enums (muscle, equipment, movement pattern, mechanics, force,
  plane, difficulty, type) — all indexable.
- **Full-text + fuzzy** over name + aliases + keywords + tags + localized name, **per locale**, via a
  materialized `ExerciseSearchDoc(ExerciseId, LanguageCode, tsv tsvector, trigrams)` using PostgreSQL
  `tsvector` (GIN index) + `pg_trgm` for typo tolerance. This fixes today's `LIKE`-on-name limitation and
  closes the **alias search gap** ([EXERCISE_LIBRARY_RESEARCH.md](EXERCISE_LIBRARY_RESEARCH.md) §3).
- **Scale path:** Postgres FTS comfortably covers 5–10k exercises × few languages. Only adopt a dedicated
  engine (OpenSearch/Meilisearch/Typesense) if multi-language relevance ranking or instant-search latency
  later demands it. Don't add it preemptively.
- **Ranking:** boost by `PopularityScore`, exact-name > alias > keyword match.

---

## 11. Data-quality standards (Phase 7)

Validation is enforced at **publish time** (`Status: Draft → Published`). A record cannot be published unless
it passes every gate; failures keep it `Draft`/`InReview` with a checklist of what's missing.

### 11.1 Required-field gates (publish blockers)

Every published exercise MUST have:

- ≥ 1 muscle with `Role = PrimeMover` (and contribution weights summing to ≈ 1.0 ± tolerance);
- `MovementPattern`, `Mechanics`, `ForceType`, `Difficulty`, `Type`, `TrackingType` set;
- ≥ 1 `Equipment` row (bodyweight counts);
- a canonical-locale `ExerciseTranslation` with non-empty `Name`, `Overview`, `Setup`, ≥ 2 `Execution` steps;
- ≥ 1 published `ExerciseMedia` of an image/animation type (see [MEDIA_STRATEGY.md](MEDIA_STRATEGY.md));
- `Source` + `LicenseCode` set (legal-clean guardrail);
- if `RequiresSpotter` or `NeverSpot` is implied by pattern (overhead/back-loaded/ballistic), the safety
  fields must be explicitly set (no silent nulls on risk-bearing lifts).

### 11.2 Uniqueness & integrity

- No duplicate `Slug`; no duplicate alias within a `(Exercise, LanguageCode)`; no duplicate alias *across*
  exercises in the same locale beyond a configured allowlist (catches accidental dupes).
- `ContributionWeight` ∈ [0,1]; per-exercise sum within tolerance of 1.0.
- Relationship graph: no self-loops; `ProgressionOf`/`RegressionOf` must be inverse-consistent.
- Enum referential integrity (FK to lookups).

### 11.3 Content-integrity / review

- Safety-relevant text (`SafetyNotes`, `Contraindications`, spotting) requires `ReviewedBy` (a qualified
  reviewer) before `Published` — **no auto-publish of AI-drafted safety content** (Phase 5 rule).
- Translations failing `SourceVersion == ContentVersion` are flagged `Outdated` and excluded from the
  published locale set.

### 11.4 Where enforced

Domain invariants in the aggregate (as today, e.g. `CreateGlobal` already enforces ≥1 primary muscle);
publish-gate validation as a FluentValidation rule set on the publish command (GymBro convention); and an
import-time validator in the ETL ([IMPORT_PIPELINE.md](IMPORT_PIPELINE.md)). Defense-in-depth, mirroring the
platform's existing layered-validation philosophy.

---

## 12. Provenance & legal-clean by construction

`Source` + `LicenseCode` on every `Exercise` and every `ExerciseMedia` (media has its own license — data and
media are separate regimes). The import pipeline refuses to write a row whose `LicenseCode` is in the
**quarantine set** (`CC-BY-SA-*`, `CC-*-NC-*`, `AGPL`) into the owned master table
([DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §4.5). An attribution/credits view is generated from
rows whose license requires it.

---

## 13. Worked example — Barbell Bench Press (illustrative target record)

```
Exercise: slug=barbell-bench-press, Type=Strength, TrackingType=Strength,
  MovementPattern=HorizontalPush, Mechanics=Compound, ForceType=Push,
  PlaneOfMotion=[Sagittal,Transverse], KineticChain=Closed, Utility=Basic,
  Laterality=Bilateral, Difficulty=Intermediate, PrimaryBodyRegion=UpperBody,
  MetLight=3.5, MetVigorous=6.0, Source=in-house, License=in-house, ContentVersion=1
Muscles: Chest(PrimeMover,0.55), AnteriorDeltoid(Synergist,0.20),
  Triceps(Synergist,0.20), Lats(Stabilizer,0.05)
Equipment: Barbell(Required), Bench(Required)
Programming: Strength(85–100%,1–6,2–6 sets,2–5min); Hypertrophy(67–85%,6–12,30–90s)
Safety: RequiresSpotter=true, SpotterTechnique=OverFace, NeverSpot=false
Relationships: Variation→close-grip-bench; Regression→dumbbell-bench / push-up;
  Alternative→machine-chest-press; Antagonist→barbell-row
Translation[en]: Name="Barbell Bench Press", Aliases=["bench","flat bench press"],
  Overview/Setup/Execution[…]/Breathing/Cues/CommonMistakes/SafetyNotes (authored, reviewed)
Translation[vi]: Name="Đẩy ngực với tạ đòn", … (SourceVersion=1, Status=Published)
Media: image(card/detail/hero renditions), animation(lottie or muted mp4/webm) — each license-tagged
```

Compare to today's record: a single `MuscleGroup=Chest` row, `MovementType=Compound`, one `ImageUrl`, flat
instruction steps, English only — see [MIGRATION_PLAN.md](MIGRATION_PLAN.md) for the gap and the path.

---

## Sources

Sports-science grounding (values are facts; cite, do not copy prose/tables —
[DATA_SOURCE_COMPARISON.md](DATA_SOURCE_COMPARISON.md) §3.8):
- NSCA load/rep/rest continuum (CSCS/Essentials): https://www.ptpioneer.com/personal-training/certifications/nsca-cpt/nsca-cpt-chapter-15/ ,
  https://www.themovementsystem.com/blog/cscs-program-design-chart ,
  https://www.nsca.com/globalassets/education/articles/coaches/12.3/nsca-coach-12.3.8_using-intensity-based-on-sets-and-repetitions.pdf
- ACSM Position Stand, *Progression Models in Resistance Training* (Med Sci Sports Exerc 2009;41(3):687–708):
  https://pubmed.ncbi.nlm.nih.gov/19204579/ , https://acsm.org/science-spotlight-acsm-releases-new-position-stand-on-resistance-training/
- RPE↔RIR (Zourdos et al. 2016): https://pmc.ncbi.nlm.nih.gov/articles/PMC4961270/ ,
  https://massresearchreview.com/2023/05/22/rpe-and-rir-the-complete-guide/
- MET definition/formula & values (2024 Adult Compendium — values factual; table is CC-BY-NC-ND, not shipped):
  https://pmc.ncbi.nlm.nih.gov/articles/PMC10818145/ , https://pacompendium.com/conditioning-exercise/ ,
  RT energy-expenditure caveats: https://link.springer.com/article/10.1007/s40279-024-02047-8
- Training-status / difficulty (NSCA 2021 SCJ): https://journals.lww.com/nsca-scj/fulltext/2021/10000/classification_and_determination_model_of.7.aspx
- Spotting/safety (NSCA): https://www.ptpioneer.com/personal-training/certifications/nsca-cpt/nsca-cpt-chapter-13/ ,
  https://www.nsca.com/contentassets/de9aebfe7a7340b69217b99bb13862a7/basics_of_strength_and_conditioning_manual.pdf
- Classification taxonomies: planes (NASM) https://blog.nasm.org/exercise-programming/sagittal-frontal-traverse-planes-explained-with-exercises ;
  kinetic chain https://www.physio-pedia.com/Closed_Chain_Exercise ; muscle roles (OpenStax A&P)
  https://open.oregonstate.education/aandp/chapter/11-1-describe-the-roles-of-agonists-antagonists-and-synergists/ ;
  movement patterns (Dan John / functional literature — NOT a governing-body standard)
  https://www.otpbooks.com/dan-john-5-basic-human-movements/ , https://www.strongfirst.com/seven-basic-human-movements/

Localization & DB design:
- Translation-table vs JSON vs per-column: https://www.red-gate.com/blog/multi-language-database-design/ ,
  https://dev.to/dwarvesf/database-designs-for-multilingual-apps-4jb6 ,
  https://aspnetzero.com/blog/mastering-multi-lingual-database-design-in-asp.net-core-with-ef-core
- BCP-47 & fallback: https://developer.mozilla.org/en-US/docs/Glossary/BCP_47_language_tag ,
  https://en.wikipedia.org/wiki/IETF_language_tag
- CLDR plurals/units: https://cldr.unicode.org/translation/getting-started/plurals , https://www.i18next.com/translation-function/plurals
- Translation versioning/staleness: https://translated.com/resources/translation-version-control-content-management-change-tracking ,
  https://lokalise.com/blog/headless-cms-localization-tools/

**Caveats:** "Movement pattern" is coaching literature (Dan John / functional training), **not** an
ACSM/NSCA/WHO standard — attribute accordingly. The ACSM Position Stand PDF returned as binary; confirm any
shipped numeric from the PubMed record / journal page. NSCA and ACSM differ on novice strength %1RM — store
both with source labels, do not blend.
