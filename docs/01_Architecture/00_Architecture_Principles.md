# Architecture Principles

**Document:** `docs/01_Architecture/00_Architecture_Principles.md`
**Version:** 1.0 — Frozen
**Status:** Ratified. Amendments follow the process in Section 8.
**Last updated:** 2026-06-25
**Applies to:** Every architecture, engine, database, research, and implementation document in this repository.

> This document is the constitution of the architecture. It defines the laws that every other technical document and every line of code must obey. It contains no modules, no database tables, no engine designs, and no feature definitions — only the principles those things must conform to. When any downstream document conflicts with this one, this document wins, and the conflict must be resolved here before code is written.

---

## How to read this document

There are exactly **five laws**. Everything else in the platform's design — versioning, registries, modularity, deterministic capture, references instead of copies — is a *corollary* that follows from one of them. Five laws are memorable; nine are not. If you can recite the five laws, you can derive almost every architectural decision in the platform without consulting anything else.

Each law is stated, justified in one line, and followed by the corollaries (the operational rules) it implies.

---

## Decisions locked in this document

These are the specific, potentially contentious choices this document freezes. They are listed here so they can be reviewed as a set rather than discovered in prose:

1. **The architecture is governed by five foundational laws** (below), with all prior principles folded in as corollaries.
2. **A snapshot is raw, never enriched.** It records what the market *was*, never what the market *meant*. (Law 2)
3. **Snapshots are captured at deterministic points, never at computed ones** — every completed bar per enabled timeframe, every execution, and optional user-defined intervals; never triggered by a derived feature or event. (Law 2)
4. **"Disposable" derived data means *regenerable*, not *deletable in place*.** Derived records are versioned and retained; they are never silently overwritten. (Laws 1 and 2)
5. **Events are interpretations, not market data.** A snapshot never contains an event. (Law 4)
6. **Trades reference context by stable ID; they never duplicate it.** (Law 4)
7. **This document is itself append-only in spirit:** it is amended additively and by version, never silently rewritten. (Section 8)

If any of these is wrong, it is far cheaper to change here than after ten documents cite it.

---

## Law 1 — History is immutable

**Recorded market facts are append-only and are never modified.**

*Why:* Reproducibility and auditability are only possible if the past cannot change. Every research conclusion, every backtest, and every audit is anchored to the exact data that existed at a moment in time. If history can be edited, none of those can be trusted.

**Corollaries**

- **1.1 Append-only.** The system only ever adds records. There is no operation that edits or deletes a recorded market fact in place.
- **1.2 Time is the root axis.** The deeper form of this law is temporal, not spatial: a record, once written, is fixed for all time. The one-way module diagram in Law 4 is a *consequence* of this, not a separate rule.
- **1.3 Snapshots, once written, are never altered.** A snapshot is a permanent recording of a moment. Nothing is ever added to or removed from a snapshot after capture.
- **1.4 Corrections are appended, never applied.** If recorded data is later found to be wrong (a feed glitch, a gap), the original record is preserved and a *correction* or *supersession* record is appended that points to it. The incorrect value remains visible in history; what changes is the record that supersedes it. Nothing is erased.
- **1.5 History only grows.** The database gets larger over time; it never gets rewritten.

---

## Law 2 — Raw data is the source of truth

**Every derived value must be reproducible from raw data and versioned calculations.**

*Why:* If raw data is the source of truth, the platform can invent new analysis at any point in the future and apply it to the entire history — without regenerating anything. This is the single property that lets the system evolve for a decade without accumulating irreversible decisions.

**Corollaries**

- **2.1 Raw is permanent; derived is regenerable.** Raw data is kept forever. Derived data is *disposable only in the sense that it can always be rebuilt* from raw data plus the versioned logic that produced it. "Disposable" never means "deletable in place" — existing derived records are versioned and retained (see Law 1), not overwritten.
- **2.2 A snapshot is raw.** A snapshot records only what the market *actually was* at an instant: price, OHLC, volume, delta, DOM, tape, bid/ask, time, instrument, and session — and nothing computed. Levels, features, events, research outputs, and AI signals are all computed *from* snapshots and are never written *into* them.
- **2.3 Capture is deterministic, not intelligent.** Snapshots are taken at points that can be decided without computing anything derived:
  - every completed bar, for each enabled timeframe;
  - every execution (entry, partial fill, scale, exit, reversal);
  - optional user-defined intervals, if enabled.

  A snapshot is **never** triggered by a derived condition — not by BarConv ≥ N, absorption, a volume spike, an AVWAP touch, a delta divergence, or any other computed feature. *Why this is a law and not a preference:* a moment that was never captured is lost forever and cannot be recomputed, so the capture rule must be deterministic and must not depend on the computed layer. Allowing a feature to decide when raw data is recorded would make the raw layer depend on the feature layer and reintroduce the exact circular dependency this architecture exists to prevent.
- **2.4 Everything derived is versioned.** Every feature, event, calculation, schema, and research artifact carries the version of the logic that produced it. Reproducibility is meaningless without knowing *which* version of a calculation generated a value.

---

## Law 3 — Each responsibility has a single owner

**Every calculation, feature, event, and dataset has exactly one authoritative implementation.**

