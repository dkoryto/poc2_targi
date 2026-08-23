# Development guide

Orientation for anyone working in this repository: the commands you need, and the architecture decisions that are not obvious from the file tree.

## What this is

**Defense Supply & Production Control** — local, offline trade-show demonstrator (Polish UI, EN switch). Full spec: `poc.md` (Polish; sections 5–15 are the functional contract, 15 = MVP acceptance criteria, 18 = phase order). All data is fictional; UI/docs must carry the disclaimer "Demonstrator wykorzystuje fikcyjne dane…". Never claim NATO/AQAP/STANAG compliance or certification.

Demo golden path that must always work end-to-end (on the **Kielce** plant, `SITE-01`):
`ACT-40 ETA +10 days → risk rises → WO-2026-014 endangered → What-If → solver → Gantt Before/After → WO-2026-019 pulled forward → trace serial → passport PDF`.
Secondary path: block lot `HTS-22-2608` → trace-forward → passports invalidated.

The demonstrator runs **four plants** (Kielce, Piła, Zamość, Leszno), each with its own data, baseline and lead
scenario — see [`docs/architecture/multi-site.md`](docs/architecture/multi-site.md). Kielce is the one every
fixture, doc and e2e spec is written against; changing its numbers breaks all three.

## Layout (monorepo, no workspace tool — each app is self-contained)

| Path | Stack | Role |
|---|---|---|
| `apps/web` | React 19 + TS strict, Vite, TanStack Query, react-hook-form + zod, MapLibre GL (local GeoJSON only), custom SVG Gantt, i18next PL/EN, Vitest | Control-room UI |
| `apps/business-api` | ASP.NET Core 10 LTS (.NET 10 SDK), EF Core + Npgsql, SignalR, Swashbuckle, QuestPDF + QRCoder, xUnit | Modular monolith — all business logic, auth, audit, PDF |
| `apps/planning-engine` | Java 17, Spring Boot 3, Maven, deterministic heuristic solver (no external CSP lib) | MRP / re-scheduling only; stateless |
| `packages/contracts` | OpenAPI YAML (`planning-engine.yaml`) + example problems in `examples/` | Single source for the .NET ↔ Java contract and the baseline fixture |
| `packages/demo-data` | JSON seed files | Loaded by business-api seeder; deterministic |
| `infrastructure/` | notes for identity and observability; production reverse proxy under `production/` | |
| `tests/e2e` | Playwright specs + `mobile-check.mjs` (responsive audit) | Golden path, multi-plant, smoke |
| `docs/` | architecture, ADRs, demo script, API, screenshots | |

## Commands

```bash
# whole stack (demo profile: auto-migrate, seed, auto-login as DemoPresenter)
docker compose --profile demo up --build
./scripts/start.sh            # same + waits for health + runs smoke test
./scripts/reset-demo.sh       # POST /api/v1/demo/reset

# business-api (run from apps/business-api)
dotnet build
dotnet run --project src/Dspc.Api                    # http://localhost:5080, swagger at /swagger
dotnet test                                          # all
dotnet test --filter "FullyQualifiedName~RiskScoreTests"   # single class
# dotnet-ef is a LOCAL tool (apps/business-api/dotnet-tools.json) — run from this directory or it is not found:
dotnet ef migrations add <Name> -p src/Dspc.Infrastructure -s src/Dspc.Api

# planning-engine (run from apps/planning-engine; use the wrapper, no system Maven needed)
./mvnw -q spring-boot:run                            # http://localhost:8081
./mvnw -q test
./mvnw -q test -Dtest=Act40DelayScenarioTest         # single test

# web (run from apps/web)
pnpm install && pnpm dev                             # http://localhost:5173, proxies /api and /hubs to :5080
pnpm dev:mock                                        # MSW fixtures, no backend needed
pnpm test                                            # vitest
pnpm test src/components/ui/KpiTile.test.tsx         # single file — NOTE: no `--`, which vitest treats as "run all"
pnpm lint && pnpm typecheck
pnpm gen:api                                         # regenerate TS client from running API swagger

# e2e (run from tests/e2e; needs the stack up on :5173/:5080)
pnpm install && pnpm exec playwright install chromium
pnpm test                                            # all specs
pnpm smoke                                           # @smoke only
pnpm exec playwright test specs/02-whatif.spec.ts    # single file
node mobile-check.mjs                                # responsive audit at 360/390/768/1920 px, both themes
```

