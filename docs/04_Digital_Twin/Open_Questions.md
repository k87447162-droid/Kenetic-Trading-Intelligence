# Open Questions
> Rev.2, 2026-07-06. Q-02 CLOSED (multi-instrument spread = the strategy's universe, not human drift). Q-03 PARTIALLY CLOSED (platform authoritative: +$1,375.95; ~$1,166 gap under investigation; multipliers verified).

**Q-01 — DEPRIORITIZED 2026-07-06** per trader decision: TDFYSL episodes set aside as emotional incidents; account rules not pursued unless the pattern recurs.
**Q-04 — CLOSED 2026-07-06:** fork (b). The algorithm is the trading; the trader's discretionary role is system design and refinement. The Digital Twin's [HUMAN] track now models R&D/override decisions; the [STRATEGY] track is the primary research program. Constraint honored by design: the trader dislikes typing — the pipeline runs on automated capture (recorder + state dumps + periodic upload), never journaling.
**Q-09 — CLOSED (effectively):** trader confirms the strategy fires low-quality entries; refinement is the intent. See SH-008 for the evidence-based shape of that refinement.

## Research Priorities (as of 2026-07-06)
1. **SH-008 shadow test** — 2–3 weeks, strategy unchanged, entries tagged with fire-time regime; evaluate the Chopping/Drifting gate out-of-sample before any code change.
2. **SH-002 exit study** — $12.7K/week of MFE giveback is the largest dollar lever on the board; larger than trade selection.
3. **Persist the SessionLevels state dumps to disk** (engineering, when instructed) — the context layer currently dies with the Output window, and 42% of entries had no resolved context.
4. **Fix OriginClassifier v2** (engineering, when instructed) — use NT order/strategy metadata, not names.
5. **Weekly import cadence** — same zip-and-upload as this session; each import must strengthen/weaken/reject/discover.
**Q-05 — CLOSED 2026-07-06:** trader confirmed the episodes were emotional; no plan existed. 'No plan' is the recorded weakness class (W-03 stands).
**Q-06 — DEPRIORITIZED** with Q-01: MAE/MFE on the six trades adds little once they are classified as incidents rather than process.
**Q-07 — Recorder gaps:** 37% of legs lack market context; 4 trade groups incomplete; OriginClassifier defect (engineering ticket). 
**Q-08 — CLOSED 2026-07-06 (trader-confirmed):** SimAccount2 is auto-trading only. The 13 generic-name rows ('EntryLong'/'EntryShort', net -$277) are strategy trades using generic signal names. The human sample remains n=6, all on evaluation accounts. Consequence for the classifier fix: signal-name heuristics are one-directional — descriptive names imply Strategy, but generic names do NOT imply Manual; the OriginClassifier repair must use NinjaTrader's actual order/strategy metadata, not names.
**Q-09 (new) — Strategy intent:** is the unfiltered Failed_Test_Retest (101 rows, -$609) meant to run live, or is it a data-collection probe with DIV as the production filter? Determines whether SH-001 is a finding or a design confirmation.
