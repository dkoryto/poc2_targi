import { request, type APIRequestContext } from '@playwright/test';
import { API } from '../playwright.config';

export type Role =
  | 'DemoPresenter' | 'ProductionPlanner' | 'InboundCoordinator' | 'QualityInspector'
  | 'OperationsDirector' | 'Auditor' | 'Administrator' | 'SupplierUser';

export async function apiAs(role: Role, supplierCode?: string): Promise<APIRequestContext> {
  const anon = await request.newContext({ baseURL: API });
  const q = supplierCode ? `&supplierCode=${supplierCode}` : '';
  const res = await anon.get(`/api/v1/auth/demo-login?role=${role}${q}`);
  if (!res.ok()) throw new Error(`demo-login failed: ${res.status()} ${await res.text()}`);
  const { accessToken } = await res.json();
  await anon.dispose();
  return request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` } });
}

export async function resetDemo(): Promise<void> {
  const api = await apiAs('DemoPresenter');
  try {
    // The reset endpoint is rate limited; a full suite run legitimately exceeds a low budget,
    // so back off and retry rather than failing the spec on a 429.
    for (let attempt = 0; attempt < 5; attempt++) {
      const res = await api.post('/api/v1/demo/reset');
      if (res.ok()) return;
      if (res.status() !== 429) throw new Error(`reset failed: ${res.status()}`);
      await new Promise((r) => setTimeout(r, 3000));
    }
    throw new Error('reset failed: still rate limited after 5 attempts');
  } finally {
    await api.dispose();
  }
}

export async function kpi(code: string): Promise<number> {
  const api = await apiAs('OperationsDirector');
  const res = await api.get('/api/v1/dashboard/kpis');
  const body = await res.json();
  await api.dispose();
  const item = body.items.find((k: { code: string }) => k.code === code);
  if (!item) throw new Error(`KPI ${code} missing`);
  return item.value as number;
}

// ---------------------------------------------------------------- multi-plant

export const PLANTS = {
  'SITE-01': { name: 'Zakład Kielce', city: 'Kielce', lat: 50.87, lon: 20.63, featured: 'DELAY_ACT40_10D' },
  'SITE-02': { name: 'Zakład Piła', city: 'Piła', lat: 53.15, lon: 16.74, featured: 'DELAY_MCUX7_14D' },
  'SITE-03': { name: 'Zakład Zamość', city: 'Zamość', lat: 50.72, lon: 23.25, featured: 'BLOCK_LOT_HTS22' },
  'SITE-04': { name: 'Zakład Leszno', city: 'Leszno', lat: 51.84, lon: 16.58, featured: 'CAPACITY_INT_50' },
} as const;

export type SiteCode = keyof typeof PLANTS;

export interface Preset { key: string; titleKey?: string; featured?: boolean; changes: unknown[] }

export interface ScenarioResult {
  id: string;
  status: string;
  solver?: string;
  elapsedMs?: number;
  before?: { orders?: OrderResult[]; operations?: { changed?: boolean }[] };
  after?: { orders?: OrderResult[]; operations?: { changed?: boolean }[] };
  kpiBefore?: PlanKpi;
  kpiAfter?: PlanKpi;
  explanations?: { reasonCode: string; orderCode?: string; params?: Record<string, unknown> }[];
}

export interface OrderResult { code: string; latenessDays: number; materialComplete: boolean; riskFlag?: string }
export interface PlanKpi { downtimeHours: number; lateOrders: number; totalLatenessDays: number; movedOperations: number }

/** Lists a plant's scenario presets. */
export async function presetsFor(api: APIRequestContext, siteCode: SiteCode): Promise<Preset[]> {
  const res = await api.get(`/api/v1/planning/scenarios/presets?siteCode=${siteCode}`);
  if (!res.ok()) throw new Error(`presets ${siteCode} failed: ${res.status()}`);
  const body = await res.json();
  return Array.isArray(body) ? body : body.items;
}

/** Creates a scenario from a preset, runs it and polls until it settles. */
export async function runPresetFor(
  api: APIRequestContext,
  siteCode: SiteCode,
  key: string,
): Promise<{ scenario: ScenarioResult; wallClockMs: number }> {
  const preset = (await presetsFor(api, siteCode)).find((p) => p.key === key);
  if (!preset) throw new Error(`preset ${key} not offered for ${siteCode}`);
  const created = await (await api.post('/api/v1/planning/scenarios', {
    data: { name: `${siteCode} ${key}`, changes: preset.changes },
  })).json();
  const started = Date.now();
  await api.post(`/api/v1/planning/scenarios/${created.id}/run`);
  let scenario = created as ScenarioResult;
  for (let i = 0; i < 40; i++) {
    scenario = await (await api.get(`/api/v1/planning/scenarios/${created.id}`)).json();
    if (scenario.status === 'Completed' || scenario.status === 'Failed') break;
    await new Promise((r) => setTimeout(r, 400));
  }
  return { scenario, wallClockMs: Date.now() - started };
}

/** All KPI values for one plant, keyed by code. */
export async function kpisFor(api: APIRequestContext, siteCode: SiteCode): Promise<Record<string, number>> {
  const res = await api.get(`/api/v1/dashboard/kpis?siteCode=${siteCode}`);
  if (!res.ok()) throw new Error(`kpis ${siteCode} failed: ${res.status()}`);
  const body = await res.json();
  return Object.fromEntries(body.items.map((k: { code: string; value: number }) => [k.code, k.value]));
}

/** Business codes visible to a plant, for leakage assertions. */
export async function codesFor(api: APIRequestContext, siteCode: SiteCode) {
  const [pos, lots, baseline] = await Promise.all([
    api.get(`/api/v1/purchase-orders?siteCode=${siteCode}`).then((r) => r.json()),
    api.get(`/api/v1/lots?siteCode=${siteCode}`).then((r) => r.json()),
    api.get(`/api/v1/planning/baseline?siteCode=${siteCode}`).then((r) => r.json()),
  ]);
  const lotItems = Array.isArray(lots) ? lots : lots.items;
  return {
    purchaseOrders: pos.items.map((p: { code: string }) => p.code) as string[],
    lots: lotItems.map((l: { lotNumber: string }) => l.lotNumber) as string[],
    workCenters: baseline.gantt.workCenters.map((w: { code: string }) => w.code) as string[],
    orders: baseline.gantt.orders.map((o: { code: string }) => o.code) as string[],
    baselineVersion: baseline.version as number,
  };
}
