/**
 * Regenerates every screenshot used in the documentation.
 *
 *   docker compose --profile demo up -d          # stack must be running
 *   node docs/screenshots/capture.mjs            # run from the repository root
 *
 * The script resets the demonstration data first, then walks the trade-show
 * scenario in order, so the shots tell one consistent story: a calm Control
 * Room, a supplier pushing an ETA, the risk reacting, the re-plan, the
 * genealogy and finally the passport.
 *
 * Playwright is used rather than a real browser window because Chrome clamps
 * its window to roughly 500 px, which makes true phone-width shots impossible.
 *
 * Dependencies: @playwright/test (installed in tests/e2e) and pdftoppm for the
 * PDF page render.
 */
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { createRequire } from 'node:module';
import { mkdir, writeFile, readdir, stat, rename, unlink } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const execFileAsync = promisify(execFile);

// Playwright is a dev dependency of tests/e2e, not of the repository root, so resolve it
// from there. That keeps this script runnable from any working directory.
const here = path.dirname(fileURLToPath(import.meta.url));
const e2eRequire = createRequire(path.join(here, '..', '..', 'tests', 'e2e', 'package.json'));
const { chromium } = e2eRequire('@playwright/test');

const WEB = process.env.DSPC_WEB ?? 'http://localhost:5173';
const API = process.env.DSPC_API ?? 'http://localhost:5080';
const OUT = here;

const DESKTOP = { width: 1920, height: 1080 };
const PHONE = { width: 390, height: 844 };
/** Downscaled from 1920 so a page stays readable in Markdown without a multi-megabyte file. */
const DESKTOP_TARGET_WIDTH = 1440;
const MAX_BYTES = 400 * 1024;

const log = (...a) => console.log('·', ...a);

// ---------------------------------------------------------------- API helpers

async function token(role, supplierCode) {
  const q = supplierCode ? `&supplierCode=${supplierCode}` : '';
  const res = await fetch(`${API}/api/v1/auth/demo-login?role=${role}${q}`);
  if (!res.ok) throw new Error(`demo-login ${role} failed: ${res.status}`);
  return (await res.json()).accessToken;
}

async function api(role, pathname, init = {}) {
  const t = await token(role);
  const res = await fetch(`${API}${pathname}`, {
    ...init,
    headers: { Authorization: `Bearer ${t}`, ...(init.headers ?? {}) },
  });
  if (!res.ok) throw new Error(`${pathname} failed: ${res.status}`);
  return res;
}

async function resetDemo() {
  for (let attempt = 0; attempt < 5; attempt++) {
    const t = await token('DemoPresenter');
    const res = await fetch(`${API}/api/v1/demo/reset`, { method: 'POST', headers: { Authorization: `Bearer ${t}` } });
    if (res.ok) return;
    if (res.status !== 429) throw new Error(`reset failed: ${res.status}`);
    await new Promise((r) => setTimeout(r, 3000));
  }
  throw new Error('reset failed: still rate limited');
}

// ---------------------------------------------------------------- image utils

/** Keeps files small enough to live comfortably in a Git repository. */
async function optimise(file, targetWidth) {
  await execFileAsync('sips', ['-Z', String(targetWidth), file], { encoding: 'utf8' }).catch(() => {});
  const { size } = await stat(file);
  if (size > MAX_BYTES) {
    await execFileAsync('sips', ['-Z', String(Math.round(targetWidth * 0.8)), file], { encoding: 'utf8' }).catch(() => {});
  }
}

async function shoot(page, name, { full = false, target = DESKTOP_TARGET_WIDTH } = {}) {
  const file = path.join(OUT, `${name}.png`);
  await page.screenshot({ path: file, fullPage: full });
  await optimise(file, target);
  const { size } = await stat(file);
  log(`${name}.png  ${(size / 1024).toFixed(0)} kB`);
}

// ---------------------------------------------------------------- page helpers

/**
 * Waits for the shell plus the panel that matters, so nothing is captured mid-skeleton.
 * One reload on failure: the first paint of a freshly created context occasionally loses the
 * race with the auto-login round trip, and a stale capture is worse than a slow one.
 */