Local dev without Docker: business-api needs `ConnectionStrings__Default` to a Postgres; set `Storage__Provider=FileSystem` to skip MinIO; planning engine optional (API falls back to `Heuristic fallback`).

### Seed and reset

The demo content is seeded by `MigrateAndSeedHostedService` on startup, and only when `Demo:Enabled=true` or the
environment is Development. Consequences worth knowing before a deployment:

- With `Demo__Enabled=false` the database migrates but **stays empty** — no plants, no users, nobody can log in.
- Re-seeding an existing database is triggered by the `SeedVersion` constant differing from the `seed_metadata` row,
  not by the data files changing. Bump the constant when you change `packages/demo-data`.
- `POST /api/v1/demo/reset` (button **Resetuj demo**, or `./scripts/reset-demo.sh`) truncates every mapped table in one
  `TRUNCATE … CASCADE` and reseeds, then runs post-processors that render the passport PDFs. Measured 0.7–2.0 s
  against the 10 s budget; the response reports `durationMs` and per-entity counts.
- Ids are SHA-1-derived GUIDs of business codes, so a reset reproduces identical ids and deep links keep working.
  Only `createdAt`-style timestamps differ between resets.

## Architecture rules that matter

- **business-api is a modular monolith.** `src/Dspc.Domain` (entities, enums, domain events — no EF attributes), `src/Dspc.Application` (vertical slices per module: `Modules/{Admin,Audit,Dashboard,Demo,Documents,Identity,Inbound,Inventory,Notifications,Passports,Planning,Quality,Risk,Sites,Suppliers,Traceability}`, each with handlers + DTOs), `src/Dspc.Infrastructure` (EF `AppDbContext`, migrations, MinIO/FS storage, planning-engine HTTP client, PDF), `src/Dspc.Api` (minimal-API endpoint groups, SignalR `LiveHub`, auth). DTOs never leak entities; EF entities never reach the wire.
- **Events:** domain events are written to an `OutboxMessage` table in the same transaction, then dispatched by a hosted service → in-process handlers + SignalR broadcast. Event names are fixed strings (`ShipmentEtaChanged`, `DeliveryRiskChanged`, `MaterialLotBlocked`, `PlanningScenarioCompleted`, `ProductionPlanApproved`, `PassportInvalidated`, `PassportGenerated`, …). Frontend listens on `/hubs/live`, method `DomainEvent(name, payload)` and invalidates TanStack queries by name.
- **Risk score is rule-based, explainable, not "AI".** Formula + weights live in `Dspc.Domain/Risk/RiskScoreCalculator.cs` and `appsettings` `Risk:Weights`. Returns score 0–100, category (Low <25, Medium <50, High <75, Critical), and top-3 factors. Documented in `docs/architecture/risk-model.md`. Every ETA/doc/lot change triggers re-scoring of affected PO lines.
- **Planning:** business-api assembles a `PlanningRequest` (work centers + calendars, orders + operations + material requirements, material availability incl. inbound ETAs, weights) — it applies the scenario's changes to the inputs *before* calling the engine. Engine is stateless and deterministic (same input → same output). If engine fails/times out (3 s), `Dspc.Application/Modules/Planning/Scheduling/BaselineImpactEvaluator.cs` produces a result flagged `Solver = "Heuristic fallback"` (the client that falls back is `Dspc.Infrastructure/Planning/PlanningEngineClient.cs`). Request + response JSON are persisted on `PlanningScenario` for audit. Explanations are `reasonCode + params`, localized in the frontend — never free text from the solver.
- **Scenarios never mutate the baseline.** Approval copies the current baseline to a new `PlanningBaseline` version, applies the proposal, raises `ProductionPlanApproved`, audits it.
- **Passports:** completeness rules in `Dspc.Domain/Quality/PassportCompletenessEvaluator.cs` return a list of missing items (codes). PDF generated only when empty. Each generation = new `PassportVersion` with SHA-256 + QR (links to `/passports/{serial}`); old versions retained. `MaterialLotBlocked` invalidates passports whose genealogy contains the lot.
- **RBAC is enforced in API**, not just UI: policies per role; supplier users are scoped to their `SupplierId` in every query (`ISupplierScope`). Tests in `apps/business-api/tests/Dspc.Api.Tests/AuthorizationTests.cs` must keep passing.
- **Plant scoping** is a second, independent boundary: `ISiteContext` turns `?siteCode=` into a site id (absent → the caller's default plant, unknown → `404`, out of reach → `403`); query classes take a plain `Guid siteId`. `/notifications` and `/audit` are the documented exceptions — they accept the parameter and ignore it.
- **Demo mode** (`Demo:Enabled=true`): `/api/v1/auth/demo-login?role=` issues JWT for any role, `/api/v1/demo/reset` truncates + reseeds in <10 s, UI shows role switcher + presenter panel. Outside demo mode these endpoints return 404.
- **Seed version must be bumped by hand.** Re-seeding on an existing database is triggered by the `SeedVersion` constant differing from the `seed_metadata` row. Change seed data without bumping it and an upgraded database silently keeps the old content.
- **Seed is the scenario.** `packages/demo-data/*.json` is tuned so that ACT-40 +10 days actually starves `WO-2026-014` at `WC-INT` and `WO-2026-019` (100% material) can fill the gap. Don't "fix" numbers there without re-running `Act40DelayScenarioTest` (Java) and `PlanningScenarioTests` / `PlanningScenarioApiTests` / `GoldenPathTests` (.NET). KPIs are always computed from data.
- **Frontend:** `src/features/<module>/` (api hooks, components, pages), `src/components/ui` design system (dark navy/graphite, teal=ok, amber=warn, red=critical, blue=info — status always has label + icon, never color alone), `src/i18n/{pl,en}.json`. Map uses `public/geo/*.geojson`; no tile server, no internet. Gantt in `src/components/gantt` (SVG). Respect `prefers-reduced-motion`.
- Dates stored UTC, displayed in site TZ (`Europe/Warsaw`). Business codes (`WO-2026-014`, `PO-2026-0007`, `HTS-22-2608`) are separate from technical GUID ids.

## Key identifiers in demo data

Kielce (`SITE-01`): parts `ACT-40`, `MCU-X7`, `HTS-22`; lot `HTS-22-2608`; orders `WO-2026-014` (priority), `WO-2026-019` (alternative); work centers `WC-CUT, WC-WELD, WC-ELEC, WC-INT, WC-TEST`; lines `LINE-1, LINE-2`.
Other plants use prefixed namespaces — `PIL-`/`ZAM-`/`LES-` for work centres and lines, `WO-2026-1xx`/`2xx`/`3xx` for orders — so nothing collides with Kielce; the mapping is in [`docs/architecture/multi-site.md`](docs/architecture/multi-site.md).
Suppliers `SUP-01…SUP-08` are shared by all plants. Demo accounts in [`docs/demo-script/accounts.md`](docs/demo-script/accounts.md) (password `demo`, overridable with `Demo__AccountPassword`).

## Licensing constraints

Commercial-friendly only: QuestPDF Community (flag in docs), QRCoder (MIT), MapLibre (BSD), no OptaPlanner/Timefold (engine is custom). Keep `docs/licenses.md` updated when adding deps.
