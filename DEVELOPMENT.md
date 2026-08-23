# Development guide

Orientation for anyone working in this repository: the commands you need, and the architecture decisions that are not obvious from the file tree.

## What this is

**Defense Supply & Production Control** — local, offline trade-show demonstrator (Polish UI, EN switch). Full spec: `poc.md` (Polish; sections 5–15 are the functional contract, 15 = MVP acceptance criteria, 18 = phase order). All data is fictional; UI/docs must carry the disclaimer "Demonstrator wykorzystuje fikcyjne dane…". Never claim NATO/AQAP/STANAG compliance or certification.

Demo golden path that must always work end-to-end:
`ACT-40 ETA +10 days → risk rises → WO-2026-014 endangered → What-If → solver → Gantt Before/After → WO-2026-019 pulled forward → trace serial → passport PDF`.
Secondary path: block lot `HTS-22-2608` → trace-forward → passports invalidated.

## Layout (monorepo, no workspace tool — each app is self-contained)

| Path | Stack | Role |
|---|---|---|
| `apps/web` | React 19 + TS strict, Vite, TanStack Query, react-hook-form + zod, MapLibre GL (local GeoJSON only), custom SVG Gantt, i18next PL/EN, Vitest | Control-room UI |
| `apps/business-api` | ASP.NET Core 10 LTS (.NET 10 SDK), EF Core + Npgsql, SignalR, Swashbuckle, QuestPDF + QRCoder, xUnit | Modular monolith — all business logic, auth, audit, PDF |
| `apps/planning-engine` | Java 17, Spring Boot 3, Maven, deterministic heuristic solver (no external CSP lib) | MRP / re-scheduling only; stateless |
| `packages/contracts` | OpenAPI YAML (`planning-engine.yaml`), generated TS client | Single source for cross-language DTOs |
| `packages/demo-data` | JSON seed files | Loaded by business-api seeder; deterministic |
| `infrastructure/compose` | compose fragments, MinIO/Postgres init | |
| `tests/e2e` | Playwright | Golden-path + smoke |
| `docs/` | architecture, ADRs, demo script, API | |

## Commands

```bash
# whole stack (demo profile: auto-migrate, seed, auto-login as DemoPresenter)
docker compose --profile demo up --build
./scripts/start.sh            # same + waits for health + runs smoke test
./scripts/reset-demo.sh       # POST /api/v1/demo/reset

# business-api (apps/business-api)
dotnet build
dotnet run --project src/Dspc.Api                    # http://localhost:5080, swagger at /swagger
dotnet test                                          # all
dotnet test --filter "FullyQualifiedName~RiskScoreTests"   # single class
dotnet ef migrations add <Name> -p src/Dspc.Infrastructure -s src/Dspc.Api

# planning-engine (apps/planning-engine)
mvn -q spring-boot:run                               # http://localhost:8081
mvn -q test
mvn -q test -Dtest=Act40DelayScenarioTest            # single test

# web (apps/web)
pnpm install && pnpm dev                             # http://localhost:5173, proxies /api and /hubs to :5080
pnpm test                                            # vitest
pnpm test -- src/features/risk/RiskBadge.test.tsx    # single file
pnpm lint && pnpm typecheck
pnpm gen:api                                         # regenerate TS client from running API swagger

# e2e (tests/e2e)
pnpm install && pnpm test                            # needs stack up on :5173/:5080
pnpm smoke
```

Local dev without Docker: business-api needs `ConnectionStrings__Default` to a Postgres; set `Storage__Provider=FileSystem` to skip MinIO; planning engine optional (API falls back to `Heuristic fallback`).

## Architecture rules that matter

