import { test, expect } from '@playwright/test';
import { apiAs, resetDemo } from '../helpers/api';

async function runPreset(key: string) {
  const planner = await apiAs('ProductionPlanner');
  const presets = await (await planner.get('/api/v1/planning/scenarios/presets')).json();
  const preset = (Array.isArray(presets) ? presets : presets.items).find((p: { key: string }) => p.key === key);
  expect(preset, `preset ${key}`).toBeTruthy();
  const created = await (await planner.post('/api/v1/planning/scenarios', {
    data: { name: preset.key, changes: preset.changes },
  })).json();
  const started = Date.now();
  await planner.post(`/api/v1/planning/scenarios/${created.id}/run`);
  let scenario: Record<string, unknown> = {};
  for (let i = 0; i < 30; i++) {
    scenario = await (await planner.get(`/api/v1/planning/scenarios/${created.id}`)).json();
    if (scenario.status === 'Completed' || scenario.status === 'Failed') break;
    await new Promise((r) => setTimeout(r, 500));
  }
  return { planner, scenario, elapsed: Date.now() - started };
}

test.describe('What-If re-scheduling', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  test('ACT-40 +10 days returns a Before/After plan within 3 s and recommends WO-2026-019', async () => {
    const { planner, scenario, elapsed } = await runPreset('DELAY_ACT40_10D');
    expect(scenario.status).toBe('Completed');
    expect(elapsed).toBeLessThan(15_000);
    expect(scenario.elapsedMs as number).toBeLessThan(3000);
    expect(scenario.before).toBeTruthy();
    expect(scenario.after).toBeTruthy();

    const kpiBefore = scenario.kpiBefore as { downtimeHours: number };
    const kpiAfter = scenario.kpiAfter as { downtimeHours: number };
    expect(kpiBefore.downtimeHours).toBe(36);
    expect(kpiAfter.downtimeHours).toBe(8);

    const explanations = scenario.explanations as { reasonCode: string; orderCode: string }[];
    expect(explanations.some((e) => e.reasonCode === 'ORDER_DELAYED_MATERIAL_SHORTAGE' && e.orderCode === 'WO-2026-014')).toBeTruthy();
    expect(explanations.some((e) => e.reasonCode === 'ORDER_PULLED_FORWARD' && e.orderCode === 'WO-2026-019')).toBeTruthy();
    await planner.dispose();
  });

  test('approval creates a new baseline version and an audit entry', async () => {
    const planner = await apiAs('ProductionPlanner');
    const baselineBefore = await (await planner.get('/api/v1/planning/baseline')).json();
    const { scenario } = await runPreset('DELAY_ACT40_10D');
    const approve = await planner.post(`/api/v1/planning/scenarios/${scenario.id}/approve`);
    expect(approve.ok()).toBeTruthy();
    const baselineAfter = await (await planner.get('/api/v1/planning/baseline')).json();
    expect(baselineAfter.version).toBeGreaterThan(baselineBefore.version);

    const auditor = await apiAs('Auditor');
    const audit = await (await auditor.get('/api/v1/audit?entity=PlanningBaseline')).json();
    expect(audit.items.length).toBeGreaterThan(0);
    await auditor.dispose();
    await planner.dispose();
  });

  test('scenarios never mutate the baseline before approval', async () => {
    const planner = await apiAs('ProductionPlanner');
    const before = await (await planner.get('/api/v1/planning/baseline')).json();
    await runPreset('CAPACITY_INT_50');
    const after = await (await planner.get('/api/v1/planning/baseline')).json();
    expect(after.version).toBe(before.version);
    await planner.dispose();
  });
});
