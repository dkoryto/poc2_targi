/**
 * Every scenario tile a presenter can click must do something the audience can see.
 * A tile that always reports "no change" is a dead moment on the stand.
 */
import { test, expect } from '@playwright/test';
import { apiAs, resetDemo, PLANTS } from '../helpers/api';

async function runPreset(plant: string, key: string) {
  const planner = await apiAs('ProductionPlanner');
  const presets = await (await planner.get(`/api/v1/planning/scenarios/presets?siteCode=${plant}`)).json();
  const list = Array.isArray(presets) ? presets : presets.items;
  const preset = list.find((p: { key: string }) => p.key === key);
  if (!preset) { await planner.dispose(); return null; }
  const created = await (await planner.post(`/api/v1/planning/scenarios?siteCode=${plant}`, {
    data: { name: `qa-${key}`, changes: preset.changes },
  })).json();
  await planner.post(`/api/v1/planning/scenarios/${created.id}/run`);
  let scenario: Record<string, any> = {};
  for (let i = 0; i < 40; i++) {
    scenario = await (await planner.get(`/api/v1/planning/scenarios/${created.id}`)).json();
    if (scenario.status === 'Completed' || scenario.status === 'Failed') break;
    await new Promise((r) => setTimeout(r, 400));
  }
  await planner.dispose();
  return scenario;
}

test.describe('scenario tiles produce a visible effect', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  for (const plant of Object.keys(PLANTS)) {
    test(`the priority preset changes something on ${plant}`, async () => {
      const scenario = await runPreset(plant, 'PRIORITY_WO014');
      test.skip(!scenario, 'preset not offered on this plant');
      expect(scenario!.status).toBe('Completed');
      const reasons = (scenario!.explanations ?? []).map((e: { reasonCode: string }) => e.reasonCode);
      // Raising an order's priority should reorder something, or the tile should not be offered.
      expect(reasons, `only ${JSON.stringify(reasons)}`).not.toEqual(['NO_CHANGE']);
    });
  }

  test('the lot-block preset has an effect on Kielce, where the tile is offered', async () => {
    const scenario = await runPreset('SITE-01', 'BLOCK_LOT_HTS22');
    test.skip(!scenario, 'preset not offered on this plant');
    const reasons = (scenario!.explanations ?? []).map((e: { reasonCode: string }) => e.reasonCode);
    expect(reasons, `only ${JSON.stringify(reasons)}`).not.toEqual(['NO_CHANGE']);
  });
});

test('every plant has inbound shipments to show', async () => {
  const coord = await apiAs('InboundCoordinator');
  const empty: string[] = [];
  for (const p of Object.keys(PLANTS)) {
    const res = await (await coord.get(`/api/v1/shipments?siteCode=${p}`)).json();
    const n = res.total ?? (res.items ?? res).length;
    if (!n) empty.push(p);
  }
  await coord.dispose();
  expect(empty, 'plants whose inbound screen is empty').toEqual([]);
});
