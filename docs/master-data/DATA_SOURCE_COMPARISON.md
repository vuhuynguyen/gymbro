# Data Source Comparison & Sourcing Strategy

> **Phase covered:** 2 (trusted data sources). **Mandate:** licensing claims are verified against the actual
> repo/site and cited in *Sources*. **No exercise data is invented.**

The single most important constraint on the entire master-data program is **licensing**. A field model is
worthless if the data filling it cannot be legally shipped, modified, and redistributed inside a commercial
SaaS. This document evaluates every candidate source on accuracy, licensing, commercial usability,
completeness, freshness, API, media, localization, and metadata quality — then recommends a concrete sourcing
strategy that keeps the dataset **legally clean** (no copyleft or non-commercial contamination).

---

## 1. The headline finding

> **Only one sizeable source is cleanly usable for a closed, commercial product with zero strings: the
> `yuhonas/free-exercise-db` _data_ (The Unlicense / public domain). But its bundled _images_ carry a
> separate CC-BY-SA 3.0 share-alike caveat that must be resolved.**

Everything else is either copyleft (wger CC-BY-SA, exercisedb-api AGPL), non-commercial (Compendium of
Physical Activities CC-BY-NC-ND), proprietary (ExRx), partnership-gated (Technogym), or provenance-risky
(scraped Kaggle sets).

**Golden rule:** treat **data** and **media** as two independent license regimes. The free-exercise-db split
(public-domain JSON, share-alike images) is the textbook example.

---

## 2. License risk summary

