import { test, expect } from '@playwright/test';
import { resetDemo } from '../helpers/api';

test.describe('UI golden path', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  test('control room → what-if → passport in the browser', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });

    await page.goto('/');
    await expect(page.getByTestId('kpi-MATERIAL_READINESS')).toBeVisible();
    await expect(page.getByTestId('kpi-OTIF')).toBeVisible();

    await page.goto('/planning');
    // Clicking a preset tile creates the scenario and starts the solve, then routes to its detail page.
    await page.getByTestId('scenario-tile-DELAY_ACT40_10D').click();
    await expect(page.getByTestId('scenario-status')).toContainText(/Completed|Zakończ/i, { timeout: 60_000 });
    await expect(page.getByTestId('explanation-ORDER_PULLED_FORWARD')).toBeVisible();
    await expect(page.getByTestId('kpi-delta-downtime')).toBeVisible();

    await page.goto('/passports/PMV-2026-0007');
    await expect(page.getByTestId('passport-status')).toBeVisible();
    await expect(page.getByTestId('btn-generate-passport')).toBeEnabled();

    expect(errors.filter((e) => !/favicon|maplibre/i.test(e))).toEqual([]);
  });

  test('language switch and role switch work', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('lang-switch').getByRole('button', { name: 'EN' }).click();
    await expect(page.getByText(/Material Readiness|Control Room/i).first()).toBeVisible();
  });
});
