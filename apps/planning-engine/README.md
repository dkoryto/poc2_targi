# DSPC Planning Engine (`apps/planning-engine`)

Stateless, deterministic MRP / re-scheduling service for the Defense Supply & Production Control demonstrator.
Java 17 · Spring Boot 3.3 · Maven · no external solver library (`dspc-heuristic/1.0`). Port **8081**.

Contract: [`packages/contracts/planning-engine.yaml`](../../packages/contracts/planning-engine.yaml) (bundled copy in
`src/main/resources/static/openapi.yaml`, served at `GET /v3/api-docs`; Swagger UI at `/swagger-ui.html`;
`ContractSyncTest` fails if the two files drift). Example requests: `packages/contracts/examples/*.json`
(regenerate with `node packages/contracts/examples/generate-fixtures.mjs <dir>…`).

## Commands

```bash
./mvnw -q test                                   # all tests
./mvnw -q test -Dtest=Act40DelayScenarioTest     # single test class
./mvnw -q spring-boot:run                        # http://localhost:8081
curl -s localhost:8081/actuator/health
curl -s -X POST localhost:8081/api/v1/plan/solve -H 'content-type: application/json' \
     --data @src/test/resources/scenarios/act40-delay.json | jq '.kpi, .explanations'
docker build -t dspc/planning-engine .           # multi-stage, non-root, HEALTHCHECK on /actuator/health
```

Endpoints: `POST /api/v1/plan/solve`, `GET /actuator/health` (liveness/readiness groups), `GET /v3/api-docs`.
Validation and input inconsistencies return RFC 7807 Problem Details (`400`, `title: Invalid planning request`).

## What the engine does

The business API sends a fully materialised problem (scenario changes already applied to ETAs, lots, priorities,
capacity). The engine returns a schedule, per-order results, KPIs, an objective breakdown and **reason-coded
explanations** (no free text — the UI localises `reasonCode + params`). Same input ⇒ byte-identical output
(except `elapsedMs`).

### Time model

- All date-times are site-local wall clock without zone (`yyyy-MM-dd'T'HH:mm:ss`).
- Each work center has a working window starting 06:00 lasting `hoursPerDay × capacityFactor` (default 16 h)
  on Mon–Fri; `calendar[]` overrides hours per date (0 = closed). One operation at a time per work center.
- Operations consume consecutive *working* time and pause over nights/weekends (a 36 h op starting Friday 06:00 ends
  Tuesday 10:00).
- Material: free stock = `onHand − reserved`; each inbound lot adds its quantity at `eta` 06:00.

### Hard constraints (never violated)

1. No two operations overlap on a work center (capacity = 1 at a time, inside the calendar).
2. Operations of an order run in `sequence` order; an op starts only after its predecessor ends.
3. No op starts before the order's `releaseDate`.
4. No op starts before all its `materialRequirements` are covered (on-hand + arrived inbound); blocked lots are
   simply not part of `onHand`.
5. Frozen operations (`frozen` on the op or on the whole order) keep `baselineStart/End` exactly and pre-occupy
   capacity — even if constraints 3–4 would push them (reported as `ORDER_FROZEN_KEPT`).
6. Frozen ops that overlap each other or break their own sequence make the input inconsistent → `status: INFEASIBLE`.

### Algorithm

1. **Ranking** — frozen orders first, then priority desc, dueDate asc, code asc.
2. **Material allocation** — every requirement gets a cumulative position on its part's supply timeline.
   Allocation order: frozen ops first, then the operation's *desired start* (baseline start, or release date
   when no baseline; the lower bound for orders being pulled forward), then priority desc, due asc, code, sequence,
   part. An op's material date = latest covering date over its requirements. Requirements that cannot be
   covered by on-hand stock are listed in `orders[].shortages` with `availableOn` = covering ETA (or `null` when
   nothing inside the horizon covers them — those count in `kpi.ordersWithShortage` and the `shortage` objective
   term, and the op is parked at `horizonEnd`).
