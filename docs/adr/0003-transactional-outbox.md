# ADR-0003 — Transactional outbox for domain events

**Status:** accepted · **Date:** 2026-08-23

## Context
Supplier updates must fan out (risk re-scoring, notifications, passport invalidation, SignalR push) without losing events or double-publishing, and a message broker must be attachable later without touching the domain.

## Decision
Handlers call `IEventPublisher.Publish(event)`; the `OutboxEventPublisher` adds an `outbox_messages` row to the same `DbContext`, persisted by the command's `SaveChanges`. `OutboxDispatcherHostedService` polls every 500 ms, invokes in-process `IDomainEventHandler<T>` implementations and broadcasts `DomainEvent(name, payload)` through SignalR, marking rows processed with retry/backoff (max 8 attempts). Event names are fixed strings (`ShipmentEtaChanged`, `DeliveryRiskChanged`, …) shared with the web client.

## Consequences
- At-least-once delivery; handlers must be idempotent.
- A broker adapter only needs to implement the dispatcher's "deliver" step.
- Up to ~0.5 s latency between a command and the dashboard refresh — acceptable for the demo.
