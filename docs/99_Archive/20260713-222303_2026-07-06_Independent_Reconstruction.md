# Independent Market Reconstruction — 2026-07-06 (supersedes §Market Reconstruction of the Daily Review)
> Destination: `docs/08_Daily_Operations/Daily_Reviews/2026/2026-07-06_Independent_Reconstruction.md`
> Method: 137 MNQ + 42 NQ raw state snapshots parsed independently (price, session extremes, POC value+distance, cumulative delta 24h/RTH, volume ratio, AVWAP side/slope, EMA values, ATR ratio). Strategy-emitted labels (regime, setup name, quality score, openType, swing/BOS) treated as hypotheses under test, not inputs. Trade times converted UTC→MT.

## Correction to the prior review (label-induced error)
The Daily Review stated "the gap did not extend: the session faded." **That was wrong.** It was inherited from the regime labels, not the data. The raw record shows the gap DID extend — a genuine initiative morning — followed by a violent midday liquidation that returned the entire extension. The corrected session narrative:

## The day, reconstructed from raw fields only
**Overnight/premarket (NQ stream, 06:32–07:23 MT):** balancing 29845–29905 on mildly negative 24h delta — a quiet two-sided overnight balance holding the weekend gap-up (~+390 pts vs prior close reference).

**RTH open → 10:15 MT — initiative buying, gap-and-go:** within minutes of the 07:30 open, delta flipped hard positive (cd24 +128 → +758 by 07:52; cdRTH built to **+10,300** by 10:15). Price drove 29900 → 30094 (+190). POC migrated upward with price (29893 → 29922 → 30003) — value FOLLOWED price, the signature of acceptance, not a fade. Price held above a rising volume-anchored AVWAP throughout. One-timeframe higher.

**10:15–11:58 MT — liquidation, full retrace:** cumulative RTH delta reversed from +10,300 to **-2,662 (a ~13,000-contract swing)**; price returned the entire morning extension (30094 → 29895). Critically, POC did NOT migrate down — it stayed at 30003 while price left it. Price moving rapidly AWAY from stationary value is inventory liquidation, not new value discovery. The morning's gain fully round-tripped.

**12:00–13:30 MT — sub-value repair and balance:** price rotated 29895 → 29990, stalling repeatedly at the UNDERSIDE of value (POC 30003 − ~13), delta oscillating. Balance forming BELOW the day's value area: weak acceptance, with POC acting as overhead resistance from below.

## The five trades against this reconstruction (labels vs independent read)
**T1 — 08:16 long @30028 (label: ReversalForming; attrib: MAIN_RETEST of "POC 30022.5", q8).** Independent read: entry ~+100 pts ABOVE the state-recorded POC (29922) during initiative buying — a with-flow entry but at extension, not at a POC retest. The attribution's "POC 30022.5" contradicts the state POC by ~100 pts (→ DI-2). Stopped -27 in a normal pullback; market then traded +65 higher. Direction right, location extended, label wrong on both regime and level.

**T2 — 09:51 long 4-lot @30078 (label: TrendingUp; FTR at session high, q7).** Independent read agrees the trend was intact (HH-HL, delta rising, value migrating up) — but the entry bought the session-high retest **+73 above value on volRatio 0.42** (participation drying into the highs). Late-stage continuation at maximal extension. The regime label was "right" and still insufficient: it carries no value-distance or participation dimension.

**T3 — 10:08 long 4-lot @30072, exited +5 at 10:10.** The three regime sources disagree at trade time: TIR snapshot said Drifting→Chopping; one dump stream said TRENDING_UP(age 4–6); the interleaved stream said CHOPPING/REVERSAL_FORMING (→ DI-1). Independently: same extension zone as T2, minutes before the liquidation began. The +5 trail-out preceded a -190 pt break — favorable timing by exit rule, not by foresight.

**T4 — 12:00 short 4-lot @29903 (label: Chopping; EMA21 resistance, "TREND_CONTINUATION", q5).** Independent read: entry at the LOW of the completed -190 liquidation, **-100 pts below stationary value**, after RTH delta had already bottomed. Auction logic: maximal downside extension from unmigrated value is a responsive-BUY zone; the continuation premise was ~90 minutes stale. Stopped -11.5; price repaired +85 to the value underside. Sharpest label-vs-auction conflict of the day alongside T5.

**T5 — 12:27 long 2+2 @29992 (attrib: FTR long at "POC 29984" as support, q7).** Independent read: the +85 repair had ALREADY completed to the value-area underside; POC (state: 30003) sat OVERHEAD. From below, POC is first-test resistance, not support — the attribution used the same level with the opposite auction meaning (and a POC value 19 pts off the state's, → DI-2). Stopped -20.6 as the value edge rejected.

## The unifying pattern (feeds SH-009)
All four losers share one geometry the labels never encode: **entries at maximal distance from value in the direction of the trade, or at the adverse side of value** — longs +73/+100 above POC, a short -100 below POC, a long into POC-from-below. The strategy reacts to *levels*; it does not know *where value is, which side of it price stands, or whether value is migrating*. The trader's phrase "no-context trades" is hereby made precise: **no value-location context**.

## Data-integrity findings (all confirmed in raw output, all pre-existing — surfaced by this method)
- **DI-1 (critical): at least two indicator instances dump interleaved, contradictory states under sym=MNQ.** Adjacent lines (e.g., 11:45 vs 11:46) disagree on cd24, EMA stack, swing, regime, and carry independent regime-age counters (one counting CHOPPING 1→13, the other REVERSAL_FORMING 0→50). "Regime" is currently not one signal. Every regime-conditioned statistic (SH-004/005, the W27 splits, the SH-008 shadow design) inherits this ambiguity.
- **DI-2: ENTRY_ATTRIB level values conflict with state values** (POC 30022.5 vs 29922; 29984 vs 30003) — different POC definitions (developing-RTH vs 24h?) or an attribution defect. Every "level reaction" attribution is unverified until definitions are reconciled.
- **DI-3: structure fields went stale through the break** — one stream reported swing=HH-HL_UPTREND and bos=none continuously across a -190 pt, multi-swing breakdown.
- **DI-4: openType emitted both GAP_AND_GO and GAP_FADE** during the same session (consistent with DI-1's dual streams).
- **Coverage:** MNQ dumps begin 08:14 MT (open auction covered only by the NQ stream, which ends 08:07 — a 7-minute handoff gap); 2-min cadence thereafter; no intra-snapshot path, no MAE/MFE.

## Consequences recorded in the ledger
SH-004/SH-005 receive a measurement-reliability caveat (regime provenance undefined). SH-008's gate variable is REDESIGNED away from the regime label toward independently computable value-location metrics (POC distance in ATR, side of value, value-migration direction, volRatio) — computable from the dumps regardless of label quality. New SH-009 opened. Methods rule added: strategy-emitted classifications are hypotheses, never inputs, in all future session analysis.