- **business-api is a modular monolith.** `src/Dspc.Domain` (entities, enums, domain events — no EF attributes), `src/Dspc.Application` (vertical slices per module: `Modules/{Suppliers,Inbound,Risk,Inventory,Production,Planning,Quality,Traceability,Documents,Passports,Identity,Audit,Dashboard,Demo}`, each with handlers + DTOs), `src/Dspc.Infrastructure` (EF `AppDbContext`, migrations, MinIO/FS storage, planning-engine HTTP client, PDF), `src/Dspc.Api` (minimal-API endpoint groups, SignalR `LiveHub`, auth). DTOs never leak entities; EF entities never reach the wire.
- **Events:** domain events are written to an `OutboxMessage` table in the same transaction, then dispatched by a hosted service → in-process handlers + SignalR broadcast. Event names are fixed strings (`ShipmentEtaChanged`, `DeliveryRiskChanged`, `MaterialLotBlocked`, `PlanningScenarioCompleted`, `ProductionPlanApproved`, `PassportInvalidated`, `PassportGenerated`, …). Frontend listens on `/hubs/live`, method `DomainEvent(name, payload)` and invalidates TanStack queries by name.
- **Risk score is rule-based, explainable, not "AI".** Formula + weights live in `Dspc.Domain/Risk/RiskScoreCalculator.cs` and `appsettings` `Risk:Weights`. Returns score 0–100, category (Low <25, Medium <50, High <75, Critical), and top-3 factors. Documented in `docs/architecture/risk-model.md`. Every ETA/doc/lot change triggers re-scoring of affected PO lines.
- **Planning:** business-api assembles a `PlanningRequest` (work centers + calendars, orders + operations + material requirements, material availability incl. inbound ETAs, weights) — it applies the scenario's changes to the inputs *before* calling the engine. Engine is stateless and deterministic (same input → same output). If engine fails/times out (3 s), `Dspc.Application/Modules/Planning/FallbackScheduler.cs` produces a result flagged `Solver = "Heuristic fallback"`. Request + response JSON are persisted on `PlanningScenario` for audit. Explanations are `reasonCode + params`, localized in the frontend — never free text from the solver.
- **Scenarios never mutate the baseline.** Approval copies the current baseline to a new `PlanningBaseline` version, applies the proposal, raises `ProductionPlanApproved`, audits it.
- **Passports:** completeness rules in `Dspc.Domain/Passports/PassportCompletenessEvaluator.cs` return a list of missing items (codes). PDF generated only when empty. Each generation = new `PassportVersion` with SHA-256 + QR (links to `/passports/{serial}`); old versions retained. `MaterialLotBlocked` invalidates passports whose genealogy contains the lot.
- **RBAC is enforced in API**, not just UI: policies per role; supplier users are scoped to their `SupplierId` in every query (`ISupplierScope`). Tests in `tests/Dspc.Api.Tests/AuthorizationTests.cs` must keep passing.
- **Demo mode** (`Demo:Enabled=true`): `/api/v1/auth/demo-login?role=` issues JWT for any role, `/api/v1/demo/reset` truncates + reseeds in <10 s, UI shows role switcher + presenter panel. Outside demo mode these endpoints return 404.
- **Seed is the scenario.** `packages/demo-data/*.json` is tuned so that ACT-40 +10 days actually starves `WO-2026-014` at `WC-INT` and `WO-2026-019` (100% material) can fill the gap. Don't "fix" numbers there without re-running `Act40DelayScenarioTest` (Java) and `WhatIfScenarioTests` (.NET). KPIs are always computed from data.
- **Frontend:** `src/features/<module>/` (api hooks, components, pages), `src/components/ui` design system (dark navy/graphite, teal=ok, amber=warn, red=critical, blue=info — status always has label + icon, never color alone), `src/i18n/{pl,en}.json`. Map uses `public/geo/*.geojson`; no tile server, no internet. Gantt in `src/components/gantt` (SVG). Respect `prefers-reduced-motion`.
- Dates stored UTC, displayed in site TZ (`Europe/Warsaw`). Business codes (`WO-2026-014`, `PO-2026-0007`, `HTS-22-2608`) are separate from technical GUID ids.

## Key identifiers in demo data

Parts `ACT-40`, `MCU-X7`, `HTS-22`; lot `HTS-22-2608`; orders `WO-2026-014` (priority), `WO-2026-019` (alternative); suppliers `SUP-01…SUP-08`; site `SITE-01`; work centers `WC-CUT, WC-WELD, WC-ELEC, WC-INT, WC-TEST`; lines `LINE-1, LINE-2`. Demo accounts in `docs/demo-script/accounts.md` (password `demo` for all).

## Licensing constraints

Commercial-friendly only: QuestPDF Community (flag in docs), QRCoder (MIT), MapLibre (BSD), no OptaPlanner/Timefold (engine is custom). Keep `docs/licenses.md` updated when adding deps.
