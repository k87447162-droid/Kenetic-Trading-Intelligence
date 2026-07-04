# Kenetic Trading Intelligence Platform — System Overview

**Document:** `docs/01_Architecture/00_System_Overview.md`
**Version:** 1.0
**Status:** Frozen Architecture
**Last updated:** 2026-06-25
**Companion to:** `00_Architecture_Principles.md` (which governs; this document organizes)

> This is the map of the system: *what exists* and *what each part owns*. It introduces no new architecture — it translates the frozen principles into a readable overview. `00_Architecture_Principles.md` explains *why*; this document explains *what*. Where the two conflict, the Principles always win.

---

## Purpose

The Kenetic Trading Intelligence Platform is an institutional-grade market observation and research platform.

Its purpose is **not** to optimize a trading strategy.

Its purpose is to create a permanent, reproducible record of market behavior, trader decisions, and trade outcomes so that statistically valid edges can be discovered through objective research.

The platform is built around one central principle:

> **Observe first. Research second. Automate last.**

---

## Architectural Philosophy

The platform is composed of independent layers.

Each layer has one responsibility. Each layer owns its own data. No layer modifies historical records created by another layer. Information always moves forward through the system. Historical data is append-only and immutable.

---

## High-Level System Flow

```
Market Data
        │
        ▼
Market Recorder
        │
        ▼
Raw Snapshot Engine
        │
        ▼
Order Flow Engine
        │
        ▼
Level Engine
        │
        ▼
Feature Engine
        │
        ▼
Event Engine
        │
        ▼
Trade Recorder
        │
        ▼
Database
        │
        ▼
Research Engine
        │
        ▼
AI / Analytics
        │
        ▼
Validated Strategy Improvements
```

No downstream component ever modifies upstream data.

---

## Core System Modules

### 1. Market Recorder

**Purpose** — Capture the raw market feed exactly as it exists.

**Responsibilities**

- Receive market data from NinjaTrader.
- Normalize timestamps.
- Capture market activity without interpretation.
- Never perform research.
- Never make trading decisions.

**Outputs** — Raw market observations.

---

### 2. Raw Snapshot Engine

**Purpose** — Capture immutable snapshots of the market at deterministic points in time.

**Snapshot Triggers** — Snapshots are created only at deterministic events such as:

- Completed bar closes.
- Trade executions.
- Other predefined recording intervals.

Snapshots are **never** triggered by calculated features or research conclusions.

**Snapshot Contents** — Snapshots contain only raw market information. Examples include:

- OHLC
- Volume
- Bid/Ask
- DOM
- Time
- Instrument
- Session
- Raw order flow

Snapshots contain no calculated features.

---

### 3. Order Flow Engine

**Purpose** — Transform raw order flow into reusable foundational measurements. Examples include:

- Cumulative Delta
- Volume Imbalance
- Absorption
- Stacked Imbalances
- Tape Statistics

These measurements become inputs for higher-level analysis.

---

### 4. Level Engine

**Purpose** — Maintain institutional market structure. Examples include:

- Daily Levels
- Weekly Levels
- Monthly Levels
- VWAP
- AVWAP
- POC
- Value Area
- High/Low Volume Nodes
- Gaps
- Opening Range

The Level Engine owns all structural calculations.

---

### 5. Feature Engine

**Purpose** — Calculate reusable research variables. Examples include:

- Bar Conviction
- Volume Ratio
- Delta Alignment
- Reaction Quality
- Trend Strength
- Volatility Metrics

Every feature has one owner, one definition, and one version.

---

### 6. Event Engine

**Purpose** — Convert combinations of features into meaningful market events. Examples include:

- Liquidity Sweep
- Failed Auction
- Volume Spike
- AVWAP Interaction
- Trend Continuation
- Reversal
- Gap Fill

Events reference snapshots and features rather than duplicating data.

---

### 7. Trade Recorder

**Purpose** — Capture every manual and automated trade without altering trading behavior.

**Responsibilities**

- Recording executions.
- Linking trades to snapshots.
- Linking trades to events.
- Recording account information.
- Recording execution source.
- Recording outcomes.

Trades store references rather than duplicated market context.

---

### 8. Database Layer

**Purpose** — Provide permanent storage for all recorded information.

The database is append-only. Historical records are never modified.

**Primary responsibilities**

- Persistence
- Versioning
- Relationships
- Data integrity

---

### 9. Research Engine

**Purpose** — Analyze historical data.

**Responsibilities**

- Hypothesis generation
- Validation
- Walk-forward testing
- Out-of-sample testing
- Statistical analysis

The Research Engine never changes live trading behavior.

---

### 10. AI & Analytics Layer

**Purpose** — Enable advanced analysis using AI and statistical tools.

**Supported technologies**

- Python
- DuckDB
- SQLite
- Parquet
- R
- Machine Learning
- Large Language Models

This layer consumes recorded data but never modifies it.

---

### 11. Trading Strategy

**Purpose** — Execute only validated trading logic.

Research findings influence this layer only after successfully completing the platform's validation framework. The strategy remains isolated from research until evidence supports promotion.

---

## Data Ownership

Each module owns a single responsibility.

| Module | Owns |
|---------------------|-------------------------|
| Market Recorder | Raw market feed |
| Raw Snapshot Engine | Immutable snapshots |
| Order Flow Engine | Order flow measurements |
| Level Engine | Market structure |
| Feature Engine | Calculated features |
| Event Engine | Market events |
| Trade Recorder | Trade records |
| Database | Persistent storage |
| Research Engine | Analysis |
| Strategy | Trade execution |

No ownership overlaps.

---

## System Boundaries

The platform is divided into three independent domains.

**Observation** — Responsible for recording facts.

**Research** — Responsible for discovering and validating hypotheses.

**Execution** — Responsible only for trading validated rules.

No domain may assume the responsibilities of another.

---

## Relationship to the Architecture Principles

This document describes the organization of the platform. It does not define architectural law.

All implementation decisions remain governed by `00_Architecture_Principles.md`.

If a conflict exists between this document and the Architecture Principles, the Principles always take precedence.

---

*End of System Overview v1.0.*