async function ready(page, testId, timeout = 30_000) {
  const target = page.locator(`[data-testid="${testId}"]`).first();
  try {
    await target.waitFor({ state: 'visible', timeout });
  } catch {
    log(`retrying: ${testId} did not appear, reloading`);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await target.waitFor({ state: 'visible', timeout });
  }
  await page.waitForTimeout(1200);
}

async function newContext(browser, viewport, theme, extra = {}) {
  const ctx = await browser.newContext({
    viewport,
    deviceScaleFactor: 1,
    locale: 'pl-PL',
    timezoneId: 'Europe/Warsaw',
    colorScheme: theme,
    ...extra,
  });
  await ctx.addInitScript((t) => {
    try {
      localStorage.setItem('dspc.theme', t);
    } catch {
      /* private mode */
    }
  }, theme);
  return ctx;
}

async function switchPlant(page, code) {
  await page.getByTestId('site-switch').click();
  await page.getByTestId(`site-option-${code}`).click();
  await page.waitForTimeout(1500);
}

// ---------------------------------------------------------------- the scenario

async function main() {
  await mkdir(OUT, { recursive: true });
  log('resetting demonstration data');
  await resetDemo();

  const browser = await chromium.launch();

  // --- 1. calm Control Room, dark ------------------------------------------
  let ctx = await newContext(browser, DESKTOP, 'dark');
  let page = await ctx.newPage();
  await page.goto(`${WEB}/`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'kpi-MATERIAL_READINESS');
  await page.waitForTimeout(2500); // let the map tiles and Gantt settle
  await shoot(page, '01-control-room');

  // --- 10b. a second plant, so the four-plant story is visible --------------
  // Piła rather than Zamość or Leszno: GET /dashboard/map currently returns 500 for
  // SITE-03 and SITE-04 (a shipment with no lines hits .First() in DashboardQueries.MapAsync),
  // which renders the map panel as an error card. Point this back at SITE-03 once that is fixed.
  await switchPlant(page, 'SITE-02');
  await page.waitForTimeout(2000);
  await shoot(page, '10-control-room-pila');

  // --- 10a. the plant switcher, open ---------------------------------------
  await page.getByTestId('site-switch').click();
  await page.waitForTimeout(600);
  await shoot(page, '09-plant-switcher');
  await page.keyboard.press('Escape');
  await switchPlant(page, 'SITE-01');

  // --- 11. administration ---------------------------------------------------
  await page.goto(`${WEB}/admin`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  await shoot(page, '11-admin');
  await ctx.close();

  // --- 2. the same Control Room in the light theme --------------------------
  ctx = await newContext(browser, DESKTOP, 'light');
  page = await ctx.newPage();
  await page.goto(`${WEB}/`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'kpi-MATERIAL_READINESS');
  await page.waitForTimeout(2500);
  await shoot(page, '02-control-room-light');
  await ctx.close();

  // --- 3. supplier changes the ETA -----------------------------------------
  ctx = await newContext(browser, DESKTOP, 'dark');
  page = await ctx.newPage();

  const po = await (await api('DemoPresenter', '/api/v1/purchase-orders/PO-2026-0007')).json();
  const line = po.lines.find((l) => l.partCode === 'ACT-40') ?? po.lines[0];
  const newEta = new Date(new Date(line.eta).getTime() + 10 * 86_400_000).toISOString().slice(0, 10);
  log(`ETA ${line.eta.slice(0, 10)} → ${newEta} on PO-2026-0007 / ${line.partCode}`);

  await page.goto(`${WEB}/supply/orders/PO-2026-0007`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'po-page');
  await page.getByTestId(`edit-line-${line.lineNo}`).click();
  await ready(page, 'eta-form');
  await page.locator('[data-testid="eta-form"] input[type="date"]').fill(newEta);
  await page.getByTestId('submit-eta').click();
  await page.locator('[data-testid="risk-after-score"]').waitFor({ state: 'visible', timeout: 20_000 });
  await page.waitForTimeout(1200);
  await shoot(page, '03-supplier-eta-change');

  // --- 4. the Control Room reacting ----------------------------------------
  await page.goto(`${WEB}/`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'kpi-PREDICTED_DOWNTIME_H');
  await page.waitForTimeout(2500);
  await shoot(page, '04-control-room-risk');

  // The ACT-40 preset delays the same purchase-order line by another 10 days. Running it on
  // top of the manual change above would compound the two and show numbers that match neither
  // the documentation nor the end-to-end assertions (36 h → 8 h), so start from clean data.
  log('resetting before the What-If so its numbers match the documented scenario');
  await resetDemo();
  await page.waitForTimeout(1000);

  // --- 5. What-If, Before / After ------------------------------------------
  await page.goto(`${WEB}/planning`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'scenario-tile-DELAY_ACT40_10D');
  await page.getByTestId('scenario-tile-DELAY_ACT40_10D').click();
  await page.locator('[data-testid="scenario-status"]').waitFor({ state: 'visible', timeout: 60_000 });
  await page.waitForFunction(
    () => /Completed|Zakończ/i.test(document.querySelector('[data-testid="scenario-status"]')?.textContent ?? ''),
    null,
    { timeout: 60_000 },
  );
  await page.waitForTimeout(2000);
  await shoot(page, '05-whatif-before-after', { full: true });

  // --- 6. genealogy ---------------------------------------------------------
  await page.goto(`${WEB}/trace/serials/PMV-2026-0007`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'genealogy-tree');
  // Open the first two levels so the chain supplier → lot → certificate is visible.
  for (const toggle of await page.locator('[data-testid^="trace-toggle-"]').all()) {
    if (await toggle.getAttribute('aria-expanded') === 'false') await toggle.click().catch(() => {});
  }
  await page.waitForTimeout(1500);
  await shoot(page, '06-genealogy', { full: true });

  // --- 7. lot with trace-forward -------------------------------------------
  await page.goto(`${WEB}/trace/lots/HTS-22-2608`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'lot-page');
  await page.waitForTimeout(1500);
  await shoot(page, '07-lot-trace-forward', { full: true });

  // --- 8. the passport ------------------------------------------------------
  await page.goto(`${WEB}/passports/PMV-2026-0007`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'passport-page');
  await page.waitForTimeout(1500);
  await shoot(page, '08-passport', { full: true });

  // --- 8b. the closing value screen ----------------------------------------
  await page.goto(`${WEB}/demo/summary`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  await shoot(page, '16-value-summary', { full: true });
  await ctx.close();

  // --- 12. phone ------------------------------------------------------------
  ctx = await newContext(browser, PHONE, 'dark', { deviceScaleFactor: 2, hasTouch: true, isMobile: true });
  page = await ctx.newPage();
  await page.goto(`${WEB}/`, { waitUntil: 'domcontentloaded' });
  await ready(page, 'kpi-MATERIAL_READINESS');
  await page.waitForTimeout(2500);
  await shoot(page, '12-mobile-control-room', { target: PHONE.width });

  await page.goto(`${WEB}/passports`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);
  await shoot(page, '13-mobile-passports', { target: PHONE.width });

  await page.getByTestId('nav-toggle').click();
  await page.locator('[data-testid="nav-drawer"]').waitFor({ state: 'visible', timeout: 10_000 });
  await page.waitForTimeout(800);
  await shoot(page, '14-mobile-nav', { target: PHONE.width });
  await ctx.close();

  await browser.close();

  // --- 9. the generated PDF, first page ------------------------------------
  log('rendering the passport PDF');
  const pdf = Buffer.from(await (await api('QualityInspector', '/api/v1/passports/PMV-2026-0007/versions/1/pdf')).arrayBuffer());
  const pdfPath = path.join(OUT, 'passport.pdf');
  await writeFile(pdfPath, pdf);
  await execFileAsync('pdftoppm', ['-png', '-r', '96', '-f', '1', '-l', '1', pdfPath, path.join(OUT, 'tmp-pdf')]);
  for (const f of await readdir(OUT)) {
    if (f.startsWith('tmp-pdf')) {
      const dst = path.join(OUT, '15-passport-pdf.png');
      await rename(path.join(OUT, f), dst);
      await optimise(dst, 900);
      const { size } = await stat(dst);
      log(`15-passport-pdf.png  ${(size / 1024).toFixed(0)} kB`);
    }
  }
  await unlink(pdfPath);

  log('restoring the demonstration data');
  await resetDemo();

  const files = (await readdir(OUT)).filter((f) => f.endsWith('.png'));
  let total = 0;
  for (const f of files) total += (await stat(path.join(OUT, f))).size;
  log(`${files.length} screenshots, ${(total / 1024 / 1024).toFixed(2)} MB total`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
