# ADR-0004 — Seed-relative demo clock and deterministic ids

**Status:** accepted · **Date:** 2026-08-23

## Context
The stand demo must look "live" on any date, reset to an identical state in < 10 s, and the engine tests pin a fixed week (T0 = 2026-09-07).

## Decision
All seed dates are offsets from **T0 = Monday 06:00 (Europe/Warsaw) of the current ISO week** (`IDemoClock.T0Date`, overridable via `Demo__ClockAnchor` for tests/e2e). The engine fixture `packages/contracts/examples/baseline.json` is the single source of baseline operations; the seeder shifts it by `T0 − 2026-09-07` days, preserving weekdays. Every seeded entity id is a SHA-1-derived GUID of its business code (`DemoSeeder.Id(kind, code)`), so resets reproduce identical ids and the web app can deep-link by code.

## Consequences
- KPIs, risk scores and baseline KPIs are computed at seed time from data, never hardcoded.
- A reset is `TRUNCATE … CASCADE` + reseed in one transaction (~2–4 s), followed by post-processors (passport PDFs).
- Timestamps such as `createdAt` differ per reset; business state does not.
