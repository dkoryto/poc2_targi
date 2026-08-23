# ADR 0006 — Passport generation, versioning and invalidation

**Status:** accepted (wave 2) · **Context:** `apps/business-api` — Quality, Traceability, Passports

## Context

The demonstrator must produce a credible acceptance document ("cyfrowy paszport jakościowy") in one click, and must show
the opposite case just as clearly: a passport that cannot be issued, with the concrete reasons, and a passport that
loses validity when its material is blocked. The document has to render offline on a trade-show laptop.

## Decisions

1. **Completeness is a pure domain rule.** `Dspc.Domain/Quality/PassportCompletenessEvaluator` takes a `PassportFacts`
   snapshot (product, order, BOM version, consumed lots with certificates and QC status, inspections, deviations,
   approval) and returns per-requirement results plus a `missing[]` of `{code, labelKey, params}`. It has no EF, no
   DTOs and no clock, so it is unit-testable and deterministic. The UI localises `labelKey`; the API never ships prose.
2. **Template `DQP-01` is a configurable register, not a compliance claim.** Requirement rows live in
   `packages/demo-data/quality.json` and are seeded; the evaluator carries the same list as its default. Mapping these
   rows onto a specific contract, AQAP or STANAG requirement needs a specialist's analysis — the document, the API and
   the UI all say so.
3. **Generation is gated twice:** every mandatory requirement satisfied **and** status `Approved`/`Generated`.
   Otherwise `422` with the missing list, which is exactly what the UI renders. Approval itself is refused while
   anything else is missing, so "approve then discover the gap" cannot happen.
4. **Versions are append-only.** Each generation renders a new `PassportVersion` (PDF in object storage, SHA-256 of the
   rendered bytes, size, author, snapshot JSON of the render model). The previous version becomes `Superseded`; nothing
   is deleted or overwritten. The QR code points at the local record `/passports/{serial}`, never at an external
   service.
5. **Rendering is QuestPDF (Community licence) + QRCoder**, both fully offline; the bundled Lato family covers Polish
   diacritics, so no font is fetched at runtime. See `docs/licenses.md` — QuestPDF's Community licence has a revenue
   threshold that must be checked before any commercial deployment.
6. **Invalidation runs inside the blocking transaction.** `LotService.BlockAsync` calls
   `PassportInvalidationService.InvalidateForSerialsAsync` before `SaveChanges`, so there is no window in which the UI
   shows a `Generated` passport for blocked material. The passport becomes `Invalidated` with the reason recorded and
   its current version marked `Invalidated` (the file is retained for the audit trail). The
   `MaterialLotBlocked` outbox handler then adds the slower consequences — notifications and inbound risk re-scoring —
   and re-runs the invalidation idempotently as a safety net for any other block path.
7. **Both trace directions read the same rows.** Trace-back (serial → lots → purchase order → supplier) and
   trace-forward (lot → orders → serials → passports) are both derived from `MaterialConsumption` and `Reservation`, so
   they cannot disagree. `TraceabilityLink` is a denormalised index rebuilt after seeding, used for search and export,
   never as the source of truth.
8. **Seeded passports are rendered by the same pipeline.** A `ISeedPostProcessor` generates the PDFs for the two
   "already issued" passports after each seed/reset instead of shipping binary fixtures — the demo therefore always
   shows a document produced by the code being demonstrated. Failure degrades those passports to `Approved` rather than
   breaking the reset, and the whole reset stays inside its 10 s budget.

## Consequences

- Adding a requirement means one row in the template plus one `case` in the evaluator; the UI needs only a translation.
- Because completeness is recomputed on every read, a passport's `complete` flag always reflects current lot status —
  blocking material immediately shows up as a missing `QC_STATUS` even before invalidation is processed.
- PDF rendering costs ~100–200 ms per document; generating many at once (e.g. a large seed) would need batching.
