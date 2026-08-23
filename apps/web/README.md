# apps/web — Control-room UI

React 19 + TypeScript (strict, `noUncheckedIndexedAccess`) + Vite 6. PL default, EN switch.

## Scripts

```bash
pnpm install
pnpm dev          # http://localhost:5173, proxies /api, /hubs, /health → http://localhost:5080
pnpm dev:mock     # same, but MSW intercepts the API with fixtures from src/mocks (no backend needed)
pnpm test         # vitest (jsdom + msw)
pnpm test -- src/components/gantt/Gantt.test.tsx   # single file
pnpm lint && pnpm typecheck
pnpm build        # tsc -b && vite build → dist/
pnpm gen:api      # openapi-typescript from running API → src/api/generated.ts (hooks still use src/api/types.ts; swap when stable)
```

## Structure

```
src/
  api/            client.ts (fetch wrapper: bearer, X-Correlation-Id, Idempotency-Key, ETag/If-Match, Problem Details → ApiError), types.ts (contract from docs/api/endpoints.md), keys.ts (query keys)
  components/ui   design system (Button, Card, StatusChip/RiskBadge, KpiTile, DataTable, Tabs, Dialog/ConfirmDialog, Drawer, Toast, Tooltip, Skeleton, Empty/ErrorState, Form*, Timeline, ProgressBar, Badge, SegmentedControl, DisclaimerBanner)
  components/gantt  SVG Gantt (rows by work center / order, zoom 4/8/12 wk, frozen/material-wait patterns, dependencies, conflicts, `compare={{before}}` ghost bars + Δdays)
  components/layout AppShell, TopBar (clock, online/offline, site, PL/EN, notifications, role switcher, reset, Run demo), Nav (RBAC-filtered), PresenterPanel
  features/<module>/ api.ts (TanStack Query hooks) + pages/components: auth, dashboard, supply, inbound, notifications, demo
  realtime/useLive.ts SignalR /hubs/live → query invalidation + toasts + `onDomainEvent` bus (map/KPI pulse)
  i18n/pl.json, en.json   every string, statuses, risk factors, KPI definitions, planning explanation reason codes
  mocks/          msw fixtures mirroring docs/architecture/demo-scenario.md (tests + dev:mock)
  styles/tokens.css, global.css
public/geo/europe.geojson   Natural Earth 110m countries clipped to Europe (public domain) — the only map data; no tiles, no internet
```

## Design tokens

`src/styles/tokens.css`: `--bg-0..3`, `--fg-0..3`, `--ok` (teal), `--warn` (amber), `--crit` (red), `--info` (blue), `--risk-*`, radii 4–6 px, `--dur` 180 ms (0 under `prefers-reduced-motion`). Font stack is `system-ui, "Segoe UI", Roboto, …` — no web font download (offline). Status is never colour-only: use `StatusChip`/`RiskBadge` (icon + label).

## Adding a screen

1. Types in `src/api/types.ts` (keep `docs/api/endpoints.md` in sync).
2. Hooks in `src/features/<module>/api.ts` with keys from `src/api/keys.ts`; add invalidations in `realtime/useLive.ts` if a domain event affects it.
3. Page component + route in `src/App.tsx`; add role access in `features/auth/auth.tsx` `ROUTE_ROLES` and nav item in `components/layout/Nav.tsx`.
4. Strings in both `i18n/pl.json` and `i18n/en.json` (same keys). Explanation reason codes live under `explain.*` with `{{param}}` placeholders.
5. Fixtures + handler in `src/mocks`, and a test.

## i18n rules

- All user-visible text via `t()`; keys grouped by module. Plurals use i18next `_one/_few/_many/_other`.
- Dates: `lib/format.ts` (site TZ Europe/Warsaw, `dd.MM.yyyy`), numbers via `Intl` with `pl-PL`/`en-GB`.
- Backend reason codes / enums are translated in the UI, never displayed raw.

## Docker

`Dockerfile` builds with pnpm and serves `dist/` with nginx (`nginx.conf`: SPA fallback, proxies `/api`, `/health`, `/swagger`, `/hubs` (WebSocket upgrade) to `business-api:5080`, security headers, gzip).

## Wave 2 screens

`/planning` (presets + custom builder → `/planning/scenarios/:id` with Before/After/Compare Gantt, KPI deltas, explanations, approve/reject/save), `/trace` (search, `/trace/serials/:serial` genealogy tree + trace-back + audit, `/trace/lots`, `/trace/lots/:lot` trace-forward + block + inspection), `/passports` + `/passports/:serial` (DQP-01 completeness, versions, QR, generate), `/audit` (diff viewer, CSV), `/admin` (service status, settings, demo state), `/demo/summary` (value screen).

### `data-testid` reference (used by `tests/e2e`)

| Screen | Test ids |
|---|---|
| Control room | `kpi-<CODE>`, `kpi-row`, `panel-map/heatmap/plan/quality`, `open-whatif`, `open-blocked-lots` |
| Planning | `planning-page`, `baseline-meta`, `scenario-tile-<presetKey>` (`ACT40_DELAY`, `MCU_X7_DELAY`, `HTS22_BLOCK`, `WO014_PRIORITY`, `WC_INT_CAPACITY`, `custom`), `btn-custom-scenario`, `btn-add-change`, `btn-create-scenario`, `scenario-list` |
| Scenario detail | `scenario-detail`, `scenario-status` (raw status text, sr-only), `scenario-running`, `scenario-changes`, `solver-badge`, `kpi-compare`, `kpi-delta-downtime`, `gantt`, `gantt-bar-<opCode>` (`data-changed`), `gantt-ghost`, `gantt-shift`, `explanation-<reasonCode>`, `moved-ops`, `btn-run-scenario`, `btn-approve-plan`, `btn-reject-plan`, `btn-save-scenario`, `confirm-button` |
| Trace | `trace-page`, `trace-search`, `trace-quick-<code>`, `trace-hit-<code>`, `serial-page`, `genealogy-tree`, `trace-node-<code>`, `trace-toggle-<code>`, `trace-node-panel`, `trace-node-open`, `trace-node-download`, `trace-components`, `audit-export`, `open-passport` |
| Lots | `lots-page`, `lots-table`, `lot-page`, `trace-forward`, `btn-block-lot`, `block-reason`, `block-ncr`, `block-result`, `btn-add-inspection`, `submit-inspection` |
| Passports | `passports-page`, `passports-table`, `passport-filter-<Status>`, `passport-page`, `passport-status`, `passport-completeness`, `passport-missing`, `passport-complete`, `passport-req-<CODE>`, `passport-versions`, `passport-pdf-<v>`, `passport-qr`, `passport-invalidated`, `btn-approve-passport`, `btn-generate-passport` |
| Audit / admin | `audit-page`, `audit-table`, `audit-row-<id>`, `audit-detail`, `json-diff`, `audit-export`, `admin-page`, `service-<name>`, `service-signalr`, `settings-tables`, `summary-page` |

Mocks for all wave-2 endpoints live in `src/mocks/wave2.ts` (stateful: scenarios run → complete after ~0.7 s, lot block invalidates passports, generate bumps versions; `resetMockState()` restores).
