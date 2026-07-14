# Two Context Engines — Analytical Architecture
> Destination: `docs/03_Research/Context_Engines.md` | Installed 2026-07-13 (trader directive). Supersedes all prior recommendations to disable the 1-minute stream.

## Roles (fixed)
**5-MINUTE — Strategy Context Engine.** Regime, auction state, value structure, session context, trade location, HTF structure. Answers: *should the strategy be interested?* Authority for: VA/POC location axis, session/value profiles, IB/OR, day character.
**1-MINUTE — Execution Context Engine.** High-resolution behavior around every 5m decision (taken or missed): delta accel/decel, divergence, volume acceleration, absorption/exhaustion, initiative bursts, responsive activity, pullback/rotation quality, micro BOS/CHOCH timing, sweeps, failed breaks, acceptance/rejection timing, time at/outside value, retests. Answers: *what actually happened inside the 5-minute bar?* Never generates trades independently. A microscope, not a system.

## Non-pooling law (extends M-10/M-12)
Bar-size-dependent fields (deltaTrend, regime, OTF, atrRatio, swing) are ENGINE-LOCAL: learned, scored, and compared only within their engine. Profile- and clock-based fields (POC/VAH/VAL, IB/OR, time) are shared. Every analysis states its engine. The gate's forward failure last week was substantially a violation of this law (M-12), now corrected.

## Hierarchical trade anatomy (target record for every trade)
SESSION CONTEXT → VALUE CONTEXT → 5-MINUTE STRUCTURE → 1-MINUTE EXECUTION CONTEXT → SIGNAL → EXECUTION → MANAGEMENT → OUTCOME.

## Missed-opportunity record (schema; daily-loop step from 2026-07-14)
Per candidate: time · setup type · qualification basis (information available AT that moment only — no hindsight) · why the strategy ignored it · 5m context · 1m context · Value Context · auction context · alignment with existing setup vs new contextual variation · confidence · supporting/contradicting evidence. Candidates accumulate beside actual trades so the two populations can be compared over hundreds of sessions. Observations only; implementation requires the Promotion Framework.

## First empirical validation of the architecture (same day as installation)
Re-scoring the gate's OOS window with flow taken from the 1m engine (where it was LEARNED) while location stays 5m: **12/17 correct; allowed +$22.10 vs blocked -$1,440.80** (raw window -$1,418.70) — versus 7/16 and -$974.40 allowed under the mis-sourced 5m flow. **This retroactive result demonstrates measurement consistency, not predictive validity. Only forward performance of the 1-minute execution engine will be used for promotion decisions.** The two engines' deltaTrend disagreed at a majority of the 17 entries: they measure genuinely different objects, exactly as this architecture asserts. Residual errors concentrate on the 07-09 migration day (SH-010 remains the live explanation for what the corrected gate still misses).