| Source | License | Commercial OK? | Key obligation / blocker |
|---|---|---|---|
| **free-exercise-db (data)** | **Unlicense (public domain)** | ✅ Yes | None |
| free-exercise-db (images) | CC-BY-SA 3.0 (Everkinetic) | ✅ Yes | Attribution + **share-alike** |
| wger (data) | CC-BY-SA 4.0 | ✅ Yes | Attribution + **share-alike (copyleft)** |
| wger (app code) | AGPL-3.0 | ✅ Yes | Network copyleft (irrelevant if we don't run their app) |
| exercisedb-api (GitHub repo) | **AGPL-3.0** | ✅ Yes | **Network copyleft — blocker for closed SaaS** |
| exercisedb.io (paid dataset) | Proprietary EULA | ✅ Yes | One-time fee; no resell/redistribute raw |
| RapidAPI ExerciseDB | Proprietary / RapidAPI | Paid only | Per-call dependency, ~$10–50+/mo |
| ExRx.net | Proprietary | License-to-buy | Negotiated cost; **no scraping** |
| Kaggle (CC0-tagged sets) | CC0 (but scraped) | ⚠️ Risky | Unverifiable upstream provenance |
| Wikimedia Commons | Per-file CC0/BY/BY-SA | ✅ Yes | Varies; **avoid BY-SA** |
| NIH / MedlinePlus | Public domain (mixed) | ✅ Yes (PD parts) | Exclude A.D.A.M./ASHP copyrighted embeds |
| ACSM / NSCA / NASM / ACE | Copyrighted publications | Facts only | Don't copy tables/branded models (OPT) |
| Compendium of Physical Activities (MET) | **CC-BY-NC-ND 4.0** | ❌ **No** | **NC + ND blocker** |
| Technogym / Life Fitness | Partner-gated API | Partnership | Not a content library |
| Muscle-map SVG libs (body-highlighter et al.) | MIT (mostly) | ✅ Yes | Attribution in license file |

---

## 3. Per-source evaluation

### 3.1 free-exercise-db (`yuhonas/free-exercise-db`) — TOP SEED CANDIDATE

- **Data license:** **The Unlicense** (verified in `LICENSE.md`) — full public-domain dedication, commercial
  use unconditional, no attribution, no share-alike, no NC.
- **⚠️ Image caveat:** images derive from **Everkinetic**, licensed **CC-BY-SA 3.0 Unported** (mirrored on
  Wikipedia). The repo's Unlicense covers the JSON, not the upstream images. Treat images as CC-BY-SA →
  attribution + share-alike, or replace them.
- **Completeness:** **873 exercises** (counted in `dist/exercises.json`). Fields: id, name, force, level
  (difficulty), mechanic, equipment, primaryMuscles, secondaryMuscles, instructions[], category, images[].
- **API:** none — static JSON (per-exercise + combined + NDJSON for Postgres). Self-host; images via GitHub
  raw.
- **Freshness:** community/PR-driven, irregular.
- **Localization:** English only.
- **Verdict:** **Use the data as the seed base.** Replace or independently re-license the images.

### 3.2 wger — best metadata + localization, but SHARE-ALIKE RISK

- **Data:** **CC-BY-SA 4.0** (some entries 3.0), per-record `license` + `licenseAuthor`. App code AGPL-3.0.
- **Obligations:** attribution per entry **+ share-alike** — any derivative *dataset* you distribute must
  also be CC-BY-SA. Displaying in-app with attribution is fine; **shipping a modified blended dataset**
  triggers copyleft, which would force GymBro's master table to become CC-BY-SA.
- **Completeness:** large, multilingual; aliases, muscles, equipment, HTML descriptions, image galleries,
  video links. Free public REST API; self-hostable.
- **Verdict:** **Do not blend into the owned master table.** Acceptable only as an isolated, attributed,
  CC-BY-SA reference store, or for cross-checking. Its *schema* (not its data) is a fine design reference.

### 3.3 ExerciseDB — three distinct things, do not confuse

- **(a) `ExerciseDB/exercisedb-api` GitHub repo:** **AGPL-3.0**. AGPL's §13 network copyleft means deploying
  it as a service obliges you to offer corresponding source — **hard blocker for a closed-source backend**
  unless fully isolated.
- **(b) exercisedb.io paid dataset:** proprietary EULA — **commercial use allowed, perpetual, one-time
  purchase**, 11,000+ exercises with GIFs/images/instructions. Cannot resell/redistribute the raw files as a
  competing library.
- **(c) RapidAPI ExerciseDB:** hosted API, free tier non-commercial/rate-limited, paid tiers for commercial
  (~$10–50+/mo); a per-call dependency, not ownership.
- **Verdict:** the **exercisedb.io paid dataset** is the cleanest *paid* option for breadth + animated media
  fast (no copyleft). Avoid the AGPL repo. RapidAPI is fine for prototyping only.

### 3.4 ExRx.net — proprietary, license-to-buy

- Fully proprietary. ToU prohibits modifying/selling/republishing without written permission and **prohibits
  scraping (incl. for AI training)**. Commercial use only via their paid **Exercise JSON REST API** (apply
  via inquiry form; Bearer token; pricing negotiated, not public).
- **Verdict:** highest-quality authoritative taxonomy, but closed — you license a feed, you don't own it.
  Consider only as a premium API if budget allows. **Never scrape.**

### 3.5 Kaggle exercise datasets — case-by-case, mostly risky

- E.g. "Gym Exercise Dataset" (niharika41298): tagged **CC0**, 2,500+ exercises, but **scraped from "various
  internet sources"**, null-heavy. A CC0 tag does **not** launder upstream copyright; provenance is
  unverifiable.
- **Verdict:** inspiration/cross-reference only. **Do not seed a commercial product from scraped Kaggle
  data.**

### 3.6 Wikimedia Commons — mixed, read each file

- Per-file: CC0/PD → CC-BY → CC-BY-SA. All allow commercial use, but obligations differ (BY = attribution;
  BY-SA = attribution + share-alike; CC0/PD = none). Attribute with **TASL** (Title-Author-Source-License).
- **Verdict:** good free image source **if filtered to CC0/PD and CC-BY only**; skip CC-BY-SA. (The
  Everkinetic images in free-exercise-db are the CC-BY-SA ones mirrored here — same caveat.)

### 3.7 NIH / NLM / MedlinePlus — partially public domain

- U.S. government works generally public domain, but MedlinePlus is **mixed**: NLM-authored content is PD;
  embedded third-party (A.D.A.M. Medical Encyclopedia, ASHP monographs) is copyrighted, licensed only for
  MedlinePlus.
- **Verdict:** good for authoritative **health/safety copy** and PD anatomical illustrations (look for the
  "U.S. National Library of Medicine" watermark; acknowledgment requested). Not an exercise-catalog source.
  Exclude A.D.A.M. content.

### 3.8 ACSM / NSCA / NASM / ACE — guidelines, not datasets

- Certification bodies/publishers, not licensable feeds. Textbooks, the NASM **OPT** model, ACSM
  *Guidelines*, NSCA load charts are **copyrighted**.
- **Usable:** the underlying *facts and ideas* (e.g. "hypertrophy ~6–12 reps", %1RM↔reps relationships) are
  **not copyrightable** — implement the science in your own words/values. The *specific expression* (exact
  tables, charts, branded models) is protected.
- **Verdict:** use as **scientific reference to author your own** programming/safety values in-house. Cite for
  structure; never reproduce tables verbatim or use branded frameworks. See sports-science grounding in
  [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §Programming/§Safety.

### 3.9 Compendium of Physical Activities (MET values) — NC + ND BLOCKER

- **License:** **CC-BY-NC-ND 4.0** (verified, 2024 Adult Compendium). **NC** blocks commercial use of the
  compiled table; **ND** blocks modification. Email for other uses.
- **Distinction:** the MET *numbers* are **facts** (not copyrightable); the **curated table** is the licensed
  work. For a commercial calorie estimator, derive MET values from primary physiology literature / licensed
  sources, or obtain written permission — **do not ship the Compendium table**.
- **Verdict:** author your own MET reference from facts/primary sources, or get permission.

### 3.10 Machine manufacturer data (Technogym, Life Fitness) — partnership-gated

- Technogym has a developer portal + Mywellness Cloud / Enterprise API, but access **requires a business
  partnership**; it's a telemetry/integration API (user results, device data), **not a content library**.
  Life Fitness etc. similar.
- **Verdict:** not a seeding source. Relevant only for future equipment integrations.

### 3.11 Open muscle-model / SVG assets — MIT, clean

- `body-highlighter` (framework-agnostic), `react-native-body-highlighter`, `react-muscle-highlighter`,
  `vue-human-muscle-anatomy`, `MuscleMap` (SwiftUI) — multiple **MIT**-licensed SVG muscle maps. The polygon
  data is shared/forked across them.
- **Verdict:** excellent clean source for the interactive **muscle-map UI** (port SVG paths to Angular and
  Flutter). MIT = attribution in license file, no copyleft — fully compatible with a closed product. Verify
  each repo's LICENSE before adopting.

---

## 4. Recommended sourcing strategy

### 4.1 Seed base — `free-exercise-db` data (public domain)

Start the catalog from the **873-exercise free-exercise-db JSON** (The Unlicense). It is the only sizeable
catalog with zero attribution/copyleft strings, with structured fields (force, mechanic, equipment, level,
primary/secondary muscles, instructions, category) that map directly onto the proposed model. This becomes
the owned, modifiable, redistributable foundation.

### 4.2 Images — resolve the share-alike caveat (most important action)

Do **not** assume the bundled images are public domain. Choose one:

1. **Commission / generate / shoot our own** exercise imagery & animations — cleanest, full ownership
   (recommended for the brand-facing core library). See [MEDIA_STRATEGY.md](MEDIA_STRATEGY.md).
2. **License the exercisedb.io paid dataset** — 11,000+ exercises + GIFs, commercial-OK, one-time fee, no
   copyleft — fastest path to rich animated media.
3. **Keep CC-BY-SA images only if** you accept per-image attribution and keep them as discrete *displayed*
   assets, never merged into a redistributed derivative dataset.

### 4.3 Muscle-map UI — MIT SVG highlighter

Adopt an MIT-licensed SVG muscle highlighter (e.g. `body-highlighter` polygons); port paths for Angular and
Flutter. Clean and commercial-safe.

### 4.4 Scientific reference values — author in-house

Rep ranges, %1RM, rest, RPE↔RIR, MET/calorie values: **author in-house** from the underlying facts (read
ACSM/NSCA/NASM/ACE + primary literature for the science, write our own values/copy; never paste tables or use
branded models). For MET/calorie estimation, **do not ship the Compendium table** — derive a MET reference
from facts/primary sources or obtain permission.

### 4.5 Keep the dataset legally clean (enforced in schema)

- **Per-row provenance:** every exercise row carries `source` + `license` from day one (see
  [MASTER_DATA_ARCHITECTURE.md](MASTER_DATA_ARCHITECTURE.md) §Provenance, and the same for media assets).
- **Quarantine by license:** never merge CC-BY-SA (wger) or NC (Compendium) content into the public-domain /
  owned master table. If ingested, keep in a separate, clearly-licensed store.
- **Data ≠ media** are separate license regimes — track both.
- Prefer **Unlicense / CC0 / MIT / PD** for anything to be modified, blended, and shipped as our own.
- Maintain an **attribution/credits page** for any CC-BY assets used.

### 4.6 Bottom line

> **free-exercise-db data (Unlicense)** + **our own or exercisedb.io-licensed images** + **MIT muscle-map
> SVG** + **in-house-authored science values** = a fully commercial, modifiable, redistributable dataset with
> **no copyleft or NC contamination**, expandable toward the 5,000–10,000+ target via the import pipeline in
> [IMPORT_PIPELINE.md](IMPORT_PIPELINE.md).

---

## Sources

- free-exercise-db repo & license & count: https://github.com/yuhonas/free-exercise-db ,
  https://raw.githubusercontent.com/yuhonas/free-exercise-db/main/LICENSE.md (The Unlicense) ,
  https://github.com/yuhonas/free-exercise-db/blob/main/dist/exercises.json (873) ,
  https://yuhonas.github.io/free-exercise-db/ (Everkinetic / CC-BY-SA 3.0 image note)
- wger: https://wger.de/en/software/api , https://github.com/wger-project/wger (AGPL app, CC-BY-SA data) ,
  https://wger.readthedocs.io/
- ExerciseDB repo (AGPL): https://github.com/ExerciseDB/exercisedb-api ,
  https://github.com/ExerciseDB/exercisedb-api/blob/main/LICENSE ; paid set: https://exercisedb.io/faq ;
  RapidAPI: https://rapidapi.com/justin-WFnsXH_t6/api/exercisedb/pricing
- ExRx licensing/terms: https://exrx.net/Notes/License , https://exrx.net/Notes/Legal ,
  https://exrx.net/Store/Other/Licensing
- Kaggle example: https://www.kaggle.com/datasets/niharika41298/gym-exercise-data (CC0 tag, scraped)
- Wikimedia Commons licensing & reuse: https://commons.wikimedia.org/wiki/Commons:Licensing ,
  https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia
- NIH/NLM/MedlinePlus: https://medlineplus.gov/about/using/usingcontent/ , https://www.nlm.nih.gov/web_policies.html
- ACSM/NSCA copyrighted reference example: https://www.nsca.com/contentassets/61d813865e264c6e852cadfe247eae52/nsca_training_load_chart.pdf
- Compendium of Physical Activities (CC-BY-NC-ND): https://pacompendium.com/ ,
  https://pmc.ncbi.nlm.nih.gov/articles/PMC10818106/
- Technogym/Mywellness APIs (partner-gated): https://developer.technogym.com/ , https://openplatformdocs.mywellness.com/
- MIT muscle-map libraries: https://github.com/lahaxearnaud/body-highlighter ,
  https://github.com/HichamELBSI/react-native-body-highlighter ,
  https://github.com/soroojshehryar/react-muscle-highlighter , https://github.com/LucaWahlen/vue-human-muscle-anatomy

**Caveat:** ExRx pages return HTTP 403 to automated fetch; ExRx licensing terms above are from its license/legal
pages as reported. The free-exercise-db image provenance (Everkinetic CC-BY-SA 3.0) is stated on the project's
own GitHub Pages frontend — confirm per-image before shipping any of those images.
