# Research Finding — W27 Correction & Strategy Performance Baseline
> Destination: `docs/08_Daily_Operations/Research_Findings/2026/`
> Supersedes the population attribution in `2026-W27_First_Import.md` (which remains on file as the record of the error).

## The Correction (trader-supplied ground truth, confirmed in data)

The trader identified that SimAccount2 activity was **algorithmic strategy trading**, not discretionary. The NinjaTrader grid export confirms it: every SimAccount2 trade carries a named strategy signal (`Scalp_Failed_Test_Retest_5m`, `Scalp_DIV_...`, `Scalp_Opening_Flush`, etc.).

**Root cause of the misattribution — a recorder defect, now confirmed:** the TIR records *contained* these signal names in `rawSignalName`, yet `OriginClassifier v2` tagged the entries `origin: Manual`. The classification logic failed despite the evidence being present in its input. → Engineering ticket: OriginClassifier must treat non-generic signal names as Strategy. Until fixed, **the origin field is untrustworthy** and population assignment must use signal names.

**Possible residual manual trades on sim:** 13 rows carry generic names (`EntryLong` n=8 net -$72.10, `EntryShort` n=5 net -$204.60) unlike the descriptive strategy names. These are plausibly chart-trader manual entries. Pending trader confirmation (Q-08).

## Corrected Populations (Week 27)

| Population | Trades | Net | Source of truth |
|---|---|---|---|
| STRATEGY (SimAccount2, named signals) | 159 rows | **+$1,653** (approx; +$1,376 incl. generic-name rows) | NT grid (authoritative) |
| HUMAN discretionary (eval accts, full NQ) | 6 | **-$3,970** (computed; no platform export yet) | TIR cash-flow |
| Probable-manual on sim (generic names) | 13 rows | -$277 | NT grid; attribution pending Q-08 |

## Reconciliation (Q-03, partially closed)

Platform SimAccount2 net **+$1,375.95** vs my TIR cash-flow **+$209.65** — a ~$1,166 gap. Verified: point multipliers are correct (MGC spot-check matches to the cent). Primary suspects: the 4 trade groups I excluded as incomplete (positions opened/closed outside recorder uptime) and NT's per-contract FIFO pairing vs my tradeId grouping. **The platform figure is authoritative.** Full leg-level reconciliation queued; until then all TIR-derived dollar figures are labeled approximate.

## Strategy Performance Baseline (STRATEGY track — this is research about the ALGORITHM, not the trader)

- **The workhorse loses; the filter wins.** `Scalp_Failed_Test_Retest` (unfiltered): 101 rows, 38.6% win, **-$609.50**. The same setup gated by delta divergence, `Scalp_DIV_Failed_Test_Retest`: 8 rows, 75% win, **+$2,047.50**. Nearly the entire week's strategy profit came from the DIV-filtered variant. [SH-001, Low confidence — n=8 on the winning side]
- **Exit inefficiency is severe.** MFE capture ratio 9.4%; cumulative end-trade drawdown (giveback) **$12,733** across the week. Example: `Scalp_Opening_Reversal` averaged $619 MFE per trade and still netted negative. [SH-002, Low]
- **Winners work immediately.** Winning rows averaged only $30 MAE; losing rows averaged just $44 MFE — trades that don't move favorably fast rarely recover. Candidate time-stop/early-exit rule. [SH-003, Low]

These belong to a separate research ledger section tagged [STRATEGY]; they say nothing about the trader's decision-making.
