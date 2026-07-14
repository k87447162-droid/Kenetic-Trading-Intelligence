# Contextual Opportunity Log — Second Research Population
> Destination: `docs/03_Research/Contextual_Opportunities.md`
> Step X+1 population: high-quality opportunities the strategy did not take, accumulated alongside actual trades for population-level comparison over hundreds of sessions. Observations only; promotion via the normal framework.
> METHOD NOTE (permanent): qualification rules are mechanical and use at-the-moment fields only; outcome annotations (45-min MFE/MAE bounds) are opportunity-quality measures, NOT trade results — no exit rule is applied. RETROACTIVE LIMITATION on seed entries: trigger-scanning avoided outcome peeking, but the RULES themselves were designed after the day's character was known. Rules R1–R3 are hereby frozen; only their forward performance carries evidential weight. (Same discipline as the gate's parallel track.)

## Frozen qualification rules (v1, 2026-07-13)
- **R1 Responsive-at-extension (long):** px < VAL, delta divergence active, deltaTrend ≠ accel_negative, fresh 1m CHOCH_UP (≤3 bars).
- **R2 VAL-retest with trend (short):** cdRTH < -5000, price within 8 pts below VAL (retesting the edge from below), deltaTrend re-accelerating negative.
- **R3 Opening continuation (short):** ≤20 min post-open, volPctl ≥90, deltaTrend accel_negative, fresh 1m BOS_DOWN (≤3 bars). (Mirror-long versions symmetric.)

## Session 2026-07-13 — 6 candidates (R1: 0 · R2: 5 · R3: 1)

### OPP-001 · 07:35 · R3 open-continuation short · Classification **B (existing setup, better execution)**
Entry ≈29,645.5 | Risk ≈28 pts (above 1m swing) | Target: measured vs ON low / open drive | 45-min bounds: **MFE +152.2 / MAE -28.5**
5m: gap-down, fresh value forming, initiative selling. 1m: BOS_DOWN age ≤3, volPctl 99, accel_negative. Value: inside forming VA, selling away from POC. Auction: opening drive continuation. Participation: extreme. 
Why strategy ignored it: **it didn't** — Opening_Flush fired at 07:34 and banked +23.6 pts via a 12-second target while the move ran 152. Similarity: Opening_Flush ~90%. The opportunity is in MANAGEMENT (hold/trail a runner leg on drive days), not entry. Confidence: Low (n=1). Supports: SH-002's capture question from the winning side. Contradicts: targets have been the cleanest exits (9/9 HIST) — a trade-off, not a defect.

### OPP-002 · recurring ×5 (10:26, 11:03, 12:15, 12:48, 13:02) · R2 VAL-retest short · Classification **D (existing setup lacking contextual awareness)**
Entries ≈29,558 / 29,536 / 29,515 / 29,501 / 29,507 (each within 8 pts under VAL) | Risk ≈15–25 pts (above VAL reclaim) | Target: rotation extension | 45-min bounds: MFE **+98 / +51 / +61 / +96 / +102** vs MAE -22.5 / -44.5 / -6.8 / -23.8 / -18.0
5m: one-way liquidation over pinned value; price accepted below VAL. 1m: each pullback to the underside of VAL met renewed accel_negative; divergence active on 3 of 5. Value Context: *selling the rejected value edge, with trend, value STABLE* — FTR geometry (~75% similar to Failed_Test_Retest) but at a level class the strategy cannot see: **its level engine watches TODAY_HIGH/POC-type references; VAH/VAL did not exist as fields until v2.1.** All five instances offered ≥2:1 bound-ratios; the strategy took zero, while its actual second trade (Engulfing_Bear, -$524.5) shorted the extension LOW instead of the value edge.
Confidence: Low-single-day, but the recurrence is the point. Supports: matched-conditions rule (with-flow, at-structure). Contradicts: nothing yet; single day-type (liquidation grind) — may be exactly and only a liquidation-day pattern.

## Daily Summary — 2026-07-13
Opportunities: 6. Higher quality than the strategy's actual trades: 5 (every R2 instance dominated the Engulfing_Bear trade on bounds). Better execution only: 1 (OPP-001). Missing contextual filters/awareness: 5 (OPP-002 — a level-class blind spot, not a filter). New setup families: 0. Highest quality: OPP-002 @12:48 (MFE +96 / MAE -24 at volPctl 1 — retest on dead participation). **Implementation justified today: NO** — one session, one day-type, retro-designed rules; R1–R3 now run frozen forward.
