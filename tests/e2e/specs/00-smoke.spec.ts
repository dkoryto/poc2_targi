import { test, expect } from '@playwright/test';
import { apiAs, resetDemo } from '../helpers/api';

test.describe('@smoke stack is up', () => {
  test.beforeAll(async () => { await resetDemo(); });

  test('api health and seeded baseline', async () => {
    const api = await apiAs('ProductionPlanner');
    expect((await api.get('/health/ready')).ok()).toBeTruthy();
    const baseline = await (await api.get('/api/v1/planning/baseline')).json();
    expect(JSON.stringify(baseline)).toContain('WO-2026-014');
    await api.dispose();
  });

  test('web renders control room with computed KPIs', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('Defense Supply & Production Control')).toBeVisible();
    await expect(page.getByTestId('kpi-MATERIAL_READINESS')).toBeVisible();
    await expect(page.getByTestId('kpi-HIGH_RISK_DELIVERIES')).toContainText('3');
  });
});
