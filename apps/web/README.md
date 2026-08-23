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
