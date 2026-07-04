# Kenetic Trading Intelligence Platform — System Architecture

**Document:** `docs/01_Architecture/01_System_Architecture.md`
**Version:** 1.0
**Status:** Frozen Architecture
**Last updated:** 2026-06-25
**Governed by:** `00_Architecture_Principles.md` — **Maps:** `00_System_Overview.md`

> The Overview says *what* the modules are. This document specifies *how they communicate*: where each module runs, how records are identified, what the registries hold, the shape of the data crossing each seam, and the threading rules that keep recording from ever interfering with trading. It adds no new laws — every rule here is a consequence of the five in the Principles.

---

## Decisions made in this draft

Listed here for review as a set. Each is a design choice *under* the frozen laws, not a change *to* them. Flag any you want reconsidered.

- **D-1 Hosting.** The recording pipeline (Recorder to Snapshot to Order Flow to Level to Feature to Event) runs inside one NinjaScript **Indicator**; the Trade Recorder runs in a separate NinjaScript **AddOn**. (Matches the Phase 2 / Phase 3 split in the build plan.)
- **D-2 Single writer.** All persistence goes through one dedicated **writer thread** draining a bounded queue. Producers never write to the database directly.
- **D-3 Identity.** Every record carries a stable, monotonic surrogate ID plus a natural key; derived records also carry the `DefinitionId` and `Version` that produced them.
- **D-4 Registries are startup-populated, read-only thereafter.** Owners register their definitions at initialization; consumers only read.
- **D-5 Backpressure policy.** The trading threads are never blocked. If the persistence queue ever saturates, a **gap marker** is appended and logged rather than blocking the feed or silently dropping data.

---

## 1. Scope

This document covers the cross-cutting framework shared by every module: hosting, identity, registries, data contracts, and threading. It stops at the boundary of each engine — the internal design and exact outputs of each engine belong to the Stage 3 engine documents, and the concrete table definitions belong to `02_Database_Schema.md`.

---

## 2. Hosting model (NinjaTrader 8)

The platform runs inside NinjaTrader 8 as two NinjaScript components, plus out-of-process research tooling.

**Recording Indicator** — hosts the forward recording pipeline: Market Recorder, Raw Snapshot Engine, Order Flow Engine, Level Engine, Feature Engine, Event Engine. It is driven by market-data callbacks (`OnBarUpdate`, `OnMarketData`, `OnMarketDepth`) on NinjaTrader's data-series threads. Its only job is to produce immutable records and hand them to persistence; it makes no trading decisions.

**Trade Monitor AddOn** — hosts the Trade Recorder. It subscribes to account and execution events (`OnExecutionUpdate`, `OnOrderUpdate`, `OnPositionUpdate`), which fire on account-driven threads independent of the data threads. It records executions and links them to context by reference; it never alters order handling.

**Research tooling** — Python / R / DuckDB processes that read the persisted data out-of-process. They are pure consumers (Law 4, Law 5) and are never hosted inside NinjaTrader.

This split exists because market recording and trade recording are driven by different event sources on different threads, and because the Trade Recorder must observe live trading without being coupled to it.

---

## 3. Communication model

Modules do not call each other to exchange mutable state. Each module **produces immutable records** that downstream modules consume. Communication is therefore the flow of versioned, read-only records along the one-way chain from the Principles:

```
Market -> Recorder -> Raw Snapshot -> Order Flow -> Level -> Feature -> Event -> Trade Recorder -> Database -> Research -> AI -> Strategy (optional)
```

Two consequences make this safe and simple:

- Because records are immutable, a consumer can read an upstream record with no locking and no risk of it changing underneath (this is what makes the threading model in Section 7 work).
- Because nothing flows backward, the entire derived state is a pure function of the raw snapshots plus versioned logic — which is exactly what Law 2 requires for reproducibility.

In-process, records move from the producing stage to the next stage directly, as a hand-off of an immutable value. Out of process, records move only by being read from the Database. No module reaches "across" the chain to mutate another's data; the only upstream access permitted is *reading* upstream records and the registries (Law 4.2).

---

## 4. Identity and IDs

Stable identity is what lets downstream records *reference* context instead of copying it (Law 4.4). Every record has identity on two axes:

- **Surrogate ID** — a globally unique, monotonically increasing 64-bit value assigned at creation, used for all downstream references. Monotonic so that ordering is intrinsic.
- **Natural key** — the meaningful coordinates of the record, used for reproducibility and lookup.

| Record | Surrogate | Natural key | Also carries |
|---|---|---|---|
| Raw Snapshot | `SnapshotId` | instrument + timeframe + close-timestamp | — |
| Order-flow / Level / Feature value | own `Id` | `SnapshotId` + `DefinitionId` | `DefinitionId`, `Version` |
| Event | `EventId` | `SnapshotId` + `DefinitionId` | `DefinitionId`, `Version`, source feature IDs |
| Trade | `TradeId` | account + execution key | `SnapshotId`(s), `EventId`(s), feature `Version`(s) |

Rules:

- A derived record **always** carries the `DefinitionId` and `Version` of the logic that produced it (Law 2.4). The same input snapshot under two feature versions yields two records, never an overwrite (Law 1).
- IDs are never reused. A superseded or corrected record is a *new* record that references the one it supersedes (Law 1.4).
- Downstream records reference upstream records by surrogate ID only. They do not embed upstream values (Law 4.3, 4.4).

