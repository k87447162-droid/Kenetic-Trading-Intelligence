# Research Board — Strategy Promotion Ledger
> Destination: `docs/03_Research/Research_Board.md` (replaces seed)
> Framework: Step 11A Levels 0–8. No recommendation skips levels. Populated 2026-07-09, retroactively classifying every active item. Evidence detail lives in the Hypothesis Ledger; this board tracks STAGE.

## LEVEL 5 — Out-of-Sample Validation (spec frozen; contradictions are evidence)

**Agreement Gate (FTR-MNQ + inVA×flow matching)** — the program's lead candidate.
- Path taken: Level 4 (HIST, n=154: allowed +$718 / blocked -$732; FTR subset +$1,837 / -$935) → Levels 3+5 concurrently (live shadow scoring on unseen sessions). Order deviation from the framework noted honestly: shadow scoring began on OOS days directly rather than a separate Level-3 period; functionally equivalent, recorded.
- OOS ledger: Day 1 (07-08) 5/5 correct, +$315.60 swing. **Day 2 (07-09) 0/6 — full inversion** on a gap-and-go value-migration day (allowed -$419.30 incl. a fade-against-acceleration the binary flow definition misread; blocked +$237.00 of winners). Cumulative: **5/11 correct; gated -$344.10 vs raw -$392.10.**
- Today's evidence: strongly against, WITH mechanism (SH-010). Confidence: reduced to Low-.
- Decision: **REMAIN Level 5, spec unchanged**, window continues to ~30 FTR signals. At window close: if SH-010 holds, demote to Level 2 for re-specification with migration conditioning, then re-climb. No mid-window edits — the inversion day is the reason this rule exists.

## LEVEL 4 — Historical Validation (passed; awaiting OOS design)

**DIV-as-confirmer (SH-001 refined).** HIST: +$1,078 (n=38, 2 months) vs DIV_DIRECT standalone -$2,061 (n=264, rejected). Next stage: Level 5 — begin counting forward DIV_FTR signals in the daily loop (zero so far in 4 live days; signal is rare, window will be long). Complexity cost: none (filter exists in strategy already).

## LEVEL 2 — Hypothesis (shadow evaluation, no strategy changes)

**SH-010 — Gate is conditional on value-migration state.** Opened 2026-07-09 from the inversion day; mechanism-backed (instrumentation timestamped the relocations that broke the gate). Next test: pre-registered variants (three-state flow; migAge/direction conditioning; day-type conditioning) run ONCE against HIST + the completed OOS window — not against the day that inspired them alone.
**Exit-type damage class (SH-002 sharpened).** Support: HIST split + 07-08 forward instance. Contradiction: 07-09 winner exited via Sell flatten. Next test: flatten-exit tally to n≥20 forward.
**Opening family positive (Opening_Flush/Momentum/OR_Breakout).** Support: all-period positive in every sample (family n≈75+ HIST; +$58.30 and +$9.10 forward). Next test: dedicated family tally to 20 forward signals; then Level 3 shadow as an 'opening module' assessment.
**inVA (standalone).** HIST +$1,248/-$1,262 split; forward evidence entangled with gate. Rides the gate's window.

## LEVEL 1 — Observation (record, collect, no action)

volPctl mid-bucket (+$3,341 HIST, single exploratory cut — pre-registered retest required to reach Level 2) · IB-hour avoid window (-$1,883 HIST, exploratory) · below-OR location (-$1,457, exploratory) · migAge buckets (conflicting HIST vs 07-09 evidence — now subsumed into SH-010's scope) · SH-003 winners-work-immediately (**demoted 2 → 1** after n=154 overlap result) · setup sprawl (100+ variants, most n<5; strategy-design observation) · Sim101 giveback datum (MFE 79.8→-$33, experiment account).

## LEVEL 0 — Measurement (implement when verified)

COMPLETED: VAH/VAL ✓ sessVWAP ✓ (M-5 resolved) · BOS/CHOCH writer ✓ (DI-3 resolved) · IB/OR windows ✓ · inst= provenance tag ✓ · volPctl ✓ · backfill mode ✓.
PENDING — user action: **DI-1 one-click fix** (disable dump on the 1m chart — still emitting as of 07-09, `MNQ-1m-33b4c0`, 367/440 lines). PENDING — engineering: OriginClassifier metadata fix (N-1) · TradeOutcome MAE/MFE (N-2) · gapDir reference (M-8) · minsIn definition (M-9) · POC-migration persistence criterion (M-11, now feeding SH-010) · three-state flow field (serves SH-010 at Level 0 cost).

## Standing rule
Movement between levels happens only in daily reviews, with evidence cited, and demotions are as routine as promotions. Current board: 1 item at L5, 1 at L4, 4 at L2, 7 at L1, and a Level-0 queue whose top item costs one click.
