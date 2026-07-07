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

**SH-002 — Strategy exits capture ~9% of available MFE ($12,733 weekly giveback).**
Confidence: **Low** (one week). Next test: exit-rule variants on recorded MFE paths.

**SH-003 — Winners work immediately (avg MAE $30); non-performing trades rarely recover (losers avg MFE $44).**
Confidence: **Low**. Next test: time-to-MFE distribution; simulated time-stop.

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

**SH-009 (new) — Entries at maximal extension from value, or at the adverse side of value, are the loss engine.**
Confidence: **Speculative** | Sample: 5 trades, 1 session (independent reconstruction).
Statement: the strategy's losers share one geometry the labels don't encode — longs entered +73/+100 pts above stationary/rising POC, a short entered -100 pts below unmigrated POC at the low of a completed liquidation, and a long entered into POC-from-below (first-test resistance). 4/5 trades fit; partial contradiction: the lone winner (T3) was also extended above value and won +5 via trail.
Supporting: 2026-07-06 all four losses. Contradicting: T3. Missing: everything — one session.
Next test: compute (pocDist/ATR, side, POC-migration, volRatio) at every entry from the daily dumps; tally win rate by value-location bucket across the shadow window. This variable is computable regardless of label quality and is the refined form of the trader's 'no-context trades' intuition (supersedes the context-availability half of SH-008; R-003 updated).

## Rejected / Refined — [STRATEGY]

**R-003 — 'No-context trades are the garbage' (trader intuition, naive form).** Partially rejected 2026-07-06: no-context entries are breakeven noise, not the loss source; the garbage is regime-specific (Chopping/Drifting). The intuition was directionally right — the strategy fires indiscriminately — but the discriminator is regime value, not context availability. Refined into SH-008.

## Retired / Rejected

**R-001** — "Reversal-forming entries lose money" (pooled): aggregation artifact. Rejected 2026-07-06.
**R-002** — "Discretionary entry, systematized exit" (B-01) and "human winner-management capacity" (old H-008/S-02): **misattribution** — the underlying trades were algorithmic. The +$2,035 MGC hold belongs to the strategy's DIV/ScalpHalf setups. Rejected 2026-07-06 on trader-supplied ground truth; root cause = OriginClassifier v2 defect (signal names present, origin still 'Manual').
**H-008, H-009 (v1)** — retired as [HUMAN] entries; content reborn as SH-002/SH-007 under [STRATEGY].
