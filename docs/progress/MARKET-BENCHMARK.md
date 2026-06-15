# Progress Page — Market Benchmark & Feasibility Audit

> **Status:** **Research only — no app code.** A competitive benchmark of GymBro's progress reporting against ~45
> products across five categories (2024–2026), with every candidate improvement verified against the **real schema**
> (file:line). It does not change the frozen design; it explains *where GymBro stands*, *what the honest gaps are*,
> and *what each gap would cost to close*. The forward-looking AI proposal it points to is [AI-NARRATIVE.md](AI-NARRATIVE.md).
> The never-fabricate stance it confirms is the one in [SPEC.md §A](SPEC.md) and [IMPLEMENTATION.md §11](IMPLEMENTATION.md).

**Related:** [SPEC.md](SPEC.md) · [FEASIBILITY.md](FEASIBILITY.md) · [ROADMAP.md](ROADMAP.md) · [AI-NARRATIVE.md](AI-NARRATIVE.md) · [IMPLEMENTATION.md](IMPLEMENTATION.md)

---

## Bottom line

1. **GymBro's honest analytics already match or beat every competitor's.** The per-lift e1RM chart + stall badge,
   adherence ring, consistency heatmap, named PR teaser, bodyweight trend, and coach roster triage equal Strong/Hevy/
   Boostcamp and exceed most coach platforms on strength specifically. Only TrainHeroic and Bridge Athletic match the
   e1RM depth on the coach side.
2. **The four "never fabricate" bans are a moat, not a limitation.** Each maps onto the market's weakest, least-
   defensible feature, and the 2024–2026 evidence vindicates refusing all four (§5).
3. **The real gaps are packaging, not data:** no auto-recap, no exports, no org rollup, no AI narrative, no per-muscle
   volume view. **17 of 21 candidate features are computable today with zero migration** (§4).
4. **The one genuinely open lane is an LLM narrative grounded in the user's own numbers** — no strength logger ships
   it. See [AI-NARRATIVE.md](AI-NARRATIVE.md).

---

## 1. Method

Six parallel research passes (strength loggers; AI/algorithmic apps; B2B coach platforms; wearable/recovery
ecosystems; strength-standards tools; a code-grounded feasibility map). Every market claim was URL-cited and vendor
marketing separated from independently-verified capability. Every feature verdict was checked against the actual EF
entities, not the design docs' claims. Sources are listed in §8.

---

## 2. The comparison matrices

### Table A — Honest, descriptive analytics

| Product | e1RM/strength trend | Volume/load | Muscle balance | Adherence/streak | PR recap | Auto weekly/annual recap |
|---|---|---|---|---|---|---|
| Strong | ✓ line | ✓ tonnage | tables | — | ✓ | — |
| Hevy | ✓ | ✓ multi-window | ✓ body map | ✓ weekly streak | ✓ rich | ✓ Year-in-Review |
| Boostcamp | ✓ | ✓ | ✓ body diagram | ✓ Sunday report | ✓ | ✓ weekly + annual |
| JEFIT | ✓ | ✓ vs target line | ✓ BodyMap | reminders | ✓ | — |
| Fitbod | ✓ | ✓ | recovery heatmap\* | basic | ✓ | — |
| TrainHeroic (coach) | ✓ team+athlete | ✓ | — | ✓ compliance | ✓ | — |
| Bridge Athletic (coach) | ✓ prescribed-vs-actual | ✓ sRPE load | — | ✓ compliance | leaderboards | coach-run |
| **GymBro (built)** | ✓ per-lift chart + stall | ✓ acute/chronic (coach) | **— (gap)** | ✓ ring + heatmap | ✓ named teaser | **— (gap)** |

\* Fitbod's "muscle recovery %" is a volume×time-decay heuristic, not physiology — see Table C.

### Table B — AI & enterprise capabilities (the packaging gaps)

| Product | LLM narrative | Coach roster triage | Automated reports | Exports (PDF/CSV) | Org/location rollup | White-label |
|---|---|---|---|---|---|---|
| Trainerize | light | ✓ auto-tag at-risk | weekly summary | ⚠ blocks progress export | ✓ per-trainer | ✓ Studio+ |
| TrueCoach | — | ✓ "Needs Attention" (−20% drop) | — | weak (CSV+txt) | limited | — |
| TeamBuildr | — | color-coded warnings | coach-run | ✓ 16+ reports, PDF/CSV/Excel | ✓ unlimited coaches | — |
| Bridge Athletic | — (Kitman partner) | ✓ Enterprise-only | coach-run | ✓ CSV | ✓ department-wide | — |
| Everfit | CV rep-count; "at-risk AI" *(aspirational)* | marketed | — | — | Studio/Ent | ✓ Enterprise |
| Whoop / Oura / Strava | ✓ **real** (GPT-4 / Advisor / Athlete Intelligence) | n/a | ✓ recaps | — | n/a | n/a |
| **GymBro (built)** | **— (the open lane)** | ✓ onTrack/drifting/quiet | **— (gap)** | **— (gap)** | **— (gap)** | — |

