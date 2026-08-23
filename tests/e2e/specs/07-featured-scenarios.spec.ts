import { test, expect } from '@playwright/test';
import { apiAs, codesFor, PLANTS, resetDemo, runPresetFor, type ScenarioResult, type SiteCode } from '../helpers/api';

/** Explanations carrying a given reason code, optionally for one order. */
function reasons(scenario: ScenarioResult, code: string, orderCode?: string) {
  return (scenario.explanations ?? []).filter(
    (e) => e.reasonCode === code && (orderCode === undefined || e.orderCode === orderCode),
  );
}

function order(scenario: ScenarioResult, code: string) {
  const found = (scenario.after?.orders ?? []).find((o) => o.code === code);
  if (!found) throw new Error(`order ${code} missing from the proposed plan`);
  return found;
}

test.describe('every plant tells its own story', () => {
  test.beforeAll(async () => { await resetDemo(); });
  test.afterAll(async () => { await resetDemo(); });

  test('Kielce: delaying ACT-40 by 10 days is absorbed by pulling WO-2026-019 forward', async () => {
    const api = await apiAs('ProductionPlanner');
    const { scenario, wallClockMs } = await runPresetFor(api, 'SITE-01', PLANTS['SITE-01'].featured);

    expect(scenario.status).toBe('Completed');
    expect(scenario.elapsedMs!).toBeLessThan(3000);
    expect(wallClockMs).toBeLessThan(20_000);
    expect(scenario.kpiBefore!.downtimeHours).toBe(36);
    expect(scenario.kpiAfter!.downtimeHours).toBe(8);
    expect(order(scenario, 'WO-2026-014').latenessDays).toBe(4);

    const shortage = reasons(scenario, 'ORDER_DELAYED_MATERIAL_SHORTAGE', 'WO-2026-014');
    expect(shortage).toHaveLength(1);
    expect(shortage[0].params!.partCode).toBe('ACT-40');
    expect(Number(shortage[0].params!.missingQty)).toBe(8);
    expect(reasons(scenario, 'ORDER_PULLED_FORWARD', 'WO-2026-019')).toHaveLength(1);
    await api.dispose();
  });

  test('Piła: delaying MCU-X7 by 14 days makes an electronics order late', async () => {
    const api = await apiAs('ProductionPlanner');
    const { scenario } = await runPresetFor(api, 'SITE-02', PLANTS['SITE-02'].featured);

    expect(scenario.status).toBe('Completed');
    expect(scenario.elapsedMs!).toBeLessThan(3000);

    const late = order(scenario, 'WO-2026-102');
    expect(late.latenessDays).toBeGreaterThanOrEqual(5);
    expect(late.materialComplete).toBe(false);
    expect(late.riskFlag).toBe('critical');

    const shortage = reasons(scenario, 'ORDER_DELAYED_MATERIAL_SHORTAGE', 'WO-2026-102');
    expect(shortage).toHaveLength(1);
    expect(shortage[0].params!.partCode).toBe('MCU-X7');
    expect(reasons(scenario, 'ORDER_LATE_DUE', 'WO-2026-102')).toHaveLength(1);
    // The shortage propagates as a material-coverage warning without pushing the rest of the plan late.
    expect(order(scenario, 'WO-2026-103').riskFlag).toBe('warn');
    await api.dispose();
  });

  test('Zamość: blocking its steel lot starves the structures orders', async () => {
    const api = await apiAs('ProductionPlanner');
    const { scenario } = await runPresetFor(api, 'SITE-03', PLANTS['SITE-03'].featured);

    expect(scenario.status).toBe('Completed');
    expect(scenario.elapsedMs!).toBeLessThan(3000);
    expect(scenario.kpiAfter!.downtimeHours).toBeLessThan(scenario.kpiBefore!.downtimeHours);

    const starved = reasons(scenario, 'ORDER_DELAYED_MATERIAL_SHORTAGE');
    expect(starved.length).toBeGreaterThanOrEqual(2);
    expect(starved.every((e) => e.params!.partCode === 'HTS-22')).toBeTruthy();
    expect(starved.map((e) => e.orderCode)).toEqual(expect.arrayContaining(['WO-2026-204', 'WO-2026-205']));
    await api.dispose();
  });

  test('Leszno: halving the integration cell costs three weeks of lateness', async () => {
    const api = await apiAs('ProductionPlanner');
    const { scenario } = await runPresetFor(api, 'SITE-04', PLANTS['SITE-04'].featured);

    expect(scenario.status).toBe('Completed');
    expect(scenario.elapsedMs!).toBeLessThan(3000);

    // A hard capacity crunch makes the plan worse, not better — that is the point of this story.
    expect(scenario.kpiAfter!.totalLatenessDays).toBeGreaterThan(scenario.kpiBefore!.totalLatenessDays);
    expect(scenario.kpiAfter!.totalLatenessDays).toBeGreaterThanOrEqual(14);
    expect(scenario.kpiAfter!.lateOrders).toBeGreaterThanOrEqual(3);

    const capacity = reasons(scenario, 'CAPACITY_REDUCED');
    expect(capacity).toHaveLength(1);
    expect(capacity[0].params!.workCenterCode).toBe('LES-WC-INT');
    expect(Number(capacity[0].params!.factor)).toBe(0.5);
    await api.dispose();
  });

  test('the moved-operation count agrees with the proposed plan', async () => {
    const api = await apiAs('ProductionPlanner');
    for (const code of ['SITE-01', 'SITE-02', 'SITE-04'] as SiteCode[]) {
      const { scenario } = await runPresetFor(api, code, PLANTS[code].featured);
      const changed = (scenario.after?.operations ?? []).filter((o) => o.changed).length;
      expect(scenario.kpiAfter!.movedOperations, `${code} moved-op KPI vs plan`).toBe(changed);
    }
    await api.dispose();
  });

  test('approving one plant leaves the other plants untouched', async () => {
    const api = await apiAs('ProductionPlanner');
    const codes = ['SITE-01', 'SITE-02', 'SITE-03', 'SITE-04'] as SiteCode[];
    const before = Object.fromEntries(
      await Promise.all(codes.map(async (c) => [c, (await codesFor(api, c)).baselineVersion] as const)),
    ) as Record<SiteCode, number>;

    const { scenario } = await runPresetFor(api, 'SITE-02', PLANTS['SITE-02'].featured);
    expect((await api.post(`/api/v1/planning/scenarios/${scenario.id}/approve`)).ok()).toBeTruthy();

    const after = Object.fromEntries(
      await Promise.all(codes.map(async (c) => [c, (await codesFor(api, c)).baselineVersion] as const)),
    ) as Record<SiteCode, number>;

    expect(after['SITE-02'], 'the approved plant advances').toBeGreaterThan(before['SITE-02']);
    for (const other of ['SITE-01', 'SITE-03', 'SITE-04'] as SiteCode[]) {
      expect(after[other], `${other} must be untouched`).toBe(before[other]);
    }
    await api.dispose();
    // This suite approved a plan; restore the seeded baselines for whatever runs next.
    await resetDemo();
  });
});
