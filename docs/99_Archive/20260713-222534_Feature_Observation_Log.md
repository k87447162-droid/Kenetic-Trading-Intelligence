# Feature Observation Log
> Destination: `docs/03_Research/Feature_Observation_Log.md`
> Purpose: cumulative Step-X record. One entry block per session. Observations only — no significance claimed from single sessions. Features graduate to `docs/06_Data/Features/` definitions once they separate winners/losers across ≥4 weeks.
> Method: features computed no-hindsight from the last stream-A snapshot at or before entry. MFE/MAE/ETD are AUTHORITATIVE platform values (evening grid export, points per contract). METHOD CORRECTION 2026-07-06: bar-bounded MFE/MAE estimation is hereby invalidated for holds under ~2 bars — verified exact on the 25-min trade but overstated 2–80× on 1–4-min holds (5-min bar resolution dominates). Platform export is the standard; the evening grid export joins the daily upload routine.

## Session 2026-07-06 (5 strategy trades, MNQ, 1W/4L, platform net -$527.20; TIR estimate -$505 → reconciled within ~4%)

| id | dir | POCdist | POC ATR | side of value | POCmig/30m | AVWAPvol d | ETH-high d | cdRTH | volRatio | lvls≤1ATR | hold | MFE pts | MAE pts | ETD pts | net$ |
|----|-----|---------|---------|---------------|-----------|-----------|-----------|-------|----------|-----------|------|---------|---------|---------|------|
| T1 | L | +105.8 | 1.9 | above | 0 | +62.4 | -5.0 | +7,332 | 1.36 | 2 | 25m | 28.75 | 41.75 | 56.4 | -56.60 |
| T2 | L | +75.2 | 1.6 | above | **+80.2** | +75.7 | -3.8 | +9,276 | **0.42** | 2 | 4m | 3.75 | 29.0 | 30.9 | -222.40 |
| T3 | L | +69.2 | 1.6 | above | 0 | +54.4 | -27.2 | +9,320 | 0.47 | 2 | 2m | 13.5 | **0.2** | 10.2 | **+29.10** |
| T4 | S | -99.5 | **2.7** | below | 0 | -101.5 | -197.0 | -2,187 | 0.72 | **0** | 1m | 8.25 | 12.0 | 20.4 | -102.40 |
| T5 | L | -10.1 | 0.3 | below (adverse) | 0 | -5.9 | -104.2 | +2,244 | 0.61 | 1 | 2m | **0.1** | 12.25 | 10.8 | -174.90 |

### Per-feature observations (recorded, not concluded)
- **Immediate MFE/MAE asymmetry (true values) — the day's cleanest separator:** the winner NEVER went underwater (MAE 0.2 pts); two of four losers never went favorable (MFE 0.1 and 3.75 pts). Strong single-day support for SH-003's time-stop direction. Candidate feature: `firstBars_MFE_MAE` → refined to `wentAdverseFirst` (binary).
- **The T1 exception:** one loser DID work first — +28.75 pts in hand (≈1R), then a 56.4-pt end-trade drawdown into the full stop. SH-002's giveback signature, now exact.
- **POC distance (ATR-normalized):** 4/5 entries ≥1.6 ATR from value; did not separate W/L alone today (winner also extended). Pairs with adverse-side geometry (SH-009).
- **Side of value, adverse entry:** unique to T5 — which also posted the day's worst MFE (0.1 pts): instantly wrong. Candidate `valueSideAdverse` gains its first outcome datum.
- **Value migration at entry:** only T2 entered with value migrating favorably (+80/30m) — and on volRatio 0.42 it still barely went favorable (MFE 3.75). Candidate interaction `pocMigDir × volRatio` stands.
- **Level vacuum:** T4, zero references within 1 ATR, mid-overnight-range short. Candidate `lvlsIn1ATR`.
- **Overnight-high proximity:** T1/T2 longs within ~5 pts beneath the tracked ETH high; field semantics still need a Data Dictionary entry.
- **No separation today:** volRatio alone, atrRatio, EMA stack, swing label.