### Table C — The fabrication line (who crosses it)

| Product | Readiness/recovery score | Strength percentile / standards | Cross-user leaderboard | Honesty assessment |
|---|---|---|---|---|
| Whoop/Oura/Garmin/Fitbit | ✓ **sensor-based** (HRV/sleep) | — | — | Legit signals, but the **composite scores are unvalidated** |
| JEFIT / Fitbod | ⚠ **fabricated from logs** | — | — | "Recovery %" with no sensor = pseudo-precision |
| Juggernaut / Volt / Kahunas | ⚠ self-report "readiness" | — | — | Honest *that they ask*; not measured |
| Caliber / Boostcamp | — | ✓ "Strength Score" (DOTS/age-sex norm) | — | Opaque normative composite |
| Strength Level | — | ✓ self-reported percentile | implicit | Self-report + selection bias (~10–20% inflation) |
| Hevy / Gravitus / TrainHeroic | — | — | ✓ leaderboards | Engagement bait; social-comparison risk |
| **GymBro** | ⛔ **banned** | ⛔ **banned** | ⛔ **banned** | **The only fully-honest column** |

---

## 3. What each cluster taught us

- **Strength loggers (Strong, Hevy, Boostcamp, JEFIT, Fitbod, Gravitus, Setgraph, FitNotes, Caliber):** their *honest*
  analytics are arithmetic over logs (e1RM, volume, PR detection) — which GymBro already does. Their differentiation is
  **presentation**: weekly/annual auto-recaps (Hevy "Year in Review", Boostcamp Sunday report), per-muscle volume body
  diagrams, an estimated rep-max table off one e1RM (FitNotes), a switchable chart metric, and explicit "you-then vs
  you-now" framing (Setgraph). All buildable on existing data.
- **AI/algorithmic apps (Fitbod, Juggernaut, RP, Dr. Muscle, Volt, Freeletics, Future, Peloton IQ, Aaptiv, Train
  Fitness):** almost none is generative ML acting as a calculator — the dominant pattern is **deterministic
  autoregulation wearing an "AI" skin**. The genuinely new 2024–2026 development is a thin **LLM conversational/
  narrative layer** (Freeletics Coach+, Aaptiv AVA, Fitbit Gemini). Freeletics shows the right division of labor:
  **ML generates the plan, the LLM talks about it** — and the independent LLM-training-plan study found raw LLM plans
  "moderate quality… require expert revision," which is the strongest reason to keep GymBro's **LLM-as-writer, not
  calculator** rule.
- **B2B coach platforms (Trainerize, TrueCoach, Everfit, TeamBuildr, CoachRx, TrainHeroic, Volt, My PT Hub, Kahunas,
  Exercise.com, Bridge):** "enterprise-grade reporting" = **multi-format exports, org/per-location rollups, automated
  periodic reports, white-label, and a published security posture**. GymBro's multi-tenant coach model is
  *architecturally ahead* of single-coach tools but under-packaged. Two specific openings: **exports + automated
  weekly reports** (Trainerize *blocks* progress export and an automated report is a top unfilled request on its own
  forum), and a **published SOC 2** (I could not confirm SOC 2 from the *own site* of any of the 12 platforms).
  **(PUSH/PUSHportal is defunct — acquired by WHOOP in 2021 and shut down.)**
- **Wearable/recovery ecosystems (Whoop, Oura, Garmin, Apple, Strava, Polar, Coros, Ultrahuman, Eight Sleep, Fitbit):**
  the portable, sensorless lesson is **"vs. your own rolling baseline"** (Apple Vitals "typical range", Polar 28-day,
  Strava 30-day, Coros 42-day base vs 7-day acute) plus **LLM recaps grounded in your own data** with an explicit
  *"this can be wrong"* disclaimer (Oura) and **zero-retention** LLM privacy (Whoop). The proprietary readiness/recovery
  *scores* are the part to leave alone (§5).
