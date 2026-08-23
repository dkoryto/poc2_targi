# apps/web — Control-room UI

React 19 + TypeScript (strict, `noUncheckedIndexedAccess`) + Vite 6. PL default, EN switch.

## Scripts

```bash
pnpm install
pnpm dev          # http://localhost:5173, proxies /api, /hubs, /health → http://localhost:5080
                  # override the API with VITE_API_TARGET=http://localhost:5180 pnpm dev
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

## Design tokens and theming

`src/styles/tokens.css` holds **both palettes**: `:root` is dark (the default control-room look), `:root[data-theme='light']` is the light palette, and a `prefers-color-scheme` block covers the pre-hydration moment. Tokens: `--bg-0..3`, `--fg-0..3`, `--ok`/`--warn`/`--crit`/`--info` (+ `-bg`/`-border`/`-fg` variants), `--risk-*`, `--on-accent` (text on a saturated fill), `--surface-paper` (stays white in both themes, e.g. QR codes), `--map-*` (map surfaces, marker stroke/ring, overlays), `--chart-1..6`, radii 4–6 px, `--dur` 180 ms (0 under `prefers-reduced-motion`). Font stack is `system-ui, "Segoe UI", Roboto, …` — no web font download (offline). Status is never colour-only: use `StatusChip`/`RiskBadge` (icon + label).

**No component may hard-code a colour.** SVG (Gantt, heatmap) can use `var(--token)` directly. WebGL cannot, so `DeliveryMap` reads concrete values with `readThemeColor()` from `src/theme/theme.tsx` and repaints its layers via `setPaintProperty` when the `dspc:themechange` event fires — the map is never recreated.

`ThemeProvider` / `useTheme()` / `ThemeSwitch` live in `src/theme/theme.tsx`. The preference is `auto | light | dark` (persisted in `localStorage` under `dspc.theme`, all access wrapped in try/catch); `auto` follows `prefers-color-scheme` live. An inline snippet in `index.html` stamps the resolved theme on `<html>` before React mounts, so there is no flash.

## Layout

The side nav collapses to a 56 px icon rail (`data-testid="nav-toggle"` in the top bar, `aria-expanded` + `aria-controls="main-nav"`). The choice persists in `localStorage` (`dspc.nav.collapsed`); below 1200 px the rail is the default. Collapsing dispatches a window `resize` so the map and Gantt re-measure. Rail items keep an accessible name and show a tooltip on hover/focus.

A global `ErrorBoundary` (in `components/ui`) wraps the routed content in `AppShell`, keyed on the route path: a component failure renders a localized error card (with the Problem Details `traceId` when present) instead of blanking the page.

## Adding a screen

1. Types in `src/api/types.ts` (keep `docs/api/endpoints.md` in sync).
2. Hooks in `src/features/<module>/api.ts` with keys from `src/api/keys.ts`; add invalidations in `realtime/useLive.ts` if a domain event affects it.
3. Page component + route in `src/App.tsx`; add role access in `features/auth/auth.tsx` `ROUTE_ROLES` and nav item in `components/layout/Nav.tsx`.
4. Strings in both `i18n/pl.json` and `i18n/en.json` (same keys). Explanation reason codes live under `explain.*` with `{{param}}` placeholders.
5. Fixtures + handler in `src/mocks`, and a test.

## Multi-site

`src/features/sites/` holds `SiteProvider` / `useSite()` / `SiteSwitch` / `SiteChip`. The active plant comes
from `localStorage` (`dspc.site`), then `/auth/me`'s `siteCode`, then the plant flagged `isDefault`; a stored
plant outside the user's `availableSites` is discarded. See `docs/architecture/multi-site.md` for the plants
and the API contract.

Rules when adding a screen:

- Site-scoped hooks take the plant from `useScopedSiteCode()` (params) and put it in the **query key**, so a
  switch refetches instead of showing another plant's data; gate them with `enabled: useSiteReady()`.
- `useSiteReady()` is false until both auth and `/sites` have settled, so each hook fires once with the right
  key rather than re-keying a request already in flight.
- An API **without** `/sites` still works: the provider synthesises a single plant from the user's `siteCode`,
  `scoped` is false and `?siteCode=` is omitted entirely, so a single-plant backend behaves exactly as before.
- Label any record that may belong to another plant with `<SiteChip code={…} />`.

## Responsive primitives

Breakpoints and the absolute rules live in `docs/architecture/responsive.md`
(`mobile < 768`, `tablet 768–1199`, `desktop ≥ 1200`, `wall ≥ 1600`). Import everything
below from `@/components/ui`.

| Primitive | Props | What it does |
|---|---|---|
| `useIsMobile()` / `useIsCompact()` / `useMediaQuery(q)` | — | `< 768` / `< 1200` / arbitrary query. jsdom-safe (returns `false` without `matchMedia`). |
| `DataTable` | `responsive?: 'cards' \| 'scroll'` (default `'cards'`), `Column.card?: 'title' \| 'meta' \| 'hidden'` | Below `md` renders one card per row instead of a table, so no column falls off the edge. `card: 'title'` picks the heading (defaults to the first column), `'meta'` sits next to it unlabelled, `'hidden'` is dropped. Sorting moves into a `<select>` (`data-testid="card-sort"`); loading/empty/error states are unchanged. `'scroll'` keeps the table. |
| `Sheet` | `open`, `onClose`, `title`, `children`, `actions?`, `footer?`, `side?`, `wide?` | Side panel on desktop, full-width bottom sheet on mobile. Traps focus, closes on Escape / backdrop / swipe down. |
| `Drawer` | unchanged (`open`, `onClose`, `title`, `children`, `wide?`, `actions?`) | Now a thin wrapper over `Sheet`, so every existing drawer became a focus-trapped bottom sheet on mobile for free. |
| `ScrollArea` | `label` (required), `axis?: 'x' \| 'both'`, `className?` | Wide content scrolls **inside** this, never on the page. Edge shadows, keyboard focusable. |
| `OverflowMenu` / `OverflowItem` | `children`, `label?` / `label`, `children` | The "⋯" menu. Renders only below `lg`; put controls that do not fit inside it rather than hiding them. |
| `FilterBar` | `children`, `activeCount?`, `onClear?`, `clearLabel?` | Inline filters on desktop; below `md` they collapse behind a "Filtry" button carrying the active-filter count. |

Rules worth repeating when adding a screen:

- Never let the page scroll sideways — put wide content in `ScrollArea` and keep
  `min-width: 0` on flex/grid children.
- Nothing may disappear at a small width; move it into `OverflowMenu`.
- Labels wrap rather than truncate; touch targets are at least 44 px.
- Use `100dvh` (not `vh`) and the `--safe-*` inset tokens for anything pinned to an edge.
- `[hidden]` is forced to `display: none` globally — an author `display` used to beat it.

## i18n rules

- All user-visible text via `t()`; keys grouped by module. Plurals use i18next `_one/_few/_many/_other`.
- Dates: `lib/format.ts` (site TZ Europe/Warsaw, `dd.MM.yyyy`), numbers via `Intl` with `pl-PL`/`en-GB`.
- Backend reason codes / enums are translated in the UI, never displayed raw.

## Docker

`Dockerfile` builds with pnpm and serves `dist/` with nginx (`nginx.conf`: SPA fallback, proxies `/api`, `/health`, `/swagger`, `/hubs` (WebSocket upgrade) to `business-api:5080`, security headers, gzip).

## Wave 2 screens

`/planning` (presets + custom builder → `/planning/scenarios/:id` with Before/After/Compare Gantt, KPI deltas, explanations, approve/reject/save), `/trace` (search, `/trace/serials/:serial` genealogy tree + trace-back + audit, `/trace/lots`, `/trace/lots/:lot` trace-forward + block + inspection), `/passports` + `/passports/:serial` (DQP-01 completeness, versions, QR, generate), `/audit` (diff viewer, CSV), `/admin` (service status, settings, demo state), `/demo/summary` (value screen).

### `data-testid` reference (used by `tests/e2e`)

Responsive additions: `nav-toggle` (hamburger below `md`), `nav-drawer`, `overflow-menu`,
`row-card`, `card-sort`, `filter-toggle`, `filter-panel`, `map-legend-toggle`,
`gantt-view`, `gantt-list`, `gantt-op-<code>`.


| Screen | Test ids |
|---|---|
| Control room | `kpi-<CODE>`, `kpi-row`, `panel-map/heatmap/plan/quality`, `open-whatif`, `open-blocked-lots` |
| Planning | `planning-page`, `baseline-meta`, `scenario-tile-<presetKey>` (`DELAY_ACT40_10D`, `DELAY_MCUX7_14D`, `BLOCK_LOT_HTS22`, `PRIORITY_WO014`, `CAPACITY_INT_50`, `custom` — the API's `key`; its `titleKey` is the i18n name, resolved bare or fully qualified), `btn-custom-scenario`, `btn-add-change`, `btn-create-scenario`, `scenario-list` |
| Scenario detail | `scenario-detail`, `scenario-status` (raw status text, sr-only), `scenario-running`, `scenario-changes`, `solver-badge`, `kpi-compare`, `kpi-delta-downtime`, `gantt`, `gantt-bar-<opCode>` (`data-changed`), `gantt-ghost`, `gantt-shift`, `explanation-<reasonCode>`, `moved-ops`, `btn-run-scenario`, `btn-approve-plan`, `btn-reject-plan`, `btn-save-scenario`, `confirm-button` |
| Trace | `trace-page`, `trace-search`, `trace-quick-<code>`, `trace-hit-<code>`, `serial-page`, `genealogy-tree`, `trace-node-<code>`, `trace-toggle-<code>`, `trace-node-panel`, `trace-node-open`, `trace-node-download`, `trace-components`, `audit-export`, `open-passport` |
| Lots | `lots-page`, `lots-table`, `lot-page`, `trace-forward`, `btn-block-lot`, `block-reason`, `block-ncr`, `block-result`, `btn-add-inspection`, `submit-inspection` |
| Passports | `passports-page`, `passports-table`, `passport-filter-<Status>`, `passport-page`, `passport-status`, `passport-completeness`, `passport-missing`, `passport-complete`, `passport-req-<CODE>`, `passport-versions`, `passport-pdf-<v>`, `passport-qr`, `passport-invalidated`, `btn-approve-passport`, `btn-generate-passport` |
| Shell | `nav-toggle`, `main-nav` (`data-collapsed`), `theme-switch` (+ `theme-switch-auto/-light/-dark`), `lang-switch`, `site-switch`, `site-option-<CODE>`, `error-boundary`, `error-retry`, `error-reload` |
| Planning (multi-site) | `scenario-featured-<presetKey>` on the active plant's headline tile |
| Map | `map-supplier-<CODE>`, `map-shipment-<CODE>`, `map-site-<CODE>` (other plants) |
| Audit / admin | `audit-page`, `audit-table`, `audit-row-<id>`, `audit-detail`, `json-diff`, `audit-export`, `admin-page`, `service-<name>`, `service-signalr`, `settings-tables`, `summary-page` |

Mocks for all wave-2 endpoints live in `src/mocks/wave2.ts` (stateful: scenarios run → complete after ~0.7 s, lot block invalidates passports, generate bumps versions; `resetMockState()` restores).