### Features Step X requests that CANNOT yet be computed (measurement gaps)
Session VWAP distance (sensor dead — see M-5), VAH/VAL distances (value-area edges not emitted; only POC + pctAbovePOC), tick-resolution MFE/MAE/risk-multiple (TradeOutcome unimplemented), tape speed (counters dead — M-6), liquidity/book features (no L2 — M-7), CHOCH/one-time-framing/IB relationship (not emitted).


## Session 2026-07-07 (3 strategy trades, MNQ, 3W/0L, platform net +$213.50; recorder zip missing — features from Output-save snapshots only)

| id | dir | POCdist | POC ATR | side of value | value state | deltaAccel@entry | volRatio | hold | MFE pts | MAE pts | ETD pts | net$ |
|----|-----|---------|---------|---------------|-------------|------------------|----------|------|---------|---------|---------|------|
| T1 | S | -53 to -113 | 1.8-2.8 | below | STALE (unmigrated) | accel_negative (live) | **1.89→6.67** | 1.7m | 33.8 | **48.0** | 2.6 | +121.80 |
| T2 | S | -192.8 | **4.5** | below | STALE (unmigrated) | accel_negative (live) | 0.97 | 1.0m | 23.2 | 3.6 | 14.7 | +86.10 |
| T3 | L | +221.5 | 4.3 | above (new value) | RELOCATED to lows | oscillating (dying) | 0.64 | 2.2m | 12.5 | 7.0 | 11.2 | +5.60 |

### Per-feature observations (cumulative n=8)
- **`pocDist_ATR` (naive): second contradiction.** T2 won at -4.5 ATR extension. Naive extension is now 2 contradictions in 8. **Interaction form strengthening:** extension WITH live accelerating delta and participation → winners (T2, and T1 amid volRatio 6.67); extension with DYING participation → Monday's losers and today's T3 scratch. Sharpened candidate: `pocDist_ATR × deltaAccel × volRatio`.
- **`wentAdverseFirst`: one support, one exception class.** T2 immediate work (MAE 3.6). T1 won AFTER 48 pts adverse — exception: opening-volatility entries where the stop is necessarily wide. Sub-feature candidate: `minsSinceOpen < 5`.
- **Risk multiple observed directly for the first time:** T1 risked ≥48 pts for a 31.75-pt target (<0.7R geometry) and won — wide-stop opening trades are a distinct risk class; flag for accumulation.
- **Exit-type split (new):** across both days, profit-target and trailing exits preserved MFE well (T1 ETD 2.6; trails kept 53% and rescued T3); the giveback damage (Mon T1: 56.4-pt ETD) came via a full-stop exit after ≈1R MFE. Candidate: `exitType` as a conditioning variable for SH-002.
- **Value relocation as a tradable event (new candidate):** the day's structure hinged on POC relocating 350 pts to the lows at ~09:30 — no setup referenced it. Feature: `valueRelocation` (binary, with time-since).
- **No separation added:** EMA stack, swing labels (COMPRESSED during a 300-pt repair).

## Session 2026-07-07 (evening) — HISTORICAL BACKFILL BLOCK (n=154 MNQ trades, May 11 – Jul 7, mode=HIST)
Feature evolution verdicts (per protocol: strengthened / weakened / contradicted / merged / removed / promoted):
- `pocDist_ATR` → **merged** into `matched_conditions` (extension × flow 2×2); raw-points form used (M-10: 1m/5m ATR incomparable).
- `deltaAccel alignment` → **strengthened**; only meaningful jointly with location. PROMOTED to tracked research variable as one axis of `matched_conditions`.
- `valueSideAdverse` → **contradicted at n=44** (adverse-side entries won 68.2%). REMOVED as a negative feature; flagged for possible inversion study (mean-reversion-toward-value).
- `inVA` (VAH/VAL location) → **new, opens strong** (+$1,248 in / -$1,262 out). PROMOTED to tracked research variable. First return on the v2.1 instrumentation.
- `wentAdverseFirst` → **weakened** (SH-003 result); keep recording, stop privileging.
- `exitType` → **strengthened & sharpened**: target ≈ full capture; trail net-positive; generic flatten = damage class.
- `volPctl` → **new exploratory**: mid-bucket dominant (+$3,341, n=32); pre-registered retest required before promotion.
- `distToONlow`, `pdlRetest`, `timeSinceValueMigration` → insufficient joined coverage this pass; carry forward.


