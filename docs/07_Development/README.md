# 07 Development

## Purpose

Engineering home: system architecture specifications and development standards for the recorder, research engine, and AI layer.

## Contents

Architecture/ (system overview, principles, decision log, database schema - the single authority on every subsystem), Development_Standards/ (coding standards, workflow, testing, performance, versioning). Subsystem knowledge lives in the Architecture documents; do not create per-subsystem sibling docs that would split authority.

## Relationships

Implements the platform that 06_Data and 03_Research depend on. Bound by PROJECT_RULES.md and the Architecture Decision Log.

## How This Folder Should Evolve

ADRs are append-only. Architecture changes require a new ADR; accepted entries are never edited in place.

---
_Master README created 2026-07-05. Keep this file current whenever the folder's
structure or responsibilities change._