3. **Placement** (list scheduling) — orders in ranking order, ops in sequence: `earliest = max(release,
   predecessor end, material date, baselineStart)` — the baseline start acts as a lower bound so an untouched plan is
   reproduced exactly and change is minimised; place at the first free slot ≥ earliest on the work center.
   `waitingForMaterial = true` when the material date was the binding bound.
4. **Improvement pass (pull-forward)** — `downtime` = idle working hours inside the baseline windows of operations
   that are waiting for material (capacity the committed plan expected to use but cannot). For each non-frozen
   order and each such idle window on one of its work centers, compute a lower bound for its first operation by
   walking back the preceding operations' durations through the calendars (plus the release date as a candidate),
   rebuild the whole schedule with that order "flagged" (baseline ignored, material re-allocated at the new desired
   start) and keep the move with the lowest objective if it is strictly better. Repeat until no improving move or
   `timeLimitMs` elapses (best-so-far is returned, status stays `FEASIBLE`).
5. **Fallback** — if `timeLimitMs` is below the optimiser budget (`dspc.solver.min-optimiser-budget-ms`, 10 ms) or
   already exceeded after placement, the naive placement is returned with `status: FALLBACK` and a
   `FALLBACK_USED` explanation. (The business API has its own "Heuristic fallback" for when the engine is down.)

### Objective (weighted sum, `weights` in the request, defaults in the contract)

| Term | Definition | default weight |
|---|---|---|
| lateness | Σ max(0, plannedEnd.date − dueDate) days × priority | 10 / day / priority |
| shortage | Σ units not coverable inside the horizon | 5 / unit |
| downtime | idle hours in baseline windows of material-waiting ops | 20 / h |
| deliveryBreach | number of late orders | 100 / order |
| change | operations whose start or end differs from baseline | 2 / op |
| changeover | per work center: product switches beyond the baseline count | 8 / switch |

### Explanations (`reasonCode` + `params`, sorted by reason priority then `orderCode`)

`ORDER_DELAYED_MATERIAL_SHORTAGE {orderCode, partCode, missingQty, days, availableOn}` ·
`ORDER_PULLED_FORWARD {orderCode, lineCode, days, materialCompleteness, workCenters[]}` ·
`ORDER_MOVED_LINE` (reserved — v1 never reassigns lines) · `DOWNTIME_REDUCED {fromHours, toHours}` ·
`ORDER_LATE_DUE {orderCode, days}` · `ORDER_FROZEN_KEPT {orderCode}` · `CAPACITY_REDUCED {workCenterCode, factor}` ·
`FALLBACK_USED {reason}`. Explanations without an order carry `orderCode: ""`.

### Demo scenario (pinned by tests, `T0 = 2026-09-07`)

`act40-delay.json` = `baseline.json` with PO-2026-0007/1 (12 × ACT-40) ETA T0+8 → T0+18.
Result: WO-2026-014 integration op waits for ACT-40 (shift 9 d, order late 4 d), its 36 h WC-INT slot would sit idle;
WO-2026-019 (all material on hand) is pulled forward 29 days (ELEC T0+8 06:00, **INT T0+9 14:00 → T0+11 10:00**),
downtime **36 h → 8 h**, 8 operations moved, frozen WO-2026-012 / WO-2026-013-op10 untouched. Solve time ≈ 5 ms.

## Layout

```
src/main/java/com/dspc/planning
├── api/        PlanController, OpenApiController (serves contract), ApiExceptionHandler (Problem Details)
├── config/     JacksonConfig (ISO seconds), SolverProperties
├── model/      request / response records (mirror the OpenAPI contract)
└── solver/     WorkCalendar, Timeline, MaterialLedger, Problem, ScheduleBuilder, ObjectiveCalculator, HeuristicSolver
src/test/java/com/dspc/planning/solver  Baseline/Act40/HardConstraints/NoDoubleBooking/Frozen/Fallback/Determinism/WorkCalendar tests
src/test/java/com/dspc/planning/api     MockMvc contract test, contract sync test
src/test/resources/scenarios            baseline.json, act40-delay.json (copies of packages/contracts/examples)
```
