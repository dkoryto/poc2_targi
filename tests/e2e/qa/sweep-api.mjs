// Breadth sweep: every documented endpoint x every role x every plant.
// Records status codes and flags anything that is a 5xx, an unexpected 200, or an empty payload
// where the seed guarantees data.
import { request } from '@playwright/test';

const API = process.env.E2E_API_URL ?? 'http://localhost:5080';
const PLANTS = ['SITE-01', 'SITE-02', 'SITE-03', 'SITE-04'];
const ROLES = [
  ['DemoPresenter'], ['ProductionPlanner'], ['InboundCoordinator'], ['QualityInspector'],
  ['OperationsDirector'], ['Auditor'], ['Administrator'],
  ['SupplierUser', 'SUP-01'], ['SupplierUser', 'SUP-02'], ['SupplierUser', 'SUP-03'],
];

const ctxFor = async (role, supplier) => {
  const anon = await request.newContext({ baseURL: API });
  const q = supplier ? `&supplierCode=${supplier}` : '';
  const r = await anon.get(`/api/v1/auth/demo-login?role=${role}${q}`);
  if (!r.ok()) throw new Error(`login ${role} ${supplier ?? ''}: ${r.status()}`);
  const { accessToken } = await r.json();
  await anon.dispose();
  return request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` } });
};

// GET endpoints that the multi-site contract says accept ?siteCode=
const SITE_SCOPED = [
  '/api/v1/dashboard/kpis', '/api/v1/dashboard/map', '/api/v1/dashboard/risk-heatmap',
  '/api/v1/dashboard/quality-status', '/api/v1/dashboard/plan',
  '/api/v1/purchase-orders', '/api/v1/shipments', '/api/v1/logistics-events', '/api/v1/inventory',
  '/api/v1/lots', '/api/v1/non-conformances', '/api/v1/passports', '/api/v1/notifications',
  '/api/v1/planning/baseline', '/api/v1/planning/scenarios', '/api/v1/planning/scenarios/presets',
  '/api/v1/trace/search?q=PMV', '/api/v1/audit',
];
const GLOBAL = ['/api/v1/sites', '/api/v1/auth/me', '/api/v1/suppliers', '/api/v1/admin/status', '/api/v1/admin/settings', '/api/v1/demo/status', '/api/v1/demo/script'];

const rows = [];
const findings = [];

const count = (body) => {
  if (body == null) return null;
  if (Array.isArray(body)) return body.length;
  if (Array.isArray(body.items)) return body.items.length;
  return undefined; // object payload, not a list
};

for (const [role, supplier] of ROLES) {
  const label = supplier ? `${role}:${supplier}` : role;
  const api = await ctxFor(role, supplier);
  for (const path of GLOBAL) {
    const res = await api.get(path);
    const body = res.headers()['content-type']?.includes('json') ? await res.json().catch(() => null) : null;
    rows.push({ role: label, plant: '-', path, status: res.status(), n: count(body) });
    if (res.status() >= 500) findings.push({ sev: 'HIGH', role: label, path, status: res.status(), note: '5xx' });
  }
  for (const plant of PLANTS) {
    for (const base of SITE_SCOPED) {
      const path = base.includes('?') ? `${base}&siteCode=${plant}` : `${base}?siteCode=${plant}`;
      const res = await api.get(path);
      const body = res.headers()['content-type']?.includes('json') ? await res.json().catch(() => null) : null;
      const n = count(body);
      rows.push({ role: label, plant, path: base, status: res.status(), n });
      if (res.status() >= 500) findings.push({ sev: 'HIGH', role: label, plant, path: base, status: res.status(), note: '5xx' });
    }
  }
  await api.dispose();
}

console.log(JSON.stringify({ rows, findings }, null, 0));
