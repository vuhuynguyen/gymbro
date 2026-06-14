# Progress Page — Data Visualization

> Elaborates **§G (Visualization Map)** of the [canonical Progress spec](SPEC.md). This file picks one chart
> per metric *type*, says why, and lists the ways each chart lies if you let it. It does **not** decide *what* to
> show or *when* — that is the [Priority matrix](PRIORITY-MATRIX.md) and the [IA / dashboard layout](MOBILE-DASHBOARD.md). It
> assumes the metrics, fields, and scope rules already settled in the [spec](SPEC.md) and audited in the
> [feasibility audit](FEASIBILITY.md).

Two non-negotiables carry over from the thesis and bound every choice here:

1. **Every chart answers a question, not a statistic.** A viz that drives no decision gets cut — the rationale
   column below states the decision each one enables. If you can't fill that column, don't draw the chart.
2. **GymBro is descriptive, not prescriptive.** Charts show honest *direction* against a self-referenced baseline
   ("you vs. past-you"). They never imply a prescription the backend can't compute — no readiness score, no
   MEV/MAV/MRV bands, no fabricated goal-weight bar, no cross-user leaderboard.

---

## 1. Metric type → chosen viz → rationale → pitfalls

The unit of choice is the **shape of the data**, not the feature. Pick the chart from the data's nature (slow/noisy
vs. binary-per-period vs. single-target vs. discrete-event), then map every metric of that shape onto it.