- **Strength-standards tools (Strength Level, OpenPowerlifting, Symmetric Strength, ExRx/Kilgore, DOTS/Wilks):** the
  only legitimately reusable normative dataset is **OpenPowerlifting** (public-domain data; AGPL *code*), and it
  describes *competitors*, not gym-goers. Everything else is self-reported (biased), copyrighted, or both. **DOTS as a
  personal index over time** is the honest, non-normative slice worth adopting; percentiles are not.

---

## 4. Feasibility — code-verified (the authoritative ground-truth)

Verified against the real entities. **17 of 21 candidates are data-unblocked.**

### Tier 1 — Computable NOW, zero migration (new read queries only)

| Feature | Grounding (file:line) |
|---|---|
| Auto weekly + annual "Year in Lifting" recap (sessions, volume, PRs, adherence) | `WorkoutSession.Status/StartedAt/PrCount`; `PerformedSet.WeightKg×Reps` |
| Per-muscle weekly volume vs target / body diagram | needs live join `PerformedExercise.ExerciseId → Exercise.Muscles` — **no muscle field on performed rows** (`Exercise/Entities/ExerciseMuscle.cs:5`); costly but no migration |
| "You-then vs you-now" delta framing + multi-window (7/28/90d) trends | arithmetic over existing series |
| Plateau/stall description extended to volume-load | windowed MAX over `PerformedSet` |
| Switchable chart metric (weight / volume / e1RM / duration) | all four exist (`PerformedSet.cs:17,18,19,30`) |
| Estimated rep-max table (2–10RM) from stored e1RM | reverse-Epley from `EstimatedOneRepMaxKg` |
| Trainee self-view acute/chronic load (coach version already exists) | volume by `WorkoutSession.StartedAt` window |
| "Vs your typical range" (Apple-Vitals pattern) for any MetricEntry | `MetricEntry.{Type,Value,LocalDate}` (`Nutrition/Entities/MetricEntry.cs:25-29`) |
| Coach: named −20% adherence-drop trigger + check-in two-point comparison | `PlanAssignment.FrequencyDaysPerWeek` (`WorkoutPlan/Entities/PlanAssignment.cs:12`) + completion |
| Per-coach / per-location / org rollup | tenant already scoped on every entity; aggregation only |

### Tier 2 — Needs new non-data infrastructure (data already exists)

| Feature | What it needs |
|---|---|
| **LLM weekly progress narrative** (the open lane) | LLM service + grounding + span-check. **No schema change.** → [AI-NARRATIVE.md](AI-NARRATIVE.md) |
| Automated periodic coach reports | scheduled-job + notification + templating |
| Exportable PDF/CSV reports | PDF/CSV lib + templates + endpoint |
| NL Q&A over own history | text-to-SQL + strict tenant isolation (highest risk) |

### Tier 3 — Needs a migration / new data source

- **Wearable ingestion.** ⚠ **Doc-accuracy correction:** `MetricEntry` has **no `Source` field** — only
  `Type/Value/Unit/LocalDate/LoggedAtUtc/TraineeId` (`Nutrition/Entities/MetricEntry.cs:17-30`). The Phase-5 framing
  in [IMPLEMENTATION.md §11](IMPLEMENTATION.md) ("a `Source=wearable` writer is the only missing piece") **overstates
  readiness**: provenance is not tracked today. A wearable writer needs either a new `Source` column (migration) **or**
  a `Type` naming convention (`"wearable:hrv"`). The series endpoint generalizes by free-text `Type`; it does not tag
  source. (Contrast `DailyNutritionLog.Source`, which *does* exist.)

### ⛔ Never-fabricate (confirmed correct)

Readiness/recovery score, strength percentile, ACWR ratio. No HRV/sleep/RHR fields; no normative dataset; the
literature rejects all three (§5). Also: `WorkoutSession.DurationSeconds` is **wall-clock incl. rest** and
`RpeOverall` is **integer + sparse**, so session-RPE load and "training density" are low-signal — keep load
**volume-based**, as already built.

---

## 5. The never-fabricate stance, confirmed by evidence

| Banned thing | Why the ban is correct |
|---|---|
| **Readiness/recovery score** | A 2025 peer-reviewed review (*Translational Exercise Biomedicine*) analyzed 14 composite scores across 10 manufacturers: **none discloses its formula, none is validated against clinical outcomes**, and the same body yields different scores on different devices. Even the sensor companies can't validate these — fabricating one from logs alone is indefensible. |
| **ACWR injury ratio** | Formally debunked (Lolli/Impellizzeri): a *randomized* chronic load predicted injury as well as the real ratio; "time to dismiss ACWR." Keep acute & chronic as **two separate volumes**, never a ratio. |
| **Strength percentile** | The only open normative dataset (OpenPowerlifting) is *competitors only*; self-report databases (Strength Level) carry ~10–20% inflation + selection bias; ExRx/Kilgore tables are copyrighted expert judgment, not a sampled distribution. |
| **Cross-user leaderboards** | Documented social-comparison / disordered-behavior risk; contradicts self-referenced progress. |

