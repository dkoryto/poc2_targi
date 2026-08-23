import { request } from '@playwright/test';
const API = process.env.E2E_API_URL ?? 'http://localhost:5080';
const login = async (role, sup) => {
  const anon = await request.newContext({ baseURL: API });
  const q = sup ? `&supplierCode=${sup}` : '';
  const { accessToken } = await (await anon.get(`/api/v1/auth/demo-login?role=${role}${q}`)).json();
  await anon.dispose();
  return request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` } });
};
const res = [];
const check = async (name, fn) => {
  try { res.push({ name, ...(await fn()) }); }
  catch (e) { res.push({ name, error: String(e).slice(0, 200) }); }
};
const body = async (r) => {
  const t = await r.text();
  try { return JSON.parse(t); } catch { return t.slice(0, 160); }
};
const q = await login('QualityInspector');
const p = await login('DemoPresenter');
const pl = await login('ProductionPlanner');
const a = await login('Auditor');
const s1 = await login('SupplierUser', 'SUP-01');
const anon = await request.newContext({ baseURL: API });

await check('unknown serial', async () => { const r = await q.get('/api/v1/trace/serials/NOPE-1'); return { status: r.status(), body: await body(r) }; });
await check('unknown lot', async () => { const r = await q.get('/api/v1/lots/NOPE-1'); return { status: r.status() }; });
await check('unknown PO', async () => { const r = await pl.get('/api/v1/purchase-orders/PO-9999-9999'); return { status: r.status() }; });
await check('unknown passport', async () => { const r = await q.get('/api/v1/passports/NOPE-1'); return { status: r.status() }; });
await check('unknown scenario id (guid)', async () => { const r = await pl.get('/api/v1/planning/scenarios/00000000-0000-0000-0000-000000000000'); return { status: r.status() }; });
await check('malformed scenario id', async () => { const r = await pl.get('/api/v1/planning/scenarios/not-a-guid'); return { status: r.status() }; });
await check('unknown siteCode', async () => { const r = await pl.get('/api/v1/dashboard/kpis?siteCode=SITE-99'); return { status: r.status() }; });
await check('no token', async () => { const r = await anon.get('/api/v1/dashboard/kpis'); return { status: r.status() }; });
await check('garbage token', async () => {
  const c = await request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: 'Bearer not.a.jwt' } });
  const r = await c.get('/api/v1/dashboard/kpis'); await c.dispose(); return { status: r.status() }; });
await check('incomplete passport generate', async () => { const r = await q.post('/api/v1/passports/SCM-2026-0103/generate'); return { status: r.status(), body: await body(r) }; });
await check('supplier reads foreign PO', async () => { const r = await s1.get('/api/v1/purchase-orders/PO-2026-0009'); return { status: r.status() }; });
await check('auditor tries to block lot', async () => { const r = await a.post('/api/v1/lots/HTS-22-2608/block', { data: { reason: 'qa', ncrTitle: 'qa' } }); return { status: r.status() }; });
await check('block lot twice', async () => {
  const r1 = await q.post('/api/v1/lots/HTS-22-2608/block', { data: { reason: 'qa1', ncrTitle: 'NCR qa1' } });
  const r2 = await q.post('/api/v1/lots/HTS-22-2608/block', { data: { reason: 'qa2', ncrTitle: 'NCR qa2' } });
  return { status: `${r1.status()} then ${r2.status()}`, body2: await body(r2) }; });
await check('block unknown lot', async () => { const r = await q.post('/api/v1/lots/NOPE-9/block', { data: { reason: 'x', ncrTitle: 'y' } }); return { status: r.status() }; });
await check('ETA in the past', async () => {
  const po = await (await s1.get('/api/v1/purchase-orders/PO-2026-0013')).json();
  const line = po.lines?.[0];
  if (!line) return { status: 'no line' };
  const r = await s1.post(`/api/v1/purchase-orders/PO-2026-0013/lines/${line.id}/eta`, { data: { eta: '2020-01-01', reason: 'PRODUCTION_DELAY' } });
  return { status: r.status(), body: await body(r) }; });
await check('cross-plant scenario', async () => {
  const k = await (await pl.get('/api/v1/planning/scenarios/presets?siteCode=SITE-01')).json();
  const z = await (await pl.get('/api/v1/planning/scenarios/presets?siteCode=SITE-03')).json();
  const kl = (Array.isArray(k) ? k : k.items).find(x => x.key === 'DELAY_ACT40_10D');
  const zl = (Array.isArray(z) ? z : z.items).find(x => x.key === 'BLOCK_LOT_HTS22');
  const r = await pl.post('/api/v1/planning/scenarios', { data: { name: 'qa mix', changes: [...kl.changes, ...zl.changes] } });
  return { status: r.status(), body: await body(r) }; });
await check('approve twice', async () => {
  const k = await (await pl.get('/api/v1/planning/scenarios/presets?siteCode=SITE-01')).json();
  const kl = (Array.isArray(k) ? k : k.items).find(x => x.key === 'DELAY_ACT40_10D');
  const c = await (await pl.post('/api/v1/planning/scenarios', { data: { name: 'qa approve', changes: kl.changes } })).json();
  await pl.post(`/api/v1/planning/scenarios/${c.id}/run`);
  for (let i = 0; i < 30; i++) { const sc = await (await pl.get(`/api/v1/planning/scenarios/${c.id}`)).json(); if (sc.status === 'Completed') break; await new Promise(r => setTimeout(r, 300)); }
  const a1 = await pl.post(`/api/v1/planning/scenarios/${c.id}/approve`);
  const a2 = await pl.post(`/api/v1/planning/scenarios/${c.id}/approve`);
  return { status: `${a1.status()} then ${a2.status()}`, body2: await body(a2) }; });
await check('document wrong extension', async () => {
  const r = await s1.post('/api/v1/documents', { multipart: { file: { name: 'evil.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('MZ') }, type: 'MATERIAL_CERT', documentNumber: 'X', issuedOn: '2026-01-01' } });
  return { status: r.status(), body: await body(r) }; });
await check('document oversized', async () => {
  const r = await s1.post('/api/v1/documents', { multipart: { file: { name: 'big.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(13 * 1024 * 1024, 1) }, type: 'MATERIAL_CERT', documentNumber: 'X', issuedOn: '2026-01-01' } });
  return { status: r.status() }; });
await check('stale If-Match', async () => {
  const po = await (await s1.get('/api/v1/purchase-orders/PO-2026-0013')).json();
  const line = po.lines?.[0];
  const r = await s1.fetch(`/api/v1/purchase-orders/PO-2026-0013/lines/${line.id}`, { method: 'PATCH', headers: { 'If-Match': '"stale-rowversion"', 'Content-Type': 'application/json' }, data: { progressPercent: 55 } });
  return { status: r.status() }; });

console.log(JSON.stringify(res, null, 1));
