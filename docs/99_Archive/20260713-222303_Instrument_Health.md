# Instrument Health
> Destination: `docs/03_Research/Instrument_Health.md`
> The persistent register of measurement issues. Per protocol: problems are recorded once and tracked — never rediscovered. Every daily review checks this file BEFORE trusting any field.
> Status values: OPEN / DIAGNOSED / FIXED-UNVERIFIED / RESOLVED / RETIRED. Priority: P1 blocks research; P2 degrades it; P3 cosmetic.

| ID | Issue | Status | Priority | Evidence / Notes |
|---|---|---|---|---|
| DI-1 | Multiple regime emitters produce contradictory states (≥3 variants: dump stream A, stream B, TIR source; +1m backfill parameterization). 100% cross-stream disagreement measured (94 pairs, 07-07). Gate conclusions flip sign by emitter choice. | DIAGNOSED (inst= tag deployed v2.1) | **P1** | Regime retired as research variable until single named source exists. First live Output save with v2.1 will show both inst tags. |
| DI-2 | ENTRY_ATTRIB level values conflict with state POC (19–100 pts). Different POC definitions or attribution defect. | OPEN | P2 | All 'level reaction' attributions unverified. |
| DI-3 | BOS had no writer — `lastBosBar` never set; bos=none through a 190-pt breakdown. | FIXED-UNVERIFIED (v2.1 UpdateStructureSignals) | P2 | Verify via replay assertion on 07-07 fixture. |
| DI-4 | openType emitted both GAP_AND_GO and GAP_FADE in one session (dual-stream symptom of DI-1). | OPEN | P3 | Resolves with DI-1. |
| M-5 | Session VWAP dead (0.00 in all snapshots — fed by nonexistent session-anchored AVWAP). | FIXED-UNVERIFIED (v2.1 trueSessionVWAP) | P2 | Verify: sessVwap ≠ 0 from bar 1 in next live save. |
| M-6 | Tape-speed counters zero in all snapshots; tape=normal constant/meaningless. | OPEN | P3 | Requires OnEachTick tick counting audit. |
| M-7 | No L2 (book=no_L2) — liquidity features unavailable. | OPEN (accepted limitation) | P3 | Data-feed dependent. |
| M-8 | gapDir=UP while price opened -277 vs PDC — inverted or mis-referenced. | OPEN | P2 | No gap feature trusted until defined. |
| M-9 | minsIn reads -425 at open; zero-point undefined. RTH-relative time derived from timestamps instead. | OPEN | P3 | Data Dictionary entry required. |
| M-10 | Backfill instance is 1-minute; regime/OTF/deltaTrend/ATR are bar-size-parameterized — cross-population comparisons restricted to parameter-robust fields (profile- and clock-based: POC/VAH/VAL/IB/OR/time). | DOCUMENTED (working constraint) | P2 | Applied throughout W28 import. |
| M-11 | POC oscillates between competing HVNs (median 5 jumps>30pts/day vs 88 pts net travel) — valueMigAgeMin resets on flips; migration lacks persistence criterion. | OPEN | P2 | Blocks promotion of migAge features. |
| N-1 | Recorder OriginClassifier v2 stamped strategy entries 'Manual' despite signal names in its own input; strategy also uses generic names — fix must use NT order metadata, not names. | OPEN | **P1** | Origin field untrustworthy; population assignment by signal name only. |
| N-2 | TradeOutcome (tick MAE/MFE) unimplemented in recorder — grid export is the only management-truth source. | OPEN | P2 | Evening grid export is a standing workflow requirement until shipped. |
| N-3 | PublishDiagnostic sink never observed writing to disk — Output-window save is the sole durable home of live dumps. | OPEN | P2 | Daily Output save is mandatory, not optional. |
| N-4 | Grid rows named 'Stop loss'/'Entry' as ENTRY names (n=24) — probable reversal-fill naming artifact. | OPEN | P3 | Excluded from family stats interpretation. |

## Change log
- 2026-07-07: file created; consolidates every issue from W27→W28. Statuses reflect v2.1 deployment (compiled, replay-unverified).