## Session 2026-07-07 (evening, pass 2) — additional extraction from the same backfill (exploratory unless noted)
- **`agreementGate` (pre-registered composite of established cells): ALLOWED n=71 +$718 vs BLOCKED n=83 -$732** — a +$1,450 swing on a breakeven program, trading 46% of entries. Win rates nearly equal (53.5/55.4): the edge is winner SIZE under matched conditions, not hit rate.
- **`FTR × agreementGate` — THE backtest object: n=30 ALLOWED +$1,837 (avg +$61.24) vs n=38 BLOCKED -$935.** The gate doubles the program's best family while halving its trade count.
- `ORrelation` (new, day-old instrumentation): INSIDE +$1,262 / BELOW -$1,457 — strongest single location bucket yet; exploratory, forward confirmation required.
- `IBhour` (new time feature): entries 15–60 min post-open -$1,883 (n=29) — candidate avoid-window.
- `migAge`: stale>120m +$1,132 vs 10–45m -$816 — value stability as an entry condition; complements matched-conditions.
- Day-of-week: dispersion recorded (Wed +$1,091, Tue -$895), NO story imposed — cells too small, classic multiple-comparisons bait.
- **Multiple-comparisons flag:** B-cuts above are exploratory slices of one dataset; only `agreementGate` and its FTR application were pre-registered compositions of previously established cells.


## Session 2026-07-08 (first live v2.1 day, out-of-sample)
- `agreementGate`: **5/5 correct** — first forward evidence; all blocks were the `inVA×withFlow` chasing cell (backfill's -$1,001 cell behaving identically forward). n=5.
- `exitType`: flatten-damage class confirmed forward (day's largest loss via generic Sell).
- `IBrelation`: first LIVE IB data — session broke IB both sides, resolved up; today's winner (#3) entered on the IB-breakdown side below VAL. Coverage begins.
- Giveback datum: Sim101 trade MFE 79.8 pts → -$33 net (exit `EntryLong_GB`) — largest single-trade giveback ratio recorded; experiment-account population.
- `volPctl`: entries at 97–100 percentile (trade 1) and mid-range — too few for cells; accumulating.


## Session 2026-07-09 (Gate OOS Day 2 — inversion day)
- `agreementGate`: **contradicted hard** (0/6) — see SH-010. The feature is not removed; it is revealed as CONDITIONAL. This is what feature evolution looks like.
- `flowState`: definition gap exposed — binary withFlow conflates neutral with against; three-state variant registered for testing.
- `valueMigration` (direction+age): promoted from carry-forward to PRIMARY conditioning candidate — the instrumentation timestamped both POC relocations that broke the gate.
- `openingFamily`: strengthened again (+$58.30; every sampled period positive).
- `exitType`: first contradicting instance (winning Sell flatten).
- Context Library day-character entries now 4: crash / neutral-up / gap-and-go — the day-type vocabulary SH-010 needs is accumulating naturally.


## Session 2026-07-10 (Gate OOS Day 3) + Week-28 feature rollup
- `agreementGate`: extension+flow cell now -$630 forward vs +$1,482 HIST — cell-level contradiction concentrated exactly where M-12 predicts (flow axis parameter-invalid). Feature holds at 'conditional, under re-derivation'.
- `exitType` flatten class: +2 supports (week total 3/1).
- `migAge` at losing extensions: 125/175 (stationary) — directionally SH-010-consistent, n=2.
- New setup logged: Engulfing_Bull (pattern family, first sighting, n=1).
- Week-28 library: n≈175 trade-rows, 5 live day-characters, all v2.1 features live except three-state flow (Level-0 pending).