**The one honest middle-path (optional):** if normative context is ever wanted, the *only* defensible form is an
**opt-in, off-by-default** "competition context" on public-domain OpenPowerlifting data with an unmissable
*"sanctioned-meet competitors only, not the general population"* caveat and **DOTS** as the index. Recommendation:
keep both bans; ship **DOTS-as-a-personal-index over time** (zero normative claim) instead, which captures most of the
value. Note OPL's *code* is AGPLv3 — ingest the *data* (public domain), never the code.

---

## 6. Recommended priorities (ranked by leverage)

**Buildable now (zero migration):**
1. **Auto weekly recap + annual "Year in Lifting"** — highest retention-per-effort; pure description of owned data.
2. **Per-muscle weekly volume vs goal** (accept the 3-table join cost) — closes the most visible analytics gap.
3. **Coach: named, explainable adherence-drop trigger** + **two-point check-in comparison** — extends the shipped roster.
4. **Switchable chart metric** + **estimated rep-max table** + **"then vs now" delta** — cheap UX multipliers.
5. **Per-coach / per-location org rollup** ("this gym only") — the multi-tenant advantage, under-used.

**Needs infra (data ready):**
6. **LLM weekly narrative** — the differentiation lane. Spec: [AI-NARRATIVE.md](AI-NARRATIVE.md).
7. **Exportable PDF/CSV + automated weekly report** — enterprise table-stakes; treat data portability as a feature.

**Strategic (non-feature):**
8. **Published SOC 2 + audit log** — a genuine enterprise differentiator in a category full of unverified badges.

---

## 7. Doc-accuracy corrections surfaced by this pass

1. **`MetricEntry` has no `Source` field** — §4 Tier 3; [IMPLEMENTATION.md §11](IMPLEMENTATION.md) overstates wearable readiness.
2. **Per-muscle analytics need a live 3-table join** (no denormalized muscle on performed rows) — consistent with
   [FEASIBILITY.md §3.2](FEASIBILITY.md), but flag the real query cost before building the muscle-balance view.

---

## 8. Sources (representative)

- Composite-score validity: [Translational Exercise Biomedicine 2025](https://www.degruyterbrill.com/document/doi/10.1515/teb-2025-0001/html)
- ACWR debunked: [Lolli/Impellizzeri (conceptual pitfalls)](https://pubmed.ncbi.nlm.nih.gov/32502973/)
- e1RM formula accuracy (±5% ≤10 reps): [OpenSIUC validation study](https://opensiuc.lib.siu.edu/cgi/viewcontent.cgi?article=1744&context=gs_rp)
- LLM coaching hallucination + RAG grounding: [JMIR 2025 scoping review](https://www.jmir.org/2025/1/e79217)
- OpenPowerlifting data license: [github.com/sstangl/openpowerlifting](https://github.com/sstangl/openpowerlifting) · DOTS vs Wilks: [IPF 2020 eval](https://arxiv.org/pdf/1903.10694)
- Whoop Coach (GPT-4) + zero-retention: [openai.com/whoop](https://openai.com/index/whoop/) · Oura Advisor: [ouraring.com/blog/oura-advisor](https://ouraring.com/blog/oura-advisor/) · Strava Athlete Intelligence: [press.strava.com](https://press.strava.com/articles/stravas-athlete-intelligence-translates-workout-data-into-simple-and)
- Trainerize compliance/triage: [trainerize.com](https://www.trainerize.com/blog/trainerize-update-drive-client-and-trainer-results-with-compliance-and-engagement-metrics/) · TeamBuildr exports: [exercise.com](https://www.exercise.com/grow/how-much-does-teambuildr-cost/) · TrueCoach dashboard: [truecoach.co](https://truecoach.co/features/dashboard/)
- Hevy Year-in-Review: [hevyapp.com](https://www.hevyapp.com/features/year-in-review/) · Fitbod recovery model: [fitbod.me](https://fitbod.me/blog/muscle-recovery/) · Freeletics Coach+: [insider.fitt.co](https://insider.fitt.co/press-release/freeletics-unveils-a-new-era-in-digital-fitness-with-the-launch-of-coach/)

*Full per-cluster source lists were gathered across six research passes; the above are the load-bearing citations.*
