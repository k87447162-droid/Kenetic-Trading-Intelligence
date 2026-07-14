# Hypothesis Ledger
> Index only. Confidence: Speculative → Low → Moderate → High → Validated. Nothing below Moderate informs live decisions.
> Populations are tagged **[HUMAN]** (discretionary decision-making — the Digital Twin's subject) and **[STRATEGY]** (the algorithm — a separate research track). Never pooled.
> Revised 2026-07-06 after trader-supplied population correction. All entries small-sample.

## Active — [HUMAN]

> 2026-07-06 trader statement: the TDFYSL episodes "were traded emotionally." This is treated as confirming evidence (the trader's own label), and per trader decision these episodes are SET ASIDE from active investigation — preserved in the record as behavioral incidents, not pursued as a performance dataset. H-001/H-002 move to monitor-only: no active tests; they reopen automatically if a similar episode recurs.

**H-001 (rev.2) — Discretionary trading occurs exclusively in maximum-risk conditions.**
Confidence: **Low** | Status: **Monitor-only** | Sample: 6 trades, 2 sessions — confirmed complete (trader confirmed 2026-07-06 that SimAccount2 is auto-only; no manual trades exist outside the eval accounts).
The trader's entire confirmed manual footprint for the week: full-size NQ (instant 2–4 lots), premarket, ≤5-minute holds, both episodes adjacent to a catalyst (post-NFP 07-02; pre-open on holiday-thinned 07-03). The algorithm trades micros with defined setups; the human intervenes precisely where the algorithm doesn't. Net -$3,970.
Missing: eval-account platform export (MAE/MFE), account rules (Q-01), intent behind each entry (Q-05).
Next test: next 10 manual trades — premarket written intent vs execution.

**H-002 — Loss-triggered rapid re-entry with direction flips (tilt cascade).**
Confidence: **Low** | Sample: 1 episode (07-02: L→L→S→L, 4 trades ≤4 min, -$2,220 after the initial winner).
Next test: re-entry latency after manual losses, all future sessions.

**H-003 — The immediate post-news window is a poor environment for manual entries.**
Confidence: **Low** | Sample: 1 session. The 07-02 cascade began 5 minutes after the NFP downside miss.
Next test: tag manual trades with minutes-from-release.

## Active — [STRATEGY]

**SH-001 — The delta-divergence filter is the edge; the base Failed_Test_Retest setup is negative without it.**
Confidence: **Low** | Sample: 101 unfiltered rows (-$609.50, 38.6% win) vs 8 DIV rows (+$2,047.50, 75% win).
Next test: 30+ DIV occurrences; check the 8 aren't one market/day cluster.

**SH-002 (refined 2026-07-07) — Exit giveback is exit-TYPE dependent, not universal.**
Confidence: **Low** | Evidence: W27 aggregate 9.4% MFE capture; but shadow days show targets/trails preserving MFE well (ETD 2.6 pts on a target; trails kept 53% and rescued a bad entry to scratch) while the large giveback instance (Mon T1, 56.4-pt ETD) came via full-stop-after-MFE. Next test: MFE capture split by exitType across the shadow window; MFE-lock vs time-stop comparison stands.

**SH-003 — Winners work immediately; non-performing trades rarely recover.**
Confidence: **Low → upper-Low**, now with a defined EXCEPTION CLASS. Supports: W27 aggregates; Mon winner MAE 0.2 pts; Tue T2 MAE 3.6 pts. Exception (2026-07-07 T1): an opening-flush winner survived 48 pts adverse — entries within ~5 min of the open carry structurally wide stops and violate the pattern. Scope restriction candidate: exclude `minsSinceOpen < 5`. Next test: time-to-MFE distribution excluding opening trades.

**SH-004 — Chopping/Drifting regimes are strongly negative for the strategy.** STRENGTHENED 2026-07-06 with platform-verified P&L via time-join (165/172 trades matched to TIR context): Chopping n=26, 19.2% win, -$844.70; Drifting n=12, 16.7% win, -$265.00. Combined: 38 trades, ~18% win, **-$1,110**. Confidence: **Low→upper-Low**. **MEASUREMENT CAVEAT (2026-07-07, DI-1):** the regime variable is produced by multiple indicator instances emitting contradictory states; provenance of the TIR regime tag is undefined. No promotion until regime provenance is fixed; the effect may be real but the measuring instrument is currently ambiguous.

**SH-005 — ReversalForming is the strategy's best environment.** Platform-verified n=28, 60.7% win, +$2,643. Shadow log: 2026-07-06 first counter-observation (-$54, n=1). **Same DI-1 measurement caveat as SH-004.**

**SH-006 — MNQ short-side relative edge** (58.3%, n=12). Low; unchanged.

**SH-007 — At ~21 trades/day the strategy is commission-sensitive** (TIR-derived; platform commissions much lower — re-verify after reconciliation).

**SH-008 (new) — A regime gate removes the strategy's losses; a context-availability gate removes dead weight.**
Confidence: **Low** | Sample: one week, in-sample counterfactual — treat as hypothesis, NOT an edit instruction.
Evidence: trades with no resolved context were not the losers (n=70, 40% win, net +$13 — breakeven churn, pure dead weight). The losses concentrate in *known-context* Chopping+Drifting (-$1,110). Counterfactual: suppressing those two regimes turns the matched week from +$1,519 to ≈ +$2,629. Overfitting warning: this selects on observed outcomes within a single week; the honest test is prospective.
Contradicting evidence: the unfiltered Failed_Test_Retest setup performed WORSE with context (n=55, 30.9%, -$1,014) than without (n=50, 44%, +$278) — 'context present' is not itself protective; the regime *value* is what matters.
Required next test: 2–3 weeks shadow measurement — strategy unchanged, every entry tagged by regime at fire-time, gate evaluated out-of-sample before any code change (per Law 5: research never modifies live behavior; validation is the only gate).
**Shadow ledger:** Day 1 (2026-07-06, 5 trades, MNQ, 4-lot config): Chopping n=2 -$256.50 (supports); Drifting n=1 +$39.50 (contradicts); gate counterfactual +$217.00. Comparability caveat: sizing (4-lot) and effective universe (MNQ only fired) differ from the W27 baseline — shadow tallies kept in counts/points as well as dollars.
**REDESIGN (2026-07-07):** per DI-1 the regime label is not a reliable measuring instrument. The shadow test's primary gate variable switches to independently computable value-location metrics from the raw dumps — POC distance in ATR, side of value, value-migration direction, participation (volRatio) — with the regime tally kept as a secondary series. See SH-009.

**SH-009 (refined 2026-07-07) — Extension from value is conditional: fatal against dying flow, tradable with live flow.**
Confidence: **Speculative→Low** | Sample: 8 trades, 2 sessions.
Naive form ("extension = losses") now carries 2 contradictions in 8: Tue T2 WON at -4.5 ATR below stale value, and Mon's winner was extended. Interaction form strengthening: extension entered WITH live accelerating delta and elevated participation → winners (Tue T1 amid volRatio 6.67, Tue T2); extension entered as participation dies → Mon T2/T5 losses, Tue T3 scratch-rescue. The adverse-side geometry (Mon T5) remains its own negative class. Refined feature: `pocDist_ATR × deltaAccel × volRatio`, plus `valueRelocation` (Tue's 350-pt POC relocation was the day's pivotal event and no setup referenced it).
Next test: continue accumulation; bucket entries by (extension, flow-state) 2×2.
**Shadow ledger:** Day 1: Chop n=2 -$256.50, Drifting n=1 +$39.50, label-gate counterfactual +$217.00. Day 2: **the label-gate result depends on which emitter you believe** — under stream-A labels both CHOPPING trades won and the gate would have COST -$127.40; under the TIR-snapshot labels (received with the late zip: ReversalForming/TrendingDown/ReversalForming) the gate would have suppressed NOTHING ($0), and SH-005 would instead gain two wins. Same trades, opposite conclusions, purely from emitter choice. Running stream-A label-gate net: +$89.60 over 2 days. This divergence is DI-1's impact made concrete and is why the value/flow feature series is the primary instrument.

## Rejected / Refined — [STRATEGY]

**R-003 — 'No-context trades are the garbage' (trader intuition, naive form).** Partially rejected 2026-07-06: no-context entries are breakeven noise, not the loss source; the garbage is regime-specific (Chopping/Drifting). The intuition was directionally right — the strategy fires indiscriminately — but the discriminator is regime value, not context availability. Refined into SH-008.

## Retired / Rejected

**R-001** — "Reversal-forming entries lose money" (pooled): aggregation artifact. Rejected 2026-07-06.
**R-002** — "Discretionary entry, systematized exit" (B-01) and "human winner-management capacity" (old H-008/S-02): **misattribution** — the underlying trades were algorithmic. The +$2,035 MGC hold belongs to the strategy's DIV/ScalpHalf setups. Rejected 2026-07-06 on trader-supplied ground truth; root cause = OriginClassifier v2 defect (signal names present, origin still 'Manual').
**H-008, H-009 (v1)** — retired as [HUMAN] entries; content reborn as SH-002/SH-007 under [STRATEGY].


## Backfill verdicts — 2026-07-07 evening (see 2026-W28_Backfill_Import.md for evidence)

**SH-001 → Moderate-candidate, REFINED:** divergence as CONFIRMER of level events (+$1,078, n=38 over 2 months) vs divergence standalone REJECTED (DIV_DIRECT n=264, -$2,061). Forward samples required before promotion; period overlaps W27 (not independent).
**SH-009 → restructured as the MATCHED-CONDITIONS rule (Low, n=154):** extension requires flow (+$1,482 with / -$1,198 without); value requires calm (+$703 calm / -$1,001 chasing). Adverse-side sub-claim CONTRADICTED (68.2% win, n=44) and retired.
**F-NEW `inVA` (Low, n=154):** inside value area +$1,248 vs outside -$1,262 — first VAH/VAL result; composes with matched-conditions.
**SH-003 → WEAKENED (n=154):** winner-MAE / loser-MFE distributions nearly overlap (medians 5.7 vs 6.2 pts); crispness was small-sample.
**SH-002 → SHARPENED:** targets capture ~all MFE (median ETD 0.6 pts, 9/9); trails net-positive (+$753, n=94); generic flatten exits are the damage (-$2,559 combined). Exit research targets the flatten path.
**SH-004 / SH-005 → RETIRED AS STATED (measurement-driven):** regime is parameter-dependent across ≥3 emitters; cleanest (1m) series contradicts the label story. Not evidence of absence — evidence the instrument doesn't exist yet. Superseded by SH-009/inVA/volPctl family.
**F-CANDIDATE `volPctl` (exploratory, flagged for pre-registered retest):** mid-participation +$3,341 (n=32) vs extremes negative.
**Strategy Finding (standing):** setup sprawl — 100+ variants, most n<5; the sub-5-n tail is untestable by construction.

**BACKTEST SPEC (Strategy V2 candidate, formalized 2026-07-07):** `FTR-MNQ + agreement gate` — allow FTR entries only when (inside value area AND flow not accelerating) OR (outside value area AND flow accelerating with trade direction). HIST evidence: +$1,837 vs -$935 on the blocked complement (n=30/38). Required: formal backtest on independent data or ≥3 weeks live shadow tagging before paper trading. 
**M-11 (new measurement nuance):** POC oscillates between competing HVNs (median 5 jumps>30pts/day vs median 88 pts net travel) — `valueMigAgeMin` resets on flips; migration definition needs a persistence/acceptance criterion before migAge features are promoted.

**GATE OUT-OF-SAMPLE LEDGER (opened 2026-07-08):** Day 1: 5/5 correct classifications; allowed +$75.20, blocked -$240.40 (all three blocks = the inVA&withFlow chasing cell). FTR under spec: -$141.20 → +$66.10. n=5 — recorded, not celebrated; the promotion bar is ~30 out-of-sample FTR signals or 3 weeks.
**SH-002 forward support (2026-07-08):** day's largest loss (-$184.40) exited via generic `Sell` flatten — the damage class, out-of-sample.

**GATE OOS Day 2 (2026-07-09): 0-for-6 — perfect inversion.** Allowed -$419.30 (incl. -$406.90 fade-against-acceleration the binary misread as 'calm'); blocked +$237.00 of winners. Cumulative OOS: 5/11 correct, gated -$344.10 vs raw -$392.10. Spec continues AS WRITTEN — no mid-test modification.
**SH-010 (new) — The agreement gate is conditional on value-migration state.** Confidence: Speculative | Evidence: OOS Day 2 inverted on a gap-and-go day with active upward value migration (POC +367 in two relocations); Day 1's 5/5 occurred with mixed/stationary value. Pre-registered variants to test on HIST+OOS after the window: (a) three-state flow with 'against-acceleration' blocked unconditionally; (b) migAge/migration-direction conditioning; (c) Context-Library day-type conditioning. NOT applied to the live spec.
**SH-002 counter-datum:** +$150.60 winner exited via generic Sell flatten (2026-07-09) — first exception to the flatten-damage class.

**FRAMEWORK ADOPTED (2026-07-09): Step 11A promotion levels.** Research_Board.md now carries stage assignments for every active item; this ledger remains the evidence detail. Gate held at Level 5 with spec frozen through the window despite the inversion day — per Level 5's own rule.

**GATE OOS Day 3 (2026-07-10): 1/3.** Cumulative 6/14; gated -$974.40 vs raw -$1,033.30 — indistinguishable from nothing at current n. Spec frozen, window continues.
**M-12 (new, self-audit): the gate's flow axis violated M-10** — deltaTrend learned on the 1m backfill, scored on the 5m live stream; bar-size-dependent field transferred across parameterizations. HIST flow-dimension evidence downgraded. Fix: 5m-native backfill re-run (Level-0 queue, ~5 min of trader time), then like-for-like re-derivation.
**SH-002 flatten class: forward tally 3 support / 1 contradiction** after 07-10 (both losses via Sell flatten).

**INCIDENT 2026-07-13 (ops): weekend hold, ≈ -$2,904.** Friday's final position never flattened; largest single loss in program history. Booked to risk governance, excluded from decision stats, permanently recorded. → N-5 (P1): EOD flatten guarantee is now the top engineering priority — the only register item with a four-figure realized cost.
**N-6: grid FIFO pairing unreliable across unflattened boundaries; recorder position ledger is session-relative (cannot see carried positions).** Standing rule: post-incident sessions reconstruct from fills.
**GATE OOS Day 4: 1/2 (both blocked). Cumulative 7/16; allowed -$974.40 / blocked -$444.30.** Spec frozen, window continues.
**SH-002: flatten damage forward tally 4/1** (Engulfing_Bear covered via flatten -$524.5; Opening_Flush target captured cleanly).
**Opening family: another live win (+$94.5)** — forward tally unbroken.
**Value Context: first MIGRATING-state winner** (Opening_Flush) — last week's 0-for-3 cell takes its first counter-datum, exactly as the library is designed to accumulate.

**TWO-ENGINE ARCHITECTURE ADOPTED (2026-07-13, trader directive).** 1m stream is the Execution Context Engine, permanent; all prior 'disable the 1m chart' recommendations RETRACTED. DI-1 CLOSED as expected behavior under the dual-engine design — the emitters still disagree, by design; the interpretation problem is solved, not the disagreement itself.
**GATE — PARALLEL-TRACK CORRECTION (M-12 applied):** re-scored OOS window with 1m-engine flow (like-for-like with HIST learning): **12/17 correct; allowed +$22.10 / blocked -$1,440.80** vs frozen 5m track 7/16 / -$974.40 allowed. LABEL: retro-corrected — the rule was pre-registered but the re-score follows observed failure; therefore ONLY FORWARD 1m-track performance counts toward promotion. Both tracks run in parallel from 2026-07-14. Residual misses cluster on the migration day → SH-010 stays open as the conditioning layer.

**STEP X+1 INSTALLED (2026-07-13): Contextual Opportunity population opened.** Rules R1–R3 frozen; seed session logged 6 candidates. Standing observation OPP-002: the strategy's FTR module is blind to the VAL/VAH level class (fields younger than the setup) — 5 with-trend value-edge retests untaken on a day it lost -$430 elsewhere. Observation only; forward recurrence across day-types required before any promotion. OPP-001: entry captured, 152-pt move banked at 23.6 pts — feeds SH-002's runner-leg question from the winners' side.
