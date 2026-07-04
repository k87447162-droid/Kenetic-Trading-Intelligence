# Architecture Decision Log

**Document:** `docs/01_Architecture/00a_Architecture_Decision_Log.md`
**Status:** Living — append-only.
**Last updated:** 2026-06-25
**Companion to:** `00_Architecture_Principles.md`

> This is the permanent record of *why* major architectural decisions were made. Each entry is an Architecture Decision Record (ADR). A year from now, when the reasoning behind a choice is no longer obvious, this is where the answer lives. In keeping with Law 1 (history is immutable), entries are never deleted — a reversed decision is marked **Superseded** and the entry that replaces it references it.

---

## How to use this log

- Every accepted architectural decision gets the next sequential ID: `ADR-001`, `ADR-002`, and so on. IDs are never reused.
- **Status values:** `Proposed`, `Accepted`, `Superseded`, `Deprecated`.
- Superseding a decision does not delete it. The old ADR's status becomes `Superseded by ADR-NNN`; the new ADR's context explains the contradiction or limitation that justified the change (the bar set in Section 8 of the Principles).
- An ADR records a single decision. Keep each one short: the context that forced a choice, the decision, and the consequences that follow.

**Entry template**

```
### ADR-NNN — <short title>

- Status: <Proposed | Accepted | Superseded by ADR-MMM | Deprecated> (<date>)
- Context: <the situation or tension that required a decision>
- Decision: <what was decided>
- Consequences: <what this implies, including the trade-off accepted>
- Related: <law / document references>
```

---

## Decision index

| ID | Decision | Status |
|------|------------------------------------------------|----------|
| ADR-001 | Raw snapshots only | Accepted |
| ADR-002 | Snapshot capture is deterministic | Accepted |
| ADR-003 | One-way data lineage | Accepted |
| ADR-004 | Recorder / Research / Strategy separation | Accepted |
| ADR-005 | SQLite operational store + Parquet analytics | Accepted |
| ADR-006 | Features are versioned | Accepted |
| ADR-007 | Research never modifies live strategy | Accepted |

---

## Records

### ADR-001 — Raw snapshots only

- Status: Accepted (2026-06-25)
- Context: A snapshot could store either raw market state or an enriched, pre-computed picture (levels, features, events resolved at capture). Enriching at capture time would force regeneration of all historical snapshots whenever a new calculation is invented — which becomes impossible at scale.
- Decision: A snapshot stores **raw market state only** — price, OHLC, volume, delta, DOM, tape, bid/ask, time, instrument, session. Levels, features, events, research, and AI outputs are computed *from* snapshots and never written *into* them.
- Consequences: Any future calculation applies to the entire history by forward computation; nothing historical is ever regenerated. The cost is an assembly/join step to build enriched views on demand. This is the decision the platform's long-term evolvability rests on.
- Related: Principles Law 2 (2.2), Law 1 (1.3).

### ADR-002 — Snapshot capture is deterministic

- Status: Accepted (2026-06-25)
- Context: Snapshots could be triggered by computed conditions (absorption, volume spike, AVWAP touch, delta divergence) or at deterministic points. A computed trigger would make the raw layer depend on the feature layer.
- Decision: Snapshots are captured at **deterministic points** — every completed bar per enabled timeframe, every execution (entry, partial, scale, exit, reversal), and optional user-defined intervals. Never triggered by a derived feature or event.
- Consequences: Capture never depends on the computed layer, which keeps the dependency graph acyclic. Because a moment that is not captured is unrecoverable, the capture cadence is a deliberate, recorded decision rather than an emergent one.
- Related: Principles Law 2 (2.3), Law 4 (4.5).

### ADR-003 — One-way data lineage

- Status: Accepted (2026-06-25)
- Context: Modules could be permitted to write back into upstream stores ("convenience writebacks"), or lineage could be held strictly forward.
- Decision: Data lineage flows in **one direction only**. Downstream modules may read upstream state and registries and may be configured externally, but never mutate upstream historical records.
- Consequences: The entire system can be reconstructed by replaying the forward chain, which makes it reproducible and auditable. The standing cost is discipline: writebacks must never be introduced for convenience, even when they would be locally easier.
- Related: Principles Law 4 (4.1, 4.2), Law 1.

### ADR-004 — Recorder / Research / Strategy separation

- Status: Accepted (2026-06-25)
- Context: Recording, research, and execution could share code and state, or be isolated into distinct concerns.
- Decision: These are **three separate concerns**. The recorder records facts, the research engine analyzes facts, and the strategy executes validated rules. They never mix.
- Consequences: Research cannot accidentally alter live trading behavior or recorded data, and the boundary between "what we observed" and "what we decided" stays intact. The cost is additional modules and explicit contracts between them.
- Related: Principles Law 5 (5.1), Law 4.

### ADR-005 — SQLite operational store + Parquet analytics

- Status: Accepted (2026-06-25)
- Context: The platform needs both a low-latency operational store for append-only recording and an efficient analytical store for large-scale research.
- Decision: Use **SQLite** as the operational append store, **Parquet** as the columnar analytics format, and **DuckDB** as the analytical query engine over Parquet. The analytics representation is produced from the operational store by one-way export. Concrete tables and the export path are defined in `02_Database_Schema.md`.
- Consequences: Fast local writes plus efficient columnar reads for Python, R, and ML, using portable open formats. The trade-off is two representations of the data that must be kept consistent — which is safe precisely because the export is one-way (Law 4) and append-only (Law 1).
- Related: Principles Law 2 (2.4), Law 4; detailed in `02_Database_Schema.md`.

### ADR-006 — Features are versioned

- Status: Accepted (2026-06-25)
- Context: Feature calculations evolve over time, but historical values must remain reproducible and comparable.
- Decision: Every feature — and every event, calculation, schema, and research artifact — carries the **version** of the logic that produced it. Recomputing under a new version appends a new versioned record rather than overwriting the old one.
- Consequences: Any historical value can be reproduced given its version, and multiple versions coexist for comparison. The cost is version metadata on every derived record and the registry discipline to manage it.
- Related: Principles Law 2 (2.1, 2.4), Law 1.

### ADR-007 — Research never modifies live strategy

- Status: Accepted (2026-06-25)
- Context: Research and AI outputs could feed directly into live trading, or be gated behind validation.
- Decision: **Only validated edges may influence trading logic.** No hypothesis, feature, or signal influences live trading until it satisfies the validation framework. Research and AI read from the data model and never write back into recorded or research data in any way that violates Law 1 or Law 4.
- Consequences: Live behavior and the integrity of the research record are both protected. The cost is a mandatory validation gate before any finding can reach execution — which is the intended friction, not an obstacle.
- Related: Principles Law 5 (5.2, 5.3).

---

*End of Architecture Decision Log. Append new records below ADR-007; never edit accepted entries in place.*
