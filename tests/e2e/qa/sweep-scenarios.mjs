// Runs every preset on every plant and checks internal coherence of the result.
import { request } from '@playwright/test';
const API = process.env.E2E_API_URL ?? 'http://localhost:5080';
const PLANTS = ['SITE-01', 'SITE-02', 'SITE-03', 'SITE-04'];

const login = async (role) => {
  const anon = await request.newContext({ baseURL: API });
  const { accessToken } = await (await anon.get(`/api/v1/auth/demo-login?role=${role}`)).json();
  await anon.dispose();
  return request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` } });
};

const api = await login('DemoPresenter');
const out = [];

for (const plant of PLANTS) {
  const presets = await (await api.get(`/api/v1/planning/scenarios/presets?siteCode=${plant}`)).json();
  const list = Array.isArray(presets) ? presets : presets.items;
  const featured = list.filter((p) => p.featured).map((p) => p.key);
  const baseBefore = await (await api.get(`/api/v1/planning/baseline?siteCode=${plant}`)).json();

  for (const p of list) {
    if (p.key === 'custom') continue;
    const created = await (await api.post('/api/v1/planning/scenarios', { data: { name: `qa ${plant} ${p.key}`, changes: p.changes } })).json();
    if (!created.id) { out.push({ plant, key: p.key, error: `create failed: ${JSON.stringify(created).slice(0,160)}` }); continue; }
    const t0 = Date.now();
    await api.post(`/api/v1/planning/scenarios/${created.id}/run`);
    let sc = created;
    for (let i = 0; i < 40; i++) {
      sc = await (await api.get(`/api/v1/planning/scenarios/${created.id}`)).json();
      if (sc.status === 'Completed' || sc.status === 'Failed') break;
      await new Promise((r) => setTimeout(r, 300));
    }
    const cmp = await (await api.get(`/api/v1/planning/scenarios/${created.id}/compare`)).json().catch(() => ({}));
    const baseAfter = await (await api.get(`/api/v1/planning/baseline?siteCode=${plant}`)).json();
    const changedOps = (sc.after?.operations ?? []).filter((o) => o.changed).length;
    out.push({
      plant, key: p.key, title: p.titleKey, featured: !!p.featured,
      status: sc.status, solver: sc.solver, elapsedMs: sc.elapsedMs, wallMs: Date.now() - t0,
      kpiBefore: sc.kpiBefore, kpiAfter: sc.kpiAfter,
      changesVsBaseline: sc.changesVsBaseline,
      changedOps, movedOpsCompare: (cmp.movedOperations ?? []).length,
      explanations: (sc.explanations ?? []).map((e) => `${e.reasonCode}:${e.orderCode ?? ''}`),
      lateOrders: (sc.after?.orders ?? []).filter((o) => o.latenessDays > 0).map((o) => `${o.code}+${o.latenessDays}d`),
      baselineUntouched: baseBefore.version === baseAfter.version,
    });
  }
  out.push({ plant, featuredKeys: featured });
}
console.log(JSON.stringify(out));
await api.dispose();
