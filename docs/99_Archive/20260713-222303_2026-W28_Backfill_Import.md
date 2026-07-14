# Research Finding — Historical Backfill Import (May 11 – July 7, 2026)
> Destination: `docs/08_Daily_Operations/Research_Findings/2026/2026-W28_Backfill_Import.md`
> Data: 43 backfill session files (14,225 snapshots, single instance MNQ-1m, all v2.1 fields, mode=HIST) + full grid export (2,237 rows → 1,336 round-trip trades, 3 sim accounts, 7 instruments, net -$16,629.05). MNQ context join: 154/154 trades matched.
> Provenance rules applied: everything here is HIST-grade (reconstruction). W27 is INSIDE this window — findings are extensions of sample, not independent replications.

## Step 1 — Verification facts
All three accounts are STRATEGY accounts (Sim101 = a DIV_DIRECT experiment line, 456 rows; SimAccount1 = 'Copied' mirror variants, 27 rows). Zero manual trades in the export. Session files are named by session-START date (16:01 prior evening → next-day close) — join handled accordingly. Backfill instance is 1-MINUTE (`inst=MNQ-1m-a33117`): regime/OTF/deltaTrend are 1m-parameterized and NOT comparable to live-5m labels; ATR-normalization skipped in favor of raw points for this reason.

## SCOPE CORRECTION (trader-confirmed 2026-07-07): the evaluation universe is MNQ ONLY.
The multi-instrument figures below are historical context, not the program under evaluation. **MNQ-only program: 154 trades, net -$14.30** (May +$227 / June -$599 / July +$358) — breakeven over two months, with the matched-conditions structure inside it. Critically, the setup verdicts INVERT under the correct scope: on MNQ, `Failed_Test_Retest` is the program's largest *positive* family (n=68, 54.4% win, **+$902**) — the -$9,058 FTR disaster lives entirely in the six legacy instruments. Setup design is not instrument-portable; every prior setup-level claim now carries an instrument qualifier.

## The two-month portfolio fact (LEGACY CONTEXT — outside evaluation scope)
May -$6,119 · June -$9,656 · July -$854. The unfiltered `Failed_Test_Retest` family alone: **n=463, -$9,058 (54% of all losses)** — the workhorse-loses finding confirmed at scale. MNQ specifically was breakeven (-$14.30 over 154); the losses concentrate in the *other* six instruments, which have **no context instrumentation** — the largest unmeasured territory in the program.

## Hypothesis verdicts (pre-registered tests)

**SH-001 — DIV filter: CONFIRMED and REFINED (Low → Moderate-candidate).** `Scalp_DIV_Failed_Test_Retest`: n=38, **+$887 to +$1,078** at ~32% win (positive expectancy via winner size) vs base FTR n=463, -$9,058. BUT `Scalp_DIV_DIRECT` (divergence as standalone entry): n=264, **-$2,061**. Refinement: **divergence works as a CONFIRMER of a level event, not as a signal by itself.** Caveat: same-period extension, not independent; needs forward samples before promotion.

**SH-009 — RESTRUCTURED into its strongest form yet (n=154 2×2):**
| | WITH accelerating flow | without |
|---|---|---|
| **Extended >50pts from POC** | **+$1,482** (54.5%, n=55) | **-$1,198** (51.1%, n=45) |
| **Near value** | **-$1,001** (54.5%, n=33) | **+$703** (61.9%, n=21) |

The diagonal is the finding: **location and flow must AGREE** — extension is tradable with live flow and fatal without it; value-area entries win in calm and lose when chasing acceleration. Named: the *matched-conditions* rule.
**Adverse-side sub-claim: CONTRADICTED at scale.** Entries on the adverse side of POC (n=44): 68.2% win, +$432 vs non-adverse 49.1%, -$446. Day-1's T5 story does not generalize — mean-reversion toward value from the 'wrong' side performed best by win rate. Sub-claim retired.

**Value area (FIRST VAH/VAL data — the new instrumentation's maiden result, n=154):** inside-VA entries **+$1,248 (58.4%)** vs outside-VA **-$1,262 (49.2%)** — clean, symmetric, and composable with the 2×2 (outside-value requires flow; inside-value doesn't). Feature `inVA` opens strong.

**SH-003 — MATERIALLY WEAKENED (n=154).** Winners' median MAE 5.7 pts vs losers' median MFE 6.2 pts — the distributions nearly overlap; only 49%/44% separate at a 5-pt threshold. The crisp two-day pattern was small-sample sharpness; the day-2 exception was the tell. Downgraded to weak/possibly-parameter-dependent; time-stop designs should not assume it.

**SH-002 — SHARPENED by exit type (n=154).** Profit targets: 9/9 wins, median ETD **0.6 pts** (near-perfect capture). 'Stop loss' exits (mostly trails): n=94, 54.3% win, net **+$753** despite 20-pt median giveback — trails are functioning. The damage exits are generic flattens ('Sell' 31% win -$998; 'Buy to cover' -$1,561). Exit research should target the flatten path, not the trail.

**SH-004/SH-005 — RETIRED AS STATED (measurement-driven, not evidence of absence).** The cleanest regime series ever available (single instance) shows CHOPPING cells breakeven-to-positive — directionally opposite the label-based W27 story — while being 1m-parameterized, i.e., a *fourth* regime variant. Three-plus emitters, parameter-dependent by construction, mutually contradicting: 'regime' as currently emitted is not a research variable. Superseded by the value/flow/participation features, which are parameter-robust.

## Exploratory observations (single cuts, multiple-comparison risk — recorded, not concluded)
- **volPctl mid-bucket: +$3,341 (65.6%, n=32)** vs high -$2,617 and low -$738 — extreme participation (dead OR climactic) loses; middling wins. Strongest new candidate; needs pre-registered retest.
- `Scalp_Opening_Flush` n=49: 55.1% win, +$366 over two months — the opening module is a genuine small positive.
- `Scalp_OR_Breakout` n=8 +$491; `Opening_Momentum` n=20 +$105 — opening-family setups skew positive.
- **Setup sprawl (Strategy Finding):** 100+ named variants, most n<5, parameter-suffixed clones (DeltaAccum_EMA5_2bars…30bars). The strategy itself violates 'Protect Simplicity'; the sub-5-n tail is untestable noise by construction.
- Entries named 'Stop loss'/'Entry' (n=24, +$412) — naming artifacts, likely reversal fills (Measurement note).

## Instrument Health updates
CONFIRMED: DI-1 deepened (regime is parameter-dependent across ≥3 emitters). RESOLVED-for-backfill: single-instance provenance works; `inst=` tag functioning. NEW: M-10 — 1m backfill vs 5m live parameterization means cross-population comparisons must use parameter-robust fields only (POC/VAH/VAL/IB/OR/time — profile- and clock-based). OPEN: six instruments carry the majority of losses with zero context coverage.
