import { test, expect, type Page } from '@playwright/test';
import { PLANTS, resetDemo } from '../helpers/api';

/** Console errors worth failing on — the SignalR negotiation race is pre-existing noise. */
function collectErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (m) => {
    if (m.type() !== 'error') return;
    const text = m.text();
    if (/favicon|maplibre|negotiation|connection was stopped|Failed to start the connection/i.test(text)) return;
    errors.push(text);
  });
  return errors;
}

async function switchPlant(page: Page, code: keyof typeof PLANTS) {
  await page.getByTestId('site-switch').click();
  await page.getByTestId(`site-option-${code}`).click();
  await expect(page.getByTestId('site-switch')).toContainText(PLANTS[code].city);
}

test.describe('plant switching in the browser', () => {
  test.beforeAll(async () => { await resetDemo(); });

  test('the switcher offers all four plants', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('kpi-MATERIAL_READINESS')).toBeVisible();
    await page.getByTestId('site-switch').click();
    for (const code of Object.keys(PLANTS) as (keyof typeof PLANTS)[]) {
      await expect(page.getByTestId(`site-option-${code}`)).toContainText(PLANTS[code].city);
    }
    await page.keyboard.press('Escape');
  });

  test('switching Kielce to Piła re-scopes the KPIs and the Gantt', async ({ page }) => {
    const errors = collectErrors(page);
    await page.goto('/');
    await expect(page.getByTestId('site-switch')).toContainText('Kielce');

    const gantt = page.getByTestId('gantt');
    await expect(gantt).toContainText('WC-CUT');
    const otifBefore = await page.getByTestId('kpi-OTIF').innerText();

    await switchPlant(page, 'SITE-02');

    // Piła runs an electronics line: its own work centers appear and Kielce's disappear.
    await expect(gantt).toContainText('PIL-WC-ELEC');
    await expect(gantt).not.toContainText('WC-CUT');
    await expect(page.getByTestId('kpi-OTIF')).not.toHaveText(otifBefore);

    expect(errors).toEqual([]);
  });

  test('the chosen plant survives a reload', async ({ page }) => {
    await page.goto('/');
    await switchPlant(page, 'SITE-03');
    await page.reload();
    await expect(page.getByTestId('site-switch')).toContainText('Zamość');
    await expect(page.getByTestId('gantt')).toContainText('ZAM-WC-');
  });

  test('each plant highlights its own featured scenario first', async ({ page }) => {
    await page.goto('/planning');
    // Kielce leads with the ACT-40 delay.
    const kielceTiles = page.getByTestId(/^scenario-tile-/);
    await expect(kielceTiles.first()).toHaveAttribute('data-testid', `scenario-tile-${PLANTS['SITE-01'].featured}`);

    await switchPlant(page, 'SITE-04');
    await expect(page.getByTestId(`scenario-tile-${PLANTS['SITE-04'].featured}`)).toBeVisible();
    const lesznoTiles = page.getByTestId(/^scenario-tile-/);
    await expect(lesznoTiles.first()).toHaveAttribute('data-testid', `scenario-tile-${PLANTS['SITE-04'].featured}`);
    // Kielce's ACT-40 preset belongs to another plant and must not be offered here.
    await expect(page.getByTestId(`scenario-tile-${PLANTS['SITE-01'].featured}`)).toHaveCount(0);
  });

  test('a plant scenario runs end to end from its own dashboard', async ({ page }) => {
    const errors = collectErrors(page);
    await page.goto('/planning');
    await switchPlant(page, 'SITE-02');
    await page.getByTestId(`scenario-tile-${PLANTS['SITE-02'].featured}`).click();
    await expect(page.getByTestId('scenario-status')).toContainText(/Completed|Zakończ/i, { timeout: 60_000 });
    await expect(page.getByTestId('explanation-ORDER_DELAYED_MATERIAL_SHORTAGE')).toBeVisible();
    expect(errors).toEqual([]);
  });
});
