# KENETIC Trading Intelligence Platform

An institutional-grade market observation and research platform. KENETIC records
market facts, reconstructs them faithfully, and turns them into validated,
reproducible research â€” keeping recording, research, analytics, AI, and
execution as separate, well-bounded systems.

> **Status:** Foundation / scaffolding. This repository currently contains the
> project skeleton and engineering documentation.

---

## Project Vision

Build the highest-quality market observation system possible. The goal is not to
optimise a single automated strategy, but to create a durable platform that can
record, reconstruct, analyse, validate, and continuously improve discretionary
trading decisions through objective evidence.

The recorder records facts. The research engine analyses facts. The strategy
executes validated rules. These responsibilities never mix.

## Long-Term Goals

- Preserve raw, reproducible market observations that cannot be deterministically
  recreated later.
- Maintain one authoritative implementation for every calculation and one owner
  for every piece of data.
- Treat the research database as a primary product, designed for SQLite, DuckDB,
  Parquet, Python, R, and downstream ML/AI.
- Subject every hypothesis to a formal validation framework before it can
  influence live trading.
- Keep live trading behaviour isolated from, and unaffected by, research systems.

## Folder Overview

| Path | Purpose |
|------|---------|
| `docs/00_Foundation/` | Constitution, vision, philosophy, glossary, roadmap. |
| `docs/01_Architecture/` | System and engine-level architecture specifications. |
| `docs/02_Research/` | Research, validation, evidence, and lifecycle frameworks. |
| `docs/03_Features/` | Per-feature design and definition documents. |
| `docs/04_Development/` | Coding standards, workflow, testing, performance, versioning. |
| `docs/05_Research_Findings/` | Dated research results and validation logs. |
| `src/` | Source code (NinjaTrader 8 / C# and supporting tooling). |
| `database/` | Local research databases and schema artifacts. |
| `exports/` | Generated exports (Parquet/CSV) for downstream analysis. |
| `research/` | Notebooks and analysis scripts (Python/R). |
| `logs/` | Runtime and diagnostic logs. |
| `screenshots/` | Visual references and annotated captures. |
| `scripts/` | Project tooling and setup/update scripts. |
| `tests/` | Automated tests. |
| `tools/` | Standalone utilities. |
| `assets/` | Static assets. |

## Architecture Overview

KENETIC is a system-first platform. Independent engines each own a single
responsibility and communicate through well-defined data, not shared mutable
state:

- **Market Recorder** â€” captures raw market facts.
- **Event Engine** â€” derives discrete events from observations.
- **Feature Engine** â€” computes versioned features from raw data.
- **Level / Order Flow / Snapshot Engines** â€” specialised observation and
  reconstruction subsystems.
- **Trade Recorder** â€” records execution facts, separate from strategy logic.
- **AI Architecture** â€” consumes validated research data; never drives live
  execution directly.

See `docs/01_Architecture/` for the authoritative specifications.

## Development Workflow

1. Read the relevant documentation before writing any code.
2. Review existing architecture, dependencies, thread safety, data integrity, and
   scalability.
3. Propose architectural improvements and wait for approval before major changes.
4. Work in small, reviewable phases that compile, are testable, and preserve
   existing functionality.
5. Never silently redesign existing systems.

## How New Features Are Added

1. Document the feature as a hypothesis in `docs/03_Features/`.
2. Define its single authoritative calculation and its data owner.
3. Implement behind the appropriate engine â€” features plug into systems, never
   the reverse.
4. Version the calculation and preserve reproducibility.
5. Promote to live influence only after the validation framework is satisfied.

## Research Workflow

- Treat every idea as a hypothesis; assume no variable provides an edge.
- Separate observations from conclusions at all times.
- When evidence contradicts a prior conclusion, preserve the prior work, update
  the conclusion, and document why it changed.
- Record findings in `docs/05_Research_Findings/`.

## Validation Workflow

- Evidence is graded by level (see `docs/02_Research/03_Evidence_Levels.md`).
- Features progress through a defined lifecycle
  (`docs/02_Research/04_Feature_Lifecycle.md`).
- No hypothesis influences live trading until it meets the validation bar,
  including out-of-sample and walk-forward checks.

## Technology Stack

- **Capture / Execution:** NinjaTrader 8, C# (.NET)
- **Storage:** SQLite, DuckDB, Parquet
- **Analysis:** Python, R
- **ML / AI:** downstream consumers of validated research data
- **Tooling:** PowerShell 7 setup/update scripts

## Future Roadmap

Roadmap detail is maintained in `docs/00_Foundation/04_Project_Roadmap.md`. At a
high level: harden the recorder, formalise the schema, build the
research/validation pipeline, then layer analytics and AI on top of a trustworthy
data foundation.