*Why:* Duplicate implementations drift. The moment the same quantity is computed in two places, they diverge, and it becomes impossible to say which value is correct. One owner means one answer.

**Corollaries**

- **3.1 One authoritative implementation per quantity.** A given feature, event type, or dataset is produced by exactly one component. Everything else references that component's output; nothing re-implements it.
- **3.2 Registries are the authoritative catalog.** The list of what features, snapshots, and events exist lives in a single registry. Any module may *read* the registry to discover what is available; only the owner defines an entry. (This read access is the explicit exception in Law 4.)
- **3.3 No shadow calculations.** If a value is needed somewhere, it is obtained from its owner, not recomputed locally "for convenience."

---

## Law 4 — Dependencies flow forward

**Data lineage is one-way; downstream systems may read upstream state but never mutate historical records.**

*Why:* A one-directional graph with no cycles is deterministic and reproducible. As long as nothing downstream writes back into anything upstream, the entire system can be reconstructed by replaying the forward chain.

The canonical forward chain:

```
Market
  -> Recorder
    -> Raw Snapshot
      -> Order Flow Engine
        -> Level Engine
          -> Feature Engine
            -> Event Engine
              -> Trade Recorder
                -> Database
                  -> Research
                    -> AI
                      -> Validated Strategy (optional)
```

**Corollaries**

- **4.1 Lineage is one-way.** Each stage consumes the output of the stage(s) before it and produces new records. No stage ever modifies the records of an earlier stage.
- **4.2 Configuration and reads are the only things that may point "upstream."** Modules are configured from outside, and any module may *read* upstream state or the registries. These are reads and configuration — never mutations of recorded facts. The invariant that never bends: **nothing downstream mutates upstream historical data.**
- **4.3 Events are interpretations, not market data.** An event is computed *above* the raw layer (snapshot to features to events). A snapshot never contains an event. A trade references events by ID; it does not embed them.
- **4.4 Trades reference context; they never duplicate it.** A trade stores stable references — snapshot ID(s), feature version, event IDs, timestamp — and reconstructs its surrounding context by query. This is why snapshots, features, and events must have stable, versioned identity: downstream references depend on it.
- **4.5 The graph is acyclic by construction.** Because snapshots are raw (2.2) and captured deterministically (2.3), no upstream stage ever needs an output from a downstream stage. There are no loops.

---

## Law 5 — Research never changes execution

**Research discovers hypotheses, validation tests them, and only validated edges may influence trading logic.**

*Why:* The platform's purpose is high-quality observation and disciplined research. If research could quietly alter what the system trades, the boundary between "what we observed" and "what we decided" would collapse — and with it, the integrity of every conclusion.

**Corollaries**

- **5.1 Recorder, Research, and Strategy are separate.** The recorder records facts. The research engine analyzes facts. The strategy executes validated rules. These responsibilities never mix.
- **5.2 Validation is the only gate to live influence.** No hypothesis, feature, or signal influences live trading until it has satisfied the project's validation framework. An idea is a hypothesis until proven otherwise; assume nothing provides an edge.
- **5.3 Research never mutates trading behavior, and never mutates recorded data.** Research and AI read from the data model; they never write back into it in any way that would violate Law 1 or Law 4.
- **5.4 Observations are separated from conclusions.** When new evidence overturns a prior conclusion, the prior work is preserved, the conclusion is updated, and the reason for the change is documented. Evidence is never forced to fit a hypothesis.

---

## What this document deliberately excludes

To stay stable, this document contains none of the following. They live downstream and must conform to the laws above:

- The list of modules and what each one is — `00_System_Overview.md`.
- How modules communicate and how data flows in detail — `01_System_Architecture.md` and `11_Data_Flow.md`.
- Engine designs — the Stage 3 engine documents.
- Database tables and relationships — `02_Database_Schema.md`.
- Feature and event definitions — the Stage 6 feature documents.

If any of those documents cannot be written without violating a law here, that is a signal to stop and resolve the contradiction in this document first — not to bend a law quietly.

---

## Section 8 — Architecture Freeze

Version 1.0 of the architecture is **frozen**. Future improvements should be additive whenever possible.

An architectural redesign — any change to the five laws or to the dependency graph they imply — requires **all** of the following before implementation:

- A documented contradiction or limitation, discovered during implementation.
- At least two viable alternatives.
- An analysis of their trade-offs.
- Explicit approval.

**Refactoring for preference is prohibited. Refactoring for correctness is encouraged.** If a change makes the system more correct, more reproducible, or more faithful to the five laws, it is welcome. If it merely reflects a different taste, it does not touch v1.0.

Permitted at any time, *without* reopening this document: fixing bugs, clarifying documentation, adding new features that conform to the laws, and improving performance.

## Section 9 — Amendment and versioning

When the bar in Section 8 is met and an amendment is approved:

- Amendments are **additive and versioned.** In keeping with Law 1, superseded text is recorded in version history rather than silently rewritten.
- An amendment must state *what* changed, *why*, and *which downstream documents are affected*.
- Every accepted architectural decision is recorded in `00a_Architecture_Decision_Log.md`.

Routine architecture work does **not** reopen this document. The next progress comes from writing the remaining design documents *against* these laws — not from re-deriving the laws.

---

*End of Architecture Principles v1.0.*
