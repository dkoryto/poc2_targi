# ADR 0005 — What-If scenario execution and solver fallback

**Status:** accepted · **Date:** 2026-08-23 · **Context:** spec §5.4, §13, §15.6

## Context

A trade-show scenario must return a Before/After plan in under three seconds and must never dead-end in front of an
audience, even if the Java planning engine is slow, restarting or absent. Scenarios must also be auditable and must
never silently change the plan the shop floor is working to.

## Decision

1. **Scenario changes are applied to the problem, not to the database.** `PlanModelBuilder` already accepts a
   `PlanOverrides` bag (delayed inbound ETAs, blocked lots, priorities, work-centre capacity factors, order delays);
   `ScenarioCalculations.BuildOverrides` maps the five `ScenarioChange` types onto it. Nothing in the scenario path
   writes to inventory, lots or purchase orders.
2. **"Before" and "After" come from two different evaluations of the same problem.**
   *Before* = `BaselineImpactEvaluator` — the baseline with the change applied and **no re-sequencing**, which is what
   makes the pain visible (36 h of idle WC-INT for the ACT-40 delay). *After* = the engine's proposal. Both are rendered
   through the same `GanttBuilder`, so the UI can overlay them.
3. **Runs are queued, not awaited in the request.** `POST /planning/scenarios/{id}/run` flips the row to `Running`,
   enqueues on an in-process `Channel` and returns `202`. `ScenarioRunnerHostedService` executes it on its own DI scope
   and publishes `PlanningScenarioCompleted` through the outbox, so the browser learns about completion over SignalR
   (with a 1 s poll as a belt-and-braces fallback). Re-running while `Running` is a no-op; an approved scenario is
   terminal (`409`).
4. **The engine client never throws for transport problems.** Timeout (`PlanningEngine:TimeoutMs`, default 3000 ms,
   enforced by a per-call `CancellationTokenSource`), connection failure, non-2xx or an empty body all degrade to the
   same deterministic `BaselineImpactEvaluator`, flagged `solver = "Heuristic fallback"`, `status = FALLBACK`, plus a
   `FALLBACK_USED` explanation the UI renders as a warning chip. The demo continues with a valid — if unoptimised — plan.
5. **Explanations stay reason codes.** The engine's `reasonCode + params` are passed through untouched; the API only
   adds what the engine cannot know (`FALLBACK_USED`, the caller-visible `DOWNTIME_REDUCED` delta when the engine did
   not emit one, and `NO_CHANGE` for a scenario that changes nothing). All localisation happens in the web app.
6. **Approval is the only write to the plan.** It copies the active baseline into version *n+1*, applies the proposed
   windows, marks the old version `Superseded` (it is kept), records an audit entry with before/after and raises
   `ProductionPlanApproved`. `SourceScenarioId` links the baseline back to the scenario that produced it.
7. **Request and response JSON are persisted on the scenario row** (`RequestJson`, `ResponseJson`, `BeforeJson`,
   `AfterJson`, KPI snapshots) so a run can be re-examined or replayed after the fact. Consequences are derived from
   those snapshots on read rather than stored, which avoided a schema change.

## Consequences

- No new migration was needed — the wave-1 `InitialCreate` model already covered `PlanningScenario`, `ScenarioChange`
  and `PlanningRecommendation`.
- In fallback mode *After* equals *Before* (the local heuristic does not pull orders forward), so the operator sees the
  delay and its consequences but not the recovery proposal. This is the deliberate trade: correctness and availability
  over cleverness when the solver is down.
- The in-process queue is bounded by the process lifetime: a scenario left `Running` when the API restarts stays
  `Running`. Acceptable for a demonstrator; a durable queue would be needed in production.
