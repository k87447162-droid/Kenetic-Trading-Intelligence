# Instrument Health
> Destination: `docs/03_Research/Instrument_Health.md`
> The persistent register of measurement issues. Per protocol: problems are recorded once and tracked — never rediscovered. Every daily review checks this file BEFORE trusting any field.
> Status values: OPEN / DIAGNOSED / FIXED-UNVERIFIED / RESOLVED / RETIRED. Priority: P1 blocks research; P2 degrades it; P3 cosmetic.

| ID | Issue | Status | Priority | Evidence / Notes |
|---|---|---|---|---|
| DI-1 | Multiple regime emitters produce contradictory states (≥3 variants: dump stream A, stream B, TIR source; +1m backfill parameterization). 100% cross-stream disagreement measured (94 pairs, 07-07). Gate conclusions flip sign by emitter choice. | **CLOSED (expected behavior under dual-engine architecture)** | closed | The emitters still produce DIFFERENT values — that is now EXPECTED, not an error: 5m = Strategy Context Engine, 1m = Execution Context Engine (trader directive 2026-07-13); inst= tags separate them; bar-size-dependent fields are engine-local (non-pooling law, Context_Engines.md). The interpretation problem is solved; the measurement fact (engines disagree) is permanent and by design. Prior "disable the 1m chart" recommendation retracted. |
| DI-2 | ENTRY_ATTRIB level values conflict with state POC (19–100 pts). Different POC definitions or attribution defect. | OPEN | P2 | All 'level reaction' attributions unverified. |
| DI-3 | BOS had no writer — `lastBosBar` never set; bos=none through a 190-pt breakdown. | RESOLVED (live-verified 2026-07-08: BOS fired 295 lines, CHOCH 175) | P2 | Replay fixture optional. |
| DI-4 | openType emitted both GAP_AND_GO and GAP_FADE in one session (dual-stream symptom of DI-1). | OPEN | P3 | Resolves with DI-1. |
| M-5 | Session VWAP dead (0.00 in all snapshots — fed by nonexistent session-anchored AVWAP). | RESOLVED (2026-07-08: >0 on 471/471 live lines) | P2 | VWAP-distance features unblocked. |
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

| N-5 | **No EOD flatten guarantee independent of chart/strategy process** — Friday position carried the weekend, ≈ -$2,904 realized. SESSION_HEALTH forceFlat did not fire (root cause pending: chart likely closed 12:15 with position open). | OPEN | **P1 — TOP of all queues** | Only register item with four-figure realized cost. Candidate fixes: broker-side EOD flatten order; recorder-side open-position alarm; strategy time-based hard flatten. |
| N-6 | Grid FIFO pairing unreliable across unflattened session boundaries; recorder position ledger session-relative (posBefore=0 with live broker position). | DOCUMENTED (working rule: reconstruct from fills post-incident) | P2 | Discovered 2026-07-13 during incident reconciliation. |

## Change log
- 2026-07-13 (later): DI-1 RESOLVED by two-engine architecture; M-12 correction applied via 1m-engine parallel-track gate scoring.
- 2026-07-13: N-5 (EOD flatten failure, P1) and N-6 (cross-boundary pairing/position-ledger limits) added after the weekend-hold incident.
- 2026-07-08: M-5 RESOLVED, DI-3 RESOLVED, DI-1 root identified by instance name (one-click fix available). VAH/VAL verified 100% sane (a first-pass violation report was an analysis-side regex bug, recorded in the 07-08 review).
- 2026-07-07: file created; consolidates every issue from W27→W28. Statuses reflect v2.1 deployment (compiled, replay-unverified).