| Metric type | Chosen viz | Rationale (decision it enables) | Pitfalls to avoid |
|---|---|---|---|
| **Per-lift e1RM trend** — slow, noisy, directional series | **Line + light smoothing**, raw points faint, bold line on top | The core progress question — *push, hold, or change this lift?* Load-normalized strength over weeks is the one signal that says "stronger" honestly. | Don't draw a line under ~4 points (noise reads as trend); **suppress `Reps>12` and non-strength tracking types server-side** (Epley inflates, e1RM is null off `Strength`/`Bodyweight`); truncated Y-axis is allowed **only if labeled**; over-smoothing hides a real stall. |
| **Smoothed bodyweight / slow noisy body metric** | **Trend line + EMA**, faint raw weigh-ins, bold smoothed line | *Stay the course or adjust — don't panic at a daily spike.* Smoothing separates signal from hydration/meal noise. | Over-reading a single raw spike; over-smoothing past the turn; weigh-ins are **sparse and often null** — label it "your weigh-ins," not a daily expectation; non-zero axis is fine here but **must be labeled** (small real change vanishes on a zero axis). Blocked until the metric-series endpoint exists — show an empty-state invite, never a faked sparse line. |
| **Weekly frequency adherence** — single value vs. one target | **Progress ring** ("3 / 4") | *Train again before the week resets.* A glanceable "% of goal done" with completion pull. | Vanity if the goal is trivially hit; useless for trend or comparison (it's one number); **must be forgiving** — prescribed rest is not a miss, no red, no broken streak; never render `0/0` for a planless user — hide it. |
| **Consistency / streaks** — binary-per-period (trained that week or not) | **Calendar heatmap** + a forgiving **consistency %** | *Re-engage after a visible gap; protect a building routine.* The strongest habit-formation viz — streaks and gaps are legible at a glance, and it works for ad-hoc users with no plan. | Color **legend required + colorblind-safe ramp**; intensity shows *frequency, not magnitude* — never imply load; a bare unbroken-streak counter is vanity and fragile — lead with consistency % (weeks-hit ÷ weeks-observed). |
| **Progress toward a goal** — completion of a bounded target | **Progress ring** (or bullet, if a band matters) | *Am I on pace?* Same family as the adherence ring; the ring is the right pick whenever there's exactly one real, backend-computable target. | Only use it when the target is **real** — no goal-weight ring (no field exists), no daily calorie/macro ring (nothing summed server-side). A ring against a fabricated target is the most trust-corroding chart on the page. |
| **PRs / milestones** — discrete dated events | **Annotated milestone timeline**, with PR markers **pinned on the e1RM line** | *Which lift is climbing or has gone quiet* (+ earned-progress motivation). Events on a time axis read as "progress earned," not a trophy tally. | A bare PR **count** is vanity — show the lift, weight×reps, and date; don't imply a *trend* from 1–2 markers; label values **"estimated 1RM"**; don't celebrate the trivial first-session PRs. Note: the records list comes from `/api/me/records`, but the markers-over-time come from the **e1RM series** (the records endpoint returns current best per lift, not a PR history — see [feasibility R3](FEASIBILITY.md)). |
| **Volume composition** — total + breakdown across few groups | **Stacked bar, zero-baseline, ≤6 segments** | *Is the mix balanced and is total volume rising?* One bar carries total, composition, and week-over-week. | Inner segments are hard to compare across weeks — sort segment order stably; **keep the zero baseline**; cap at the 6 coarse muscle groups; this is **secondary to e1RM**, never the strength headline; label "lifting volume only — cardio/bodyweight count 0" so a calisthenics week doesn't read as regression. |
| **Multi-axis muscle balance** — value across 6 groups | **Sorted horizontal bar** *(explicitly NOT radar)* | *Where to add or cut sets; spot a lagging area.* Bar **length** is read accurately by everyone; the biggest group sorts to the top and the imbalance pops. | **Radar is rejected**: it distorts area, the axis order is arbitrary and changes the shape, and small-N (6 axes) reads as a misleading blob. Only 6 coarse groups exist (no biceps/triceps split) — **don't draw MEV/MAV/MRV bands** over them; live-join cost keeps this on a drill-down, not the glance. |
| **Inline trend in a list or card** | **Sparkline** | *Direction at a glance, beside the number.* The shape of a lift's e1RM next to its name, in a card-sized footprint — the home strength strip. | No axis/scale → communicate **shape only**, never invite reading exact values; keep **aspect ratio consistent** across the strip or slopes lie; smooth lightly; pair with an explicit Δ number ("Bench +4.2 kg / 8 wks") so the shape isn't the only signal. |
| **Actual vs. target vs. context bands** — multi-KPI strip | **Bullet graph** | *Am I inside the acceptable band?* Packs actual, target, and qualitative range into one dense strip — good for trailing adherence %. | Needs a **meaningful** target and band — **don't fabricate** the bands GymBro lacks (no MEV/MAV/MRV, no calorie target, no goal weight). Without a real target, downgrade to a plain sparkline or ring. |
| **Acute vs. chronic load (coach)** | **Two separate small bars** + a soft text label | *Is this client ramping too fast or fading?* Showing the two loads side by side is honest and reads at a glance. | **Never compute or display the ACWR ratio** as an injury predictor — the literature rejects it; keep the bars separate. Default the load to **volume**, not `DurationSeconds×RpeOverall` (RPE is integer-only and frequently null — too sparse). Phrase as a soft nudge, never a medical claim. |

### Cross-cutting note: e1RM is the headline, volume is support

The single most common way to mislead on this page is to let **volume** (`Σ WeightKg×Reps`, working sets only)
drive the "are you progressing?" headline. Volume is **zero** for bodyweight, cardio, timed, HIIT, and mobility
work — a heavy calisthenics or running week renders a flat/empty bar that *looks like regression*. The headline
state line and the strength strip are **e1RM-driven**; the volume chart is always captioned "barbell/dumbbell work
only" and lives below the strength story. See [feasibility R7](FEASIBILITY.md).

---

## 2. Universal guardrails (apply to every chart)

These are load-bearing correctness rules, not polish. Several are enforced **server-side in the query** so the API
never emits a misleading point.

1. **Minimum-N gate.** ≥4 weeks for any trend line, ≥4 sessions-of-a-lift for an e1RM line, ≥4 weeks for an
   adherence trend. Below the gate, render **raw points** and the copy *"not enough data yet."* Never draw a line
   through 2 points.
2. **e1RM honesty filter (server-side).** Emit an e1RM point only when `SetType=Working`, `TrackingType ∈
   {Strength, Bodyweight}`, both `Reps` and `WeightKg` are non-null, and `Reps ≤ 12`. Don't push the filtering to
   the client — the API shouldn't be able to return a garbage point.
3. **Zero-based bars; labeled non-zero axis allowed only on lines** where the real change is small (e1RM,
   bodyweight). A truncated bar axis exaggerates change and is never allowed.
4. **Adherence-neutral / anti-shame.** No red "you failed"; prescribed rest counts as success; **consistency %
   over unbroken streaks**; no confirmshaming copy; never monetize streak anxiety.
5. **Self-referenced, never competitive.** "You vs. past-you" only. No cross-user strength leaderboards (documented
   harm). Coach acknowledgement is collaborative and opt-in.
6. **Never fabricate.** No readiness/recovery score (no wearable, integer RPE), no MEV/MAV/MRV bands (6 coarse
   groups), no goal-weight bar (no field), no daily calorie/macro "on track" (nothing summed server-side), no
   strength percentile (no normative data), no training-density chart (`DurationSeconds` is wall-clock incl. rest).

---

## 3. Practical Flutter charting

The mobile client is Flutter (Riverpod, design-token `ThemeExtension`); the portal is Angular/PrimeNG. The shapes
above translate cleanly to either, but the mobile glance is the binding constraint.

- **Library: `fl_chart`.** It covers the four shapes we actually need on mobile — `LineChart` (e1RM trend,
  bodyweight EMA, sparkline), `BarChart` (volume, stacked volume composition, muscle-balance horizontal bars,
  acute/chronic), and we render the **progress ring** with a `CustomPainter` arc and the **calendar heatmap** with
  a plain `GridView` of token-colored cells (a charting lib is overkill for square cells). The **PR timeline** is a
  styled list with markers, not a chart widget. No second charting dependency is justified.
- **Sparklines** are `LineChart` with axes, grid, dots, touch, and titles all **off** — just the bold line. Lock a
  **fixed aspect ratio** across the strip (wrap each in the same `AspectRatio`) or identical slopes will render at
  different angles and lie.
- **Theme through tokens, never hardcode.** Every series color, grid line, and label pulls from the design-token
  `ThemeExtension` (the `inv-*` tokens), so charts track light/dark and the brand palette. No raw `Color(0xFF…)`
  in chart code.
- **Smoothing belongs in the data layer, not the widget.** Compute EMA/rolling-average in the Riverpod
  provider/repo and feed `fl_chart` two series (faint raw + bold smoothed). Keep `isCurved` conservative — Catmull
  spline overshoot can invent a peak between two real points.
- **Respect the min-N gate in the widget contract.** The provider returns either a `points` series or an
  `insufficientData` state; the chart widget renders the empty-state copy for the latter and never interpolates.
- **Performance.** Bound series to the **top 3–6 lifts** (resolve most-trained first), cache the computed series
  (see [feasibility §4](FEASIBILITY.md)), and hand `fl_chart` already-aggregated weekly points — never raw set
  rows. `const` constructors and a stable key per chart avoid needless repaints on a scrolling dashboard.

---

## 4. Accessibility & color

- **Never encode meaning in hue alone.** Direction (up/flat/down) carries an **icon + sign + text** ("▲ +4.2 kg"),
  not just green/red. Heatmap intensity carries a **legend** and is reinforced by the cell number on tap.
- **Colorblind-safe ramps.** The consistency heatmap uses a single-hue **sequential** ramp (light→dark of one
  brand hue), not a red↔green diverging ramp — deuteranopia-safe and reads as "more/less," which is the actual
  semantics. Avoid simultaneous red+green as the only differentiator anywhere.
- **Anti-shame palette.** Adherence and consistency never use red/alarm colors. "Behind goal" is neutral/quiet,
  not a failure state — this is an accessibility *and* a product rule.
- **Contrast & touch targets.** Chart text and lines meet WCAG AA contrast against the surface token; interactive
  marks (PR markers, lift selectors, tappable cells) meet the 44×44 dp minimum.
- **Non-visual fallback.** Every chart pairs with a one-line text summary that *is* the insight ("Bench e1RM
  +4.2 kg over 8 weeks; flat the last 3 sessions"). That line is the screen-reader label and the 5-second takeaway
  for everyone — the chart elaborates it, it doesn't replace it.

---

## 5. Avoiding misleading charts

The page's only real asset is trust; a confident chart the data doesn't support destroys it. The recurring traps,
and the standing rule for each:

- **Truncated axes.** Bars are **always zero-based**. Lines may use a non-zero axis only when real change is small
  (e1RM, bodyweight) **and the axis is labeled**. Never silently crop a bar axis to dramatize a 2 kg move.
- **Trend from too few points.** The **min-N gate** (§2.1) is absolute — no line under the threshold; show raw
  points and "not enough data yet."
- **Over-smoothing.** Smoothing reveals signal but can erase a real stall or invent a peak (spline overshoot).
  Keep raw points faintly visible so the smoothing is auditable, and keep curvature conservative.
- **Mislabeled / cross-modal aggregates.** Volume is "lifting only" — caption it so a cardio/bodyweight week isn't
  read as zero effort. e1RM values are **"estimated 1RM,"** never presented as a tested max.
- **Vanity monotonics.** Lifetime totals (total kg, lifetime sessions, summed `PrCount`) only ever rise, carry no
  reference point, and can glow green during a regression. They are **cut** — every number is change-vs-baseline.
  `PrCount` is acceptable **only** as an in-session celebration count, never as a progress or stall input
  ([feasibility R4](FEASIBILITY.md)).
- **Fabricated targets & bands.** No ring/bullet/band against a target GymBro can't compute — no goal weight, no
  calorie target, no MEV/MAV/MRV. When the real target is missing, drop to a plain trend, don't invent the
  reference.
- **False precision.** Integer RPE (no half-points), wall-clock `DurationSeconds`, and sparse per-session
  `BodyweightKg` must not be charted as if continuous and dense — these stay drill-down diagnostics with explicit
  "coarse" labeling, never headline lines.
- **Radar's area illusion.** Muscle balance is a **sorted bar**, never a radar — area and axis-order distortion
  make a radar say different things depending on how you ordered the axes.

---

## Open questions

- **§G lists a bullet graph for "actual vs. target vs. bands," but the spec's strongest real bullet candidate
  (trailing adherence %) has only a target, no qualitative bands** (the only band-bearing metrics — MEV/MAV/MRV,
  calorie targets — are explicitly Not-Computable). So in practice the bullet degrades to a ring/sparkline almost
  everywhere. Worth confirming whether any v1 metric genuinely warrants the bullet's band machinery, or whether
  "bullet" should be documented as a future-only viz pending a real banded target.

---

*Sibling docs:* [Canonical spec](SPEC.md) · [Priority matrix](PRIORITY-MATRIX.md) · [IA / dashboard](MOBILE-DASHBOARD.md) ·
[Feasibility audit](FEASIBILITY.md). One fact, one place — this file owns **viz choice and chart honesty**;
metric definitions, feasibility, and priority live in the siblings.
