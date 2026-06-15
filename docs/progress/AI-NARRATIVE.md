# Progress Page — AI Weekly Narrative (Design Proposal)

> **Status:** **Proposal / design draft — no code, not frozen.** This is the one differentiation lane the market
> benchmark surfaced ([MARKET-BENCHMARK.md §4 Tier 2](MARKET-BENCHMARK.md)): an LLM-generated weekly progress
> narrative **grounded entirely in the trainee's own computed numbers**. It is written to fit GymBro's existing
> stance — **descriptive by design, never fabricate** ([SPEC.md §A](SPEC.md)) — *by construction*, not by hope.
> It needs product sign-off (the open decisions in §9) before it is frozen and handed to implementation.

**Related:** [MARKET-BENCHMARK.md](MARKET-BENCHMARK.md) · [API-CONTRACTS.md](API-CONTRACTS.md) · [SPEC.md](SPEC.md) · [IMPLEMENTATION.md](IMPLEMENTATION.md)

---

## 1. The thesis

No strength logger ships a real LLM narrative over your own data. Whoop Coach (GPT-4), Oura Advisor, Strava Athlete
Intelligence, and Fitbit's Gemini coach proved the pattern in adjacent categories; Hevy's Feb-2026 "Trainer" is
algorithmic, not narrative. This is GymBro's clearest, lowest-risk place to differentiate — **and it strengthens the
trust position instead of threatening it, if and only if one rule holds:**

> **The LLM is a writer, not a calculator.** Every number originates in deterministic .NET/SQL. The model only renders
> prose around pre-computed facts. A post-generation check rejects any number in the output that is not in the input.

The independent evidence is unambiguous on why this rule matters: a 2025 JMIR scoping review of 20 LLM coaching
studies names *"factual hallucination in exercise prescription"* as the core risk and prescribes RAG-grounding as the
mitigation; the LLM-training-plan quality study found raw LLM plans "moderate quality… require expert revision." So we
let the LLM do the one thing it is good at (writing) and never the thing it is bad at (computing or prescribing).

## 2. Why it fits GymBro (not a new philosophy — the same one)

| GymBro principle | How the narrative honors it |
|---|---|
| **Answer questions, don't display statistics** | The narrative *is* the answer — "you vs. past-you," in one paragraph. |
| **Self-referenced, never comparative** | It narrates only the trainee's own deltas; no other user, no percentile. |
| **Descriptive, prescription stays with the human coach** | Hard ban on prescriptive verbs (§5). It describes; it never tells you to deload/add weight. |
| **Never fabricate** | Span-check guarantees every number traces to a computed fact; a fabricated figure cannot survive to the screen. |
| **Forgiving** | Same tone rules as the headline state line ([PHASE-1.md §2](PHASE-1.md)) — green or neutral, never red for the whole page. |

## 3. Architecture (writer-not-calculator)

```
GetMyProgressOverviewHandler (+ E1rmSeriesCalculator)   ← all numbers already computed here
        │  deterministic fact-set (JSON), self-scoped, no PII beyond the user's own metrics
        ▼
   Prompt assembler  ──►  LLM (Claude)  ──►  draft prose
        ▲                                        │
        │                                        ▼
        │                            Numeric span-check + verb/claim guard
        │                                        │
        │  fail (regenerate once, then …)        ▼ pass
        └──────────────────────────►  Deterministic template fallback  /  return narrative
```

- **Reuse, don't recompute.** The fact-set is the existing overview output (`ThisWeek`, `TopLifts[]` with
  `CurrentE1rmKg`/`Direction`/`StallSessions`, `Consistency`, `RecentPrs[]`) plus a small "vs. last week / vs. 8-week
  norm" delta block. No new aggregation logic, no new honesty gate — they already live in
  `GetMyProgressOverviewHandler` and `E1rmSeriesCalculator`.
- **The LLM never sees raw rows.** It sees only the rounded, computed fact-set — so it *cannot* derive a wrong number
  even if it tried.

## 4. Data contract (input fact-set)

The model receives only these pre-computed values (illustrative shape — finalize with the contract in
[API-CONTRACTS.md](API-CONTRACTS.md) when frozen):

```jsonc
{
  "weekOf": "2026-06-08",
  "adherence": { "completed": 3, "goal": 4, "hasActivePlan": true },
  "consistency": { "pct": 78, "streakWeeks": 5 },
  "topLifts": [
    { "name": "Bench Press", "e1rmKg": 102.5, "deltaPct4wk": 4.0, "direction": "up", "stallSessions": 0 },
    { "name": "Back Squat",  "e1rmKg": 150.0, "deltaPct4wk": 0.0, "direction": "flat", "stallSessions": 4 }
  ],
  "recentPrs": [ { "name": "Deadlift", "weightKg": 140, "reps": 3, "e1rmKg": 153 } ],
  "volumeVsNorm": { "thisWeekKg": 18250, "eightWeekAvgKg": 16100 }
}
```

Every field here is already computable today with **zero migration** ([MARKET-BENCHMARK.md §4](MARKET-BENCHMARK.md)).

## 5. Output contract & guardrails (the whole ballgame)

