# ADR 0007 — Site scoping across the API

## Status
Accepted.

## Context
The demonstrator grew from one plant to four (`docs/architecture/multi-site.md`), so a stand visitor can be shown
four different stories without resetting. The domain already carried `SiteId` on purchase orders, production
orders, work centres and inventory, but most queries ignored it: KPIs, the map, the heatmap and the planning model
all read across the whole database. With one plant that was invisible; with four it would silently mix plants —
the worst kind of demo bug, because the screen still looks plausible.

## Decision
1. **One resolver, not a parameter everywhere.** `ISiteContext` turns the optional `?siteCode=` into a site id:
   absent → the caller's default plant, unknown → `404`, out of the caller's reach → `403`. Endpoints call it;
   query classes take a plain `Guid siteId` and stay ignorant of HTTP.
2. **Suppliers reach only the plants they deliver to**, derived from their purchase orders rather than stored, so
   the seed cannot drift out of sync with the rule. This composes with the existing `ISupplierScope`: a supplier is
   restricted *both* by organisation and by plant.
3. **`SiteId` denormalised onto `MaterialLot`, `PlanningBaseline` and `PlanningScenario`.** Lots could be reached
   through their purchase-order line, but lot queries are on the hot path of traceability and the passport rules;
   a join per lot to answer "which plant" is not worth it, and a lot physically sits at one plant anyway.
4. **A scenario belongs to exactly one plant**, derived from what its changes point at. Mixing plants is rejected
   with `400` rather than resolved by picking a winner: a scenario spanning two plants is a modelling error, and
   guessing would produce a plan nobody asked for.
5. **Baselines are versioned per plant** (`(SiteId, Version)` unique), so approving a plan at one plant does not
   renumber another's.

## Consequences
- Every dashboard, planning, inbound, quality and traceability query now filters by plant; `MultiSiteTests`
  asserts there is no leakage, because a leak would be invisible on screen.
- `PlanImpactService` caches per site instead of globally.
- Risk scoring is plant-local: a line competes for stock only with its own plant's demand.
- **Not scoped:** notifications and audit events. Neither entity carries a site — notifications target a role and
  audit rows reference entity codes across all modules. Adding a plant column to both is the obvious follow-up;
  until then those two endpoints accept `?siteCode=` for contract symmetry but return organisation-wide rows.
