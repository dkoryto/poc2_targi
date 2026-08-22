# E2E (Playwright)

Needs the stack running (`./scripts/start.sh`), or set `E2E_WEB_URL` / `E2E_API_URL`.

```bash
pnpm install
pnpm exec playwright install chromium
pnpm test          # all specs (resets demo data between suites)
pnpm smoke         # @smoke only
pnpm test -- specs/02-whatif.spec.ts   # single file
```
Specs rely on `data-testid` attributes documented in `apps/web/README.md` (`kpi-<CODE>`, `risk-badge`, `gantt-bar-<opCode>`, …).
