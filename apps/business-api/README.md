# business-api — ASP.NET Core modular monolith

Business backend of the DSPC demonstrator: suppliers & inbound, rule-based delivery risk, dashboard KPIs, baseline plan
evaluation and What-If scenarios, documents, material lots and quality blocking, traceability (genealogy, trace-back and
trace-forward), digital quality passports (versioned PDF with QR and SHA-256), notifications, audit, identity and demo
seed/reset.

## Run

```bash
export PATH=$HOME/.dotnet:$PATH                                   # .NET 10 SDK
docker run -d --name dspc-pg-dev -e POSTGRES_PASSWORD=dspc -e POSTGRES_USER=dspc -e POSTGRES_DB=dspc -p 5432:5432 postgres:16-alpine
dotnet run --project src/Dspc.Api                                 # Development profile: demo enabled, FileSystem storage, http://localhost:5080
open http://localhost:5080/swagger
```

Development profile auto-migrates and seeds (`Demo:Enabled=true`, JWT dev key). For the demo/compose profile set
`ASPNETCORE_ENVIRONMENT=Demo` and `Identity__Jwt__Key` (≥ 32 chars) — the API refuses to start without it.

| Env var | Meaning | Default |
|---|---|---|
| `ConnectionStrings__Default` | PostgreSQL | `Host=localhost;…;Username=dspc;Password=dspc` |
| `Identity__Jwt__Key` / `__Issuer` / `__Audience` / `__LifetimeMinutes` | local JWT issuer | dev key only in Development |
| `Demo__Enabled` | demo-login, role switcher, `/demo/reset` (404 otherwise) | `false` (`true` in Development/Demo) |
| `Demo__ClockAnchor` | pin T0 to the Monday of that week (tests/e2e) | current week |
| `Storage__Provider` = `FileSystem` \| `Minio`, `Storage__Root`, `Storage__Minio__Endpoint/AccessKey/SecretKey/Bucket` | document storage | FileSystem `./storage` |
| `PlanningEngine__BaseUrl`, `__TimeoutMs` | Java engine | `http://localhost:8081`, 3000 |
| `LocalAi__Enabled`, `__BaseUrl`, `__Model`, `__Simulator` | optional local LLM adapter for certificate fields (proposals only); `Simulator` answers from a deterministic fixture | disabled, simulator on |
| `Cors__Origins` | comma list | `http://localhost:5173` |
| `Seed__Path` | demo JSON folder | auto-detect `packages/demo-data` |
| `Seed__Skip` | skip migrate/seed on startup | `false` |

## Test

```bash
dotnet test                                                       # all (API tests start a PostgreSQL Testcontainer)
dotnet test tests/Dspc.Domain.Tests                               # pure rules: risk formula, fallback scheduler, calendar
dotnet test --filter "FullyQualifiedName~EtaChangeRaisesRiskTests" # single class
ConnectionStrings__Test="Host=localhost;Port=5432;Database=dspc_test;Username=dspc;Password=dspc" dotnet test tests/Dspc.Api.Tests   # without Docker
```

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/Dspc.Infrastructure -s src/Dspc.Api -o Persistence/Migrations
```
`InitialCreate` covers the whole §7 model plus the append-only trigger on `audit_events`.

## Layout

| Project | Contents |
|---|---|
| `Dspc.Domain` | entities + enums (`Entities/*.cs`, `Common/Enums.cs`), domain events (`Events/DomainEvents.cs`), `Risk/RiskScoreCalculator` (pure, weights injected) |
| `Dspc.Application` | `Abstractions/` (db, current user, supplier scope, event publisher, clock, storage, scanner, seeder), `Modules/<Name>/` vertical slices: Identity, Dashboard, Suppliers, Inbound (PO/lines/ETA/shipments/logistics events), Documents, Inventory, Risk, Planning (`PlanModelBuilder` → engine contract, `Scheduling/BaselineImpactEvaluator` = impact + fallback, `GanttBuilder`, `ScenarioService` + `ScenarioRunnerHostedService` = What-If), Quality (`LotService` lots/inspections/blocking, `TraceabilityIndex`), Traceability (`TraceQueries` genealogy), Passports (`PassportService` completeness/approval/versioned PDF, `PassportInvalidationService`), Notifications, Audit, Demo, Admin |
| `Dspc.Infrastructure` | `Persistence/` (`AppDbContext`, one `IEntityTypeConfiguration` per entity, snake_case, `xmin` concurrency, migrations), `Seeding/DemoSeeder` (deterministic, T0-relative, fixture-driven), `Outbox/`, `Identity/` (PBKDF2 + JWT), `Storage/` (FileSystem, MinIO), `Services/` (clock, probes, recent errors, no-op scanner), `Documents/` (QuestPDF passport renderer + QRCoder, seed post-processor) |
| `Dspc.Api` | `Program.cs` composition, `Endpoints/*` minimal-API groups, `Auth/` (`HttpCurrentUser`, `SupplierScope`, `Policies`), `Middleware/` (correlation id, security headers, Problem Details handler, idempotency, validation filter), `Realtime/LiveHub` |

## Request pipeline

correlation id → Serilog request log → exception handler (RFC 7807) → security headers → CORS → rate limiter → JWT auth → authorization → idempotency (`Idempotency-Key`) → endpoint (+ FluentValidation filter). Mutations write an `AuditEvent` and outbox messages inside the same `SaveChanges`; the dispatcher delivers them to handlers and SignalR `/hubs/live` (`DomainEvent`).

## Golden path (curl)

```bash
API=http://localhost:5080/api/v1
T=$(curl -s "$API/auth/demo-login?role=SupplierUser&supplierCode=SUP-02" | jq -r .accessToken)
L=$(curl -s -H "Authorization: Bearer $T" $API/purchase-orders/PO-2026-0007 | jq -r '.lines[0].id')
curl -s -X POST -H "Authorization: Bearer $T" -H 'Content-Type: application/json' -d '{"eta":"2026-09-25","reason":"PRODUCTION_DELAY"}' $API/purchase-orders/PO-2026-0007/lines/$L/eta | jq '.risk.score, .endangeredOrders[].orderCode, .predictedDowntimeHours'
# → 79, "WO-2026-014", 36
curl -s -X POST -H "Authorization: Bearer $(curl -s "$API/auth/demo-login?role=DemoPresenter" | jq -r .accessToken)" $API/demo/reset | jq .durationMs
```
