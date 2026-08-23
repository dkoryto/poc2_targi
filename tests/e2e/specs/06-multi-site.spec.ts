import { test, expect } from '@playwright/test';
import { apiAs, codesFor, kpisFor, PLANTS, type SiteCode } from '../helpers/api';

const CODES = Object.keys(PLANTS) as SiteCode[];

test.describe('four plants are isolated from each other', () => {
  test('GET /sites returns the four documented plants', async () => {
    const api = await apiAs('DemoPresenter');
    const sites = await (await api.get('/api/v1/sites')).json();
    expect(sites).toHaveLength(4);

    for (const code of CODES) {
      const site = sites.find((s: { code: string }) => s.code === code);
      expect(site, `plant ${code}`).toBeTruthy();
      const expected = PLANTS[code];
      expect(site.name).toBe(expected.name);
      expect(site.city).toBe(expected.city);
      expect(site.lat).toBeCloseTo(expected.lat, 2);
      expect(site.lon).toBeCloseTo(expected.lon, 2);
      expect(site.featuredScenarioKey).toBe(expected.featured);
    }
    // Exactly one plant is the default landing plant.
    expect(sites.filter((s: { isDefault: boolean }) => s.isDefault)).toHaveLength(1);
    expect(sites.find((s: { isDefault: boolean }) => s.isDefault).code).toBe('SITE-01');
    await api.dispose();
  });

  test('each plant offers exactly one featured scenario, matching the contract', async () => {
    const api = await apiAs('ProductionPlanner');
    for (const code of CODES) {
      const presets = await (await api.get(`/api/v1/planning/scenarios/presets?siteCode=${code}`)).json();
      const items = Array.isArray(presets) ? presets : presets.items;
      const featured = items.filter((p: { featured?: boolean }) => p.featured);
      expect(featured, `${code} featured count`).toHaveLength(1);
      expect(featured[0].key, `${code} featured key`).toBe(PLANTS[code].featured);
    }
    await api.dispose();
  });

  test('KPIs are computed per plant and differ between plants', async () => {
    const api = await apiAs('OperationsDirector');
    const perPlant = Object.fromEntries(
      await Promise.all(CODES.map(async (c) => [c, await kpisFor(api, c)] as const)),
    );
    for (const code of CODES) {
      expect(Object.keys(perPlant[code]).length, `${code} KPI count`).toBeGreaterThanOrEqual(6);
    }
    // OTIF is derived from each plant's own delivery history, so the plants must not all agree.
    const otif = CODES.map((c) => perPlant[c].OTIF);
    expect(new Set(otif).size, `OTIF per plant: ${otif.join(', ')}`).toBeGreaterThan(1);
    await api.dispose();
  });

  test('no purchase order, lot, work center or order leaks between plants', async () => {
    const api = await apiAs('ProductionPlanner');
    const codes = Object.fromEntries(
      await Promise.all(CODES.map(async (c) => [c, await codesFor(api, c)] as const)),
    ) as Record<SiteCode, Awaited<ReturnType<typeof codesFor>>>;

    for (const a of CODES) {
      for (const b of CODES) {
        if (a === b) continue;
        for (const field of ['purchaseOrders', 'lots', 'workCenters', 'orders'] as const) {
          const overlap = codes[a][field].filter((x) => codes[b][field].includes(x));
          expect(overlap, `${field} shared by ${a} and ${b}`).toEqual([]);
        }
      }
    }

    // Concrete anchors from the documented code namespaces.
    expect(codes['SITE-01'].workCenters).toContain('WC-CUT');
    expect(codes['SITE-02'].workCenters.every((w) => w.startsWith('PIL-'))).toBeTruthy();
    expect(codes['SITE-03'].workCenters.every((w) => w.startsWith('ZAM-'))).toBeTruthy();
    expect(codes['SITE-04'].workCenters.every((w) => w.startsWith('LES-'))).toBeTruthy();
    expect(codes['SITE-01'].purchaseOrders).toContain('PO-2026-0007');
    expect(codes['SITE-02'].purchaseOrders.every((p) => p.startsWith('PO-2026-1'))).toBeTruthy();
    expect(codes['SITE-03'].lots).toContain('HTS-22-3110');
    expect(codes['SITE-01'].lots).toContain('HTS-22-2608');
    await api.dispose();
  });

  test('an unknown plant is 404 across every scoped endpoint', async () => {
    const api = await apiAs('ProductionPlanner');
    const endpoints = [
      'dashboard/kpis', 'dashboard/map', 'dashboard/quality-status', 'dashboard/plan',
      'purchase-orders', 'lots', 'shipments', 'passports',
      'planning/baseline', 'planning/scenarios/presets',
    ];
    for (const e of endpoints) {
      const res = await api.get(`/api/v1/${e}?siteCode=SITE-99`);
      expect(res.status(), `${e} with unknown plant`).toBe(404);
    }
    await api.dispose();
  });

  test('a supplier reaches only the plants it supplies', async () => {
    // SUP-01 supplies Kielce and Zamość; Piła and Leszno are out of reach.
    const supplier = await apiAs('SupplierUser', 'SUP-01');
    const me = await (await supplier.get('/api/v1/auth/me')).json();
    expect(me.availableSites).toEqual(expect.arrayContaining(['SITE-01', 'SITE-03']));
    expect(me.availableSites).not.toContain('SITE-02');

    for (const reachable of ['SITE-01', 'SITE-03']) {
      expect((await supplier.get(`/api/v1/purchase-orders?siteCode=${reachable}`)).status()).toBe(200);
    }
    for (const denied of ['SITE-02', 'SITE-04']) {
      expect((await supplier.get(`/api/v1/purchase-orders?siteCode=${denied}`)).status(), `${denied} must be denied`).toBe(403);
    }
    await supplier.dispose();
  });

  test('a scenario may not span two plants', async () => {
    const api = await apiAs('ProductionPlanner');
    const res = await api.post('/api/v1/planning/scenarios', {
      data: {
        name: 'cross-plant',
        changes: [
          { type: 'CAPACITY', workCenterCode: 'WC-INT', factor: 0.5 },
          { type: 'CAPACITY', workCenterCode: 'PIL-WC-INT', factor: 0.5 },
        ],
      },
    });
    expect(res.status()).toBe(400);
    const problem = await res.json();
    expect(JSON.stringify(problem)).toMatch(/one plant|jeden zakład/i);
    await api.dispose();
  });

  test('each plant keeps its own planning baseline', async () => {
    const api = await apiAs('ProductionPlanner');
    const baselines = await Promise.all(
      CODES.map(async (c) => (await api.get(`/api/v1/planning/baseline?siteCode=${c}`)).json()),
    );
    const ids = baselines.map((b) => b.id);
    expect(new Set(ids).size, 'each plant needs a distinct baseline').toBe(4);
    await api.dispose();
  });
});
