# Value Context Library
> Destination: `docs/04_Digital_Twin/Value_Context_Library.md`
> Purpose: value treated as an independent analytical object. Accumulates per-session value-behavior profiles and per-trade value classifications until Value Context either consistently explains outcome divergence among similar setups — or demonstrably doesn't. Promotion of any observation goes through the Strategy Promotion Framework; nothing here is a trade rule.
> Created 2026-07-10 with a retroactive pass over the live week (5m streams, parameter-robust fields only per M-10/M-12).

## Operational definitions (fixed; changing them is a versioned event)
- **Location:** inside value = VAL ≤ entry ≤ VAH; else above VAH / below VAL.
- **Toward/away:** toward value = long below POC or short above POC; else away.
- **Value state at entry (from `valueMigAgeMin`):** MIGRATING ≤30 min · TRANSITIONAL 31–120 · STABLE >120. (M-11 caveat: POC flip-flop resets the counter; persistence criterion pending — states are lower-bound estimates of stability.)
- **Session relocation count:** POC jumps >30 pts between consecutive 5m snapshots.
- Classification format: *"{Buying|Selling} {location}, {toward|away from} value, value {STATE}"*. 'Unknown' when fields are absent — never forced.

## Session value profiles (live week)
| Date | Relocations >30 | Net POC move | POC travel | Value behavior (narrative) |
|---|---|---|---|---|
| 07-06 | — (pre-v2.1, no migAge) | ~+110 then pinned | ~110 | Migrated up with morning trend, then STATIONARY through the liquidation — price left value, value never followed down. |
| 07-07 | 12* | -104 | 356 | Relocated to the lows (acceptance), chased the repair up, rejected. *Count contaminated by the dual-stream interleave (pre-inst-tag); narrative from the reconstruction stands. |
| 07-08 | 3 | -67 | 104 | Stale open (665 min), relocated DOWN toward price, then followed the afternoon rally up — value chased price both directions. |
| 07-09 | 2 | **+367** | 367 | The migration day: value chased the rally in two large relocations — full acceptance of recovery. Broke the gate (SH-010). |
| 07-10 | 2 | +53 | 110 | Early migration up, then PINNED >3h while price ground +175 above — price accepted away? No: participation faded; unresolved extension over stable value into the weekend. |

Base-rate check vs the (1m) backfill library: live 5m relocation counts (2–3/day) run well below the 1m-measured median of 5/day — measurement-resolution effect, recorded, not a market change.

## Per-trade classifications — live week (n=15 classifiable signals)
07-08: Buying inside/toward/STABLE -$33 · Selling inside/away/MIGRATING -$23 · Selling below-VAL/away/TRANS **+$66** · Buying inside/away/STABLE +$9 · Buying inside/away/MIGRATING **-$184**
07-09: Buying above-VAH/away/STABLE +$29, +$29 · Buying inside/away/MIGRATING -$12 · Buying inside/away/TRANS **+$151** · Selling inside/toward/TRANS -$101, **-$306** · Buying above-VAH/away/TRANS +$28
07-10: Buying above-VAH/away/STABLE **-$455** · Selling above-VAH/toward/TRANS -$11 · Buying above-VAH/away/STABLE -$175

## Week-1 live cuts (n≤8 per cell — OBSERVATIONS ONLY, with HIST tensions recorded)
- **Toward-value entries: 0-for-4, -$450.90.** TENSION: the HIST set showed adverse-side/toward-value entries at 68% win (n=44). Both stand in the library until day-type-conditioned cells can arbitrate; this week was one continuous trend-cycle, HIST spanned two mixed months.
- **Value-MIGRATING at entry: 0-for-3.** Directionally consistent with SH-010's mechanism at per-trade grain.
- **Inside-value entries: -$500 (25% win, n=8).** TENSION vs HIST inside-VA +$1,248 — same caveat as above. VA fields are parameter-robust (not an M-12 artifact), so this tension is real and regime-flavored.
- Above-VAH: -$555 across 6, but containing both winners (trend day) and the week's worst loss (stationary day) — the SH-010 conditioning variable visibly at work inside one cell.

## Accumulation rules
One session block per review (profile + classifications + cuts). Tensions with HIST are recorded side-by-side, never resolved by preference. Cells graduate to Research Board Level 2 only when a relationship holds across ≥3 distinct day-types with n≥15 per side. The library's success criterion is explanatory: does Value Context separate outcomes among otherwise-similar setups? Its failure is also a finding.


## Session 2026-07-13
Profile: gap-down liquidation; value formed FRESH at the open (migAge 0 — first observed), two early relocations, then POC pinned 29,656 from 08:45 while price left value downward, closing below VAL on fading participation — *rejected from value, trending away, unresolved*. Second instance of the liquidation-over-stationary-value character (cf. 07-06).
Trades: Selling inside/away/MIGRATING **+$94.5** (first MIGRATING winner — cell now 1/4) · Selling inside/away/TRANSITIONAL -$524.5.
Note: session reconstructed from FILLS (grid unreliable post-incident, N-6).