---

## 5. Registries

A registry is the single authoritative catalog of *what exists* in a domain. There is one per derived domain: Snapshot, Order Flow, Level, Feature, and Event.

Each registry entry holds: `DefinitionId`, name, owning module, current `Version`, version history, and a reference to the output contract the definition produces.

- **Written only by the owner** (Law 3). A module registers its definitions when it initializes; nothing else may add or change an entry.
- **Read by anyone** (Law 4.2). Any module — and research tooling — may read a registry to discover what is available and which version is current. This read access is the explicit, permitted exception to one-way flow.
- **Versioning lives here.** Bumping a calculation's version is a registry operation; old versions remain catalogued so historical records stay interpretable.

The registries are what make "one owner, one definition, one version" enforceable rather than aspirational.

---

## 6. Data contracts

A **contract** is the shape of a record crossing a seam. Every contract is an immutable, versioned value type and obeys the same structural rules:

- it has its own surrogate ID;
- it references the upstream IDs it derives from;
- if derived, it carries the producing `DefinitionId` and `Version`;
- it carries the capture or computation timestamp;
- once constructed, it is never mutated.

The seams and their contracts:

| Producer | Contract | Consumers |
|---|---|---|
| Market Recorder | Raw market observation | Raw Snapshot Engine |
| Raw Snapshot Engine | `RawSnapshot` | Order Flow, Level, Feature engines; Trade Recorder |
| Order Flow Engine | Order-flow measurement | Level, Feature, Event engines |
| Level Engine | Level / structure record | Feature, Event engines |
| Feature Engine | Feature value | Event Engine; Research |
| Event Engine | Event record | Trade Recorder; Research |
| Trade Recorder | Trade record | Database; Research |
| Database | Persisted, queryable records | Research, AI |

The Stage 3 engine documents define the *fields* of each contract; this document fixes only their common structure and who may consume them. The field-level schema is owned by `02_Database_Schema.md`.

---

## 7. Threading model

This is the section that protects trading. The governing rule, from the platform's thread-safety stance: **a market-data or execution callback is never blocked, and data integrity is never traded for performance.**

Threads in play:

- **Data-series threads** — deliver `OnBarUpdate` / `OnMarketData` / `OnMarketDepth` and drive the Recording Indicator's pipeline.
- **Account / execution threads** — deliver execution and order events and drive the Trade Monitor AddOn.
- **Persistence writer thread** — a single dedicated thread that drains the write queue and appends to the Database (D-2).
- **UI thread** — renders only; it reads, never writes.

Rules:

1. **Produce, then hand off.** On a trading thread, a module does only the work needed to construct the immutable record for that event, then enqueues it. No database I/O and no unbounded computation happen on the trading thread.
2. **Persistence is asynchronous and single-writer.** All appends happen on the writer thread. A single writer gives total ordering of writes for free and removes write contention — and because writes are append-only (Law 1), there is never a read-modify-write to coordinate.
3. **Immutable records remove locks from the read path.** Because records never change after construction, any thread can read any record with no lock. Shared mutable state is avoided; where a module must keep rolling state (for example, cumulative delta), that state is confined to its owning module and is never read from another thread.
4. **The UI never participates in recording.** Rendering reads immutable records or persisted data; it cannot delay or alter the pipeline.

The hand-off mechanism between producers and the writer is a bounded concurrent queue of immutable records — producers enqueue without blocking, the writer dequeues and appends.

---

## 8. Ordering, failure, and backpressure

- **Ordering.** The single writer appends in the order it dequeues; the monotonic surrogate IDs (Section 4) preserve intrinsic ordering even across threads.
- **Failure.** Because the database is append-only, a failed append leaves no partial state to roll back. The policy is retry; a persistent failure is itself recorded as a logged fault, never silently swallowed.
- **Backpressure (D-5).** The write queue is bounded and sized generously for the expected event rate. The trading threads must never block on it. In the rare event the queue saturates, the system appends a **gap marker** — an explicit, queryable record that data was missed in a known interval — and logs it. This follows Law 1.4: a gap is *recorded*, never hidden, and never papered over by blocking the feed or dropping data silently.

A gap marker is a deliberate, visible scar in the record rather than an invisible hole, which keeps the dataset honest about its own completeness.

---

## 9. Conformance to the Architecture Principles

- **Law 1 (immutable history).** All records are immutable and append-only; corrections, supersessions, and gaps are new records (Sections 4, 8).
- **Law 2 (raw is truth).** Derived records carry `DefinitionId` + `Version`; all derived state is a pure function of raw snapshots plus versioned logic (Sections 3, 4).
- **Law 3 (single owner).** Registries are writable only by the owning module; no quantity has two producers (Section 5).
- **Law 4 (forward flow).** Records move one way; the only upstream access is reading records and registries; references replace copies (Sections 3, 4, 6).
- **Law 5 (research is not execution).** Research and AI are out-of-process pure consumers; the Trade Monitor observes but never alters trading (Section 2).

If any engine document cannot be written within this framework, that is a contradiction to surface against this document or the Principles — not a rule to bend quietly.

---

*End of System Architecture v1.0.*
