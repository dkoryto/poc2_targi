# ADR-0001 — Modular monolith in .NET + separate Java planning engine

**Status:** accepted · **Date:** 2026-08-23

## Context
The demonstrator must run reliably on a single trade-show laptop, yet show enterprise-grade structure and allow later extraction of modules. The spec mandates ASP.NET Core for business logic and Java/Spring Boot for the MRP engine.

## Decision
One ASP.NET Core service (`business-api`) organised as vertical slices per domain module (Suppliers, Inbound, Risk, Inventory, Production/Planning, Quality, Traceability, Documents, Passports, Identity, Audit, Dashboard, Demo) in four projects: `Dspc.Domain` (entities, enums, domain events, pure rules such as `RiskScoreCalculator`), `Dspc.Application` (handlers + DTOs + validators per module, abstractions), `Dspc.Infrastructure` (EF Core/PostgreSQL, outbox, storage, identity, seeder), `Dspc.Api` (minimal-API endpoint groups, SignalR, auth, middleware). The planning engine is a stateless Spring Boot service called over an OpenAPI contract; every request/response is persisted for audit.

## Consequences
- Single deployable, single database, one transaction per command; no distributed consistency problems during the demo.
- Module boundaries are folders + DI extensions, not processes — extracting a module later means moving its slice and replacing in-process calls with the outbox/broker.
- Engine can be swapped (or down) without breaking the API: `BaselineImpactEvaluator` is the deterministic fallback.
