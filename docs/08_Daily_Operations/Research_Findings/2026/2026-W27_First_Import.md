# Research Finding — First Session Import (Week 27: 2026-06-29 to 2026-07-03)

> Destination: `docs/08_Daily_Operations/Research_Findings/2026/`
> Source data: KeneticTradeRecorder `tir-*.jsonl` (321 execution legs) + `phase1-*.jsonl` observations, 5 sessions.
> Analyst: KENETIC Research (AI). Method: cash-flow P&L reconstruction from execution legs using standard CME point values. **P&L figures are computed, not platform-reported — pending confirmation against NinjaTrader (see Data Quality).**

## Summary of the Observation Set (FACTS)

- 321 execution legs → **105 completed round-trip trades** (4 incomplete groups excluded).
- Populations: **SIM** (SimAccount2, 96 trades; Sim101, 3 apparent test trades) and **EVAL** (two TDFYSL accounts, 6 trades). These are analyzed separately throughout.
- 8 instruments traded in 5 days: MGC, MCL, MES, MNQ, MYM, M2K, MBT (micros) and full-size NQ.
- Execution architecture: **107/109 entries Manual; 91/105 exits Strategy-managed, 8 ATM** — discretionary entries with automated exit management.
- Week net (computed): **SIM +$209.65** (gross +$734.75, commissions -$525.10) | **EVAL -$3,970.00** | combined ≈ **-$3,763**.
- Expectancy structure: 36.2% win rate, avg win $114.35 vs avg loss $121.02 → payoff ratio ≈ 0.94. At this win rate, structurally negative before tails.
- Tail domination: worst 5 trades = 62% of all gross losses; top 3 winners = 64% of all gross wins.

## Market Reconstruction — the two loss events

**2026-07-02, 12:35–12:41 UTC (8:35–8:41 a.m. ET), eval account, full NQ.** The June Employment Situation report was released at 8:30 a.m. ET that day (+57K vs ~113K expected — a downside miss; verified via BLS/press). Five minutes later: trade 1, 3-lot NQ long, +4.5 pts, +$270 in 37 seconds. Then within 4 minutes: instant 3-lot long stopped in 17 seconds (-$750); 2-lot long during a 25-point 10-second drop (-$1,010); direction flip to short, stopped (-$305); flip back long (-$155). Net: **-$1,950 in under 6 minutes**, premarket, in post-NFP volatility. The trader then switched to SIM micros for the remaining 28 trades of the day.

**2026-07-03, 13:15 UTC (9:15 a.m. ET), second eval account, full NQ.** Instant 4-lot short (entry + same-millisecond 3-lot add) 15 minutes before the RTH open on a holiday-shortened session (July 4 observed). Exited 4.7 minutes later, -25.25 pts × 4 = **-$2,020**.

All six EVAL trades of the week were: full-size NQ, premarket, ≤5 minutes, entered at full size instantly.

## Statistical Findings (per-cut tables in the analysis record)

- **Regime (context-bearing SIM trades):** Chopping 17.6% win, -$1,342 (n=17). ReversalForming *after removing EVAL contamination*: 52.9% win, **+$1,954** (n=17) — the raw pooled number was -$2,016, an aggregation artifact of mixing populations. TrendingUp 66.7% win (n=9).
- **Commission drag:** SimAccount2 commissions consumed 71% of gross profit (21 trades/day average).
- **Time of day (MT):** 08:00 block strongest (n=28, 42.9% win, +$593); losses concentrated 06:00–07:00 (news window) and 10:00.
- **Instrument:** MNQ 58.3% win, +$696 (n=12); MGC +$1,369 driven by one trade; NQ (EVAL) -$3,970 (n=6).
- **The week's one large winner:** MGC short, 8.2-hour hold, one spaced scale-in, two scale-outs, ReversalForming regime, +$2,035.80.

## Data Quality Assessment

1. **No MAE/MFE** — TradeOutcome is still a scaffold in the recorder. Trade-management quality is currently unmeasurable. Highest-value recorder improvement.
2. **37% of legs lack market context** (`hasContext: false`), concentrated overnight/premarket — precisely where the largest losses occurred. Context coverage is worst where it matters most.
3. P&L computed from cash flow with standard CME multipliers (MGC $10/pt, MES $5, MNQ $2, M2K $5, MYM $0.50, MCL $100, MBT $0.10, NQ $20). EVAL commissions recorded as zero. **Requires one-time confirmation against platform trade reports.**
4. 2026-07-03 was holiday-affected (early close); flagged, not pooled blind.
5. Sim101's 3 trades (±$2) appear to be recorder testing; excluded from behavioral analysis.
6. Indicator state dumps are not persisted to disk — the SessionLevels context layer currently dies with the Output window.

## What Changed in the Digital Twin

Strengthened: nothing (no prior knowledge existed).
Discovered: H-001 through H-008 opened (see Hypothesis Ledger); Behavioral Tendencies B-01–B-05 recorded; first entries in Strengths, Weaknesses, Market Preferences — all hypothesis-grade.
Rejected: the naive pooled conclusion "reversal-forming entries lose money" — an artifact of population mixing.
Unknown: everything at validated grade. One week validates nothing.
