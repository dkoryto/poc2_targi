# ADR 0008 — One definition of "moved operations" on the What-If screen

## Status
Accepted (2026-08-23).

## Context

A scenario produces three plans: the **approved baseline**, **before** (baseline + the scenario's change, with no
re-sequencing) and **after** (the engine's proposal). "How many operations moved?" has a different answer against
each reference, and the result screen was answering it two ways at once:

- the KPI tile and the engine's `changed`/`shiftDays` markers counted **after vs baseline**,
- the moved-operations table (`/compare`) counted **after vs before**.

Both were internally correct — an independent recomputation matched each exactly — but they shipped under the same
label on the same screen. On Kielce the tile read 8 next to a table headed "(3)"; on Piła the tile read 6 next to an
**empty** table, which reads as a broken screen. A presenter reading the screen aloud would contradict themselves.

## Decision

The headline is **after vs before**: *what did re-planning change relative to what would otherwise happen*. That is
the question the demo actually answers, and the Przed/Po Gantt already draws exactly this comparison (ghost bars are
`before`). Concretely:

- `ScenarioCalculations.ReanchorChanges` re-stamps the `after` plan's `changed`/`shiftDays` against `before`, so the
  Gantt highlights, `compare.movedOperations` and the KPI tile are the same set of operations by construction.
- `kpiAfter.movedOperations` = that count; `kpiBefore.movedOperations` = 0 ("before" is the reference).
- The engine's vs-baseline count survives as `changesVsBaseline` on the scenario DTO, labelled
  "Zmiany wobec zatwierdzonego planu bazowego" / "Changes vs the approved baseline". It is still the honest measure
  of total disruption against the plan the shop floor agreed to, so it is worth showing — just not under the same name.
- When re-planning moves nothing, the table renders an explicit empty state ("plan po zmianie jest już najlepszy
  z możliwych") instead of an empty grid beside a non-zero tile.

The **planning engine's contract is untouched**: `packages/contracts/planning-engine.yaml` still defines
`changed`/`shiftDays` relative to the baseline supplied in the request, and the Java fixtures still assert that.
Re-anchoring happens in the business API's presentation layer, and the raw engine response stays in `ResponseJson`
for audit.

## Consequences

- Kielce's demo numbers are now stated as "8 operacji różni się od planu bazowego, z czego 3 przesunęło
  przeplanowanie" rather than a bare "8". Docs quoting the old figure were corrected.
- Regression tests enforce the agreement at both levels: a .NET API test asserts tile == Gantt == table and that the
  shifts match row-by-row, and a web test asserts tile == table == ghost-bar count (it fails with
  "expected 7 to be 3" if the old behaviour is reintroduced).
- A related copy fix: the What-If `BLOCK_LOT` change is a **simulation** — it does not alter lot status or invalidate
  passports (only `POST /lots/{lot}/block` does). The preset and change labels now say "Symuluj blokadę partii", and
  the result screen carries a note pointing at the lot page for a real block.