**Shape:** a short, structured result — `headline` (one weighted sentence) + `body` (2–4 sentences) + optional
`callouts[]` (e.g. a stall note). Rendered in a `GbCard` at the top of the Progress home, above the existing sections.

**Guardrails enforced in code, not just the prompt:**

1. **Numeric span-check (mandatory).** Extract every number from the draft; each must exist in the input fact-set
   (within rounding). Any orphan number → reject. This is the single most important control.
2. **No prescription.** Reject drafts containing prescriptive verbs/phrases ("add weight," "deload," "you should,"
   "increase to," "do X sets"). The narrative describes; the human coach prescribes.
3. **No causation.** Reject "because"-style causal claims linking metrics ("your bench rose *because*…").
4. **Tone.** Green or neutral overall; a *per-lift* "slipping" note is allowed (mirrors the headline rule in
   [PHASE-1.md §5](PHASE-1.md)); never red for the whole summary.
5. **Disclosure.** Label it a generated summary (EU AI Act limited-risk = transparency obligation); borrow Oura's
   honest *"summaries can be imperfect"* affordance.
6. **Deterministic fallback.** If the LLM times out / errors, or a draft fails the span-check twice, render a
   **template narrative** assembled from the same fact-set (no model). **The feature degrades to honest, never to
   broken or fabricated.**

## 6. Endpoint, scope & freshness

- **`GET /api/me/progress/narrative`** — self-scoped (`QueryOwnAcrossGyms`, no `X-Tenant-Id`), mirrors the
  `/api/me/progress/*` convention. `200` + empty/`null` narrative for a new user (no `204`); new-user copy is the
  existing first-run hero, not a generated paragraph.
- **Caching is justified here (unlike the overview).** LLM calls cost money and latency, and the narrative changes
  only when training does. Cache per `user + ISO-week`, invalidate on `SessionCompletedEvent`. Thin clients still get
  an instant render; the model runs at most once per user per week (plus on a completed session).
- **Auth-exemption entry** for `GetMyProgressNarrativeQuery` (`ImperativeGuarded`), like the other self-scoped reads.

## 7. Model choice (decision, with a default)

Default to **Claude** (the latest capable family). The task is a short, bounded, grounded narrative — so **Claude
Haiku 4.5 (`claude-haiku-4-5`)** is the cost/latency default; escalate to **Sonnet (`claude-sonnet-4-6`)** or **Opus
(`claude-opus-4-8`)** only if quality review demands. Low temperature, a tight system prompt, and a constrained output
schema do most of the work; the model tier is secondary because the numbers are pre-computed and span-checked.

## 8. Phasing

| Phase | Scope | Risk |
|---|---|---|
| **N1 — Trainee weekly narrative** | This proposal. Self-scoped, cached, fallback-guarded. | Low — span-check + template fallback bound it. |
| **N2 — Coach-assist drafted check-ins** | LLM drafts a coach message from the **deterministic** roster flags (the −20% adherence trigger, stall). LLM writes a *message*, never a number. | Low — lowest hallucination surface; high coach time-savings. |
| **N3 — NL Q&A over own history** | "How's my squat since March?" via text-to-SQL over the trainee's own data. | **High** — wrong aggregations, empty-result confabulation, **tenant leakage**. Gate behind strict refusal + isolation enforced in the query layer, not the prompt. Defer until N1/N2 prove out. |

## 9. Open decisions (need a product owner)

1. **Ship it at all / opt-in vs default-on?** *(Default proposed: opt-in, off by default, labeled as generated.)*
2. **Provider data policy.** Confirm a zero-retention / no-training agreement with the model provider and **send
   aggregates only, never raw PII** (GDPR data-minimization; the fact-set already contains no name/email). *(Default:
   require zero-retention DPA before ship.)*
3. **Cache TTL / invalidation.** Per-week + on `SessionCompletedEvent`, or a simpler weekly TTL? *(Default: event-driven.)*
4. **Coach-facing variant (N2) sequencing** — bundle with N1 or ship after? *(Default: after N1.)*

## 10. What this is NOT

It is **not** an AI coach, an autoregulation engine, a prescription generator, a readiness score, or a chatbot that
answers open fitness/nutrition/medical questions. Those are the failure modes of the AI cluster
([MARKET-BENCHMARK.md §3](MARKET-BENCHMARK.md)). This is a **writer that narrates the trainee's own real numbers** — and
nothing else.

### Sources
- LLM coaching hallucination + RAG grounding: [JMIR 2025 scoping review](https://www.jmir.org/2025/1/e79217)
- LLM training-plan quality ("require expert revision"): [PMC12492345](https://pmc.ncbi.nlm.nih.gov/articles/PMC12492345/)
- Grounded-narrative precedents: [Whoop Coach (GPT-4)](https://openai.com/index/whoop/) · [Oura Advisor](https://ouraring.com/blog/oura-advisor/) · [Strava Athlete Intelligence](https://press.strava.com/articles/stravas-athlete-intelligence-translates-workout-data-into-simple-and)
- EU AI Act limited-risk / transparency for non-diagnostic coaching: [Tandem Health (EU AI Act/GDPR)](https://tandemhealth.ai/resources/knowledge/eu-healthcare-ai-regulations-mdr-gdpr-ai-act)
