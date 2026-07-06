# PROJECT RULES

The operating rules and engineering philosophy for the KENETIC Trading
Intelligence Platform. These rules are written to hold for the life of the
project. When a decision is uncertain, prioritise improving the quality of
observation over adding another trading feature.

---

## Architecture Principles

- Build systems before features. Features plug into existing systems; a feature
  must never define architecture.
- Single responsibility per module. One authoritative implementation per
  calculation. One owner per piece of data. One module per feature.
- Review before changing: architecture, dependencies, thread safety, data
  integrity, scalability. Identify weaknesses, propose improvements, and wait for
  approval before major changes.
- Never silently redesign existing systems.

## Coding Standards

- Prefer small classes, single responsibility, immutable objects, strong typing,
  enums over strings, and interfaces and dependency injection where appropriate.
- Favour readability over cleverness.
- Avoid duplicate calculations, hidden state, and unnecessary allocations.
- Every major class documents its purpose, responsibilities, inputs, outputs,
  dependencies, thread safety, and performance considerations.

## Research Principles

- Treat every idea as a hypothesis; assume no variable provides an edge.
- Always separate observations from conclusions.
- Never force evidence to fit a hypothesis. If evidence contradicts a prior
  conclusion, preserve the prior work, update the conclusion, and document the
  reason.

## Validation Requirements

- No hypothesis may influence live trading until it satisfies the project's
  validation framework.
- Validation includes graded evidence levels and out-of-sample and walk-forward
  testing before promotion.
- Research systems must never alter live trading behaviour.

## Statistical Discipline

- Distinguish facts, assumptions, opinions, and recommendations in every
  analysis.
- State confidence, limitations, and trade-offs. Do not guess.
- Guard against overfitting; prefer evidence that survives out-of-sample and
  walk-forward scrutiny.

## Feature Lifecycle

A feature progresses: Hypothesis -> Documented -> Implemented (versioned) ->
Validated -> Promoted, with preserved reproducibility at every stage. A feature
only gains live influence after validation. See
`docs/03_Research/04_Feature_Lifecycle.md`.

## Digital Twin Philosophy

The platform must be able to faithfully reconstruct historical market conditions
from stored raw data. Preserve raw observations whenever they cannot be
deterministically recreated later, so the "twin" reproduces what was actually
observed.

## AI-First Design

Data is structured for downstream machine learning and AI from the start. AI
systems consume validated research data; they do not drive live execution
directly and do not bypass the validation framework.

## Knowledge Graph Vision

Observations, events, features, levels, and outcomes are designed to interconnect
as a queryable knowledge graph, enabling relationships across time and context to
be analysed without re-deriving raw facts.

## Versioning Rules

- Version feature calculations; never overwrite historical information.
- Changing a calculation creates a new version; prior versions and their outputs
  remain reproducible.
- See `docs/07_Development/Development_Standards/05_Versioning.md`.

## Event Philosophy

Events are discrete, derived facts computed from raw observations. They are
reproducible from stored raw data and carry the version of the logic that
produced them. Events describe what happened, not what to do about it.

## Logging Philosophy

Logs are diagnostic and operational, separate from the research record. Logging
must never block trading threads and must not be relied upon as the system of
record for research data.

## Raw Data Principles

- The recorder records facts; preserve raw observations that cannot be
  deterministically recreated.
- Do not duplicate data that can be reproduced from stored raw data.
- Never overwrite historical information.

## Thread Safety

Assume market data, order executions, UI, and database writes may occur on
different threads. Protect shared state, prefer immutable snapshots, avoid
blocking trading threads, and never compromise data integrity for performance.

## Development Workflow

- Always work in phases. Each phase compiles, is testable, preserves existing
  functionality, and is reviewable before continuing.
- Never skip foundational work.
- Optimise every decision in priority order: Correctness -> Reproducibility ->
  Data Integrity -> Scalability -> Maintainability -> Simplicity -> Performance ->
  Development Speed.
