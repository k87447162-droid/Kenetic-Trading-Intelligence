# 06 Data

## Purpose

Definitions that make data trustworthy: the data dictionary, metrics, context variables, and feature definitions.

## Contents

Data Dictionary, Metrics, Context Variables, Features/ (per-feature definition documents). The authoritative database schema specification lives with the Architecture documents in 07_Development (it is cross-referenced by the Architecture Decision Log); create a Schemas/ folder here only when the first generated artifact (DDL export, ERD) exists.

## Relationships

Every analysis in 03_Research and every Digital Twin claim must trace to fields defined here. Feature docs follow the Feature Lifecycle in 03_Research.

## How This Folder Should Evolve

Definitions are versioned, never overwritten. A changed calculation is a new version.

---
_Master README created 2026-07-05. Keep this file current whenever the folder's
structure or responsibilities change._