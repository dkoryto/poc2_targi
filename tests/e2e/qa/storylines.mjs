import { chromium } from '@playwright/test';
const WEB = 'http://localhost:5173', API = 'http://localhost:5080';
const tok = async (role, site) => (await (await fetch(`${API}/api/v1/auth/demo-login?role=${role}${site?`&supplierCode=${site}`:''}`)).json()).accessToken;
const out = [];
const log = (step, ok, detail='') => { out.push({ step, ok, detail }); console.log(`${ok?'  ok  ':' FAIL '} ${step}${detail?' :: '+detail:''}`); };

await fetch(`${API}/api/v1/demo/reset`, { method:'POST', headers:{ Authorization:`Bearer ${await tok('DemoPresenter')}` }});
const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport:{width:1600,height:1000}, locale:'pl-PL', timezoneId:'Europe/Warsaw' });
const page = await ctx.newPage();
const errors = [];
page.on('console', m => { if (m.type()==='error' && !/1006|negotiation|favicon/i.test(m.text())) errors.push(m.text()); });
const go = async (u) => { await page.goto(WEB+u, { waitUntil:'domcontentloaded' }); await page.waitForTimeout(1400); };

// ---------- Storyline 1: ACT-40 delay -> what-if -> approve ----------
await go('/');
log('S1 dashboard renders', await page.getByTestId('kpi-MATERIAL_READINESS').isVisible());
const hr0 = (await page.getByTestId('kpi-HIGH_RISK_DELIVERIES').innerText()).match(/\d+/)?.[0];
log('S1 high-risk baseline = 3', hr0 === '3', `got ${hr0}`);

// supplier changes ETA through the API (the UI path is covered by spec 01)
const s = await tok('SupplierUser','SUP-02');
const po = await (await fetch(`${API}/api/v1/purchase-orders/PO-2026-0007`, { headers:{Authorization:`Bearer ${s}`}})).json();
const line = po.lines.find(l => l.partCode === 'ACT-40');
const eta = new Date(new Date(line.eta).getTime() + 10*864e5).toISOString().slice(0,10);
const r = await (await fetch(`${API}/api/v1/purchase-orders/PO-2026-0007/lines/${line.id}/eta`, {
  method:'POST', headers:{Authorization:`Bearer ${s}`,'Content-Type':'application/json'},
  body: JSON.stringify({ eta, reason:'PRODUCTION_DELAY', comment:'qa' })})).json();
log('S1 risk becomes Critical', r.risk?.category === 'Critical', `${r.risk?.score}`);
log('S1 endangers WO-2026-014', JSON.stringify(r.endangeredOrders||[]).includes('WO-2026-014'));

await go('/');
const hr1 = (await page.getByTestId('kpi-HIGH_RISK_DELIVERIES').innerText()).match(/\d+/)?.[0];
const dt1 = (await page.getByTestId('kpi-PREDICTED_DOWNTIME_H').innerText()).match(/\d+/)?.[0];
log('S1 dashboard reflects change (4 / 36h)', hr1==='4' && dt1==='36', `high-risk=${hr1} downtime=${dt1}`);

await go('/planning');
await page.getByTestId('scenario-tile-DELAY_ACT40_10D').click();
await page.getByTestId('scenario-status').waitFor({ timeout: 45000 });
await page.waitForTimeout(1500);
const body = await page.locator('body').innerText();
log('S1 what-if completed', /Zakończ|Completed/i.test(await page.getByTestId('scenario-status').innerText()));
log('S1 shows pulled-forward WO-2026-019', body.includes('WO-2026-019'));
log('S1 shows downtime 36 -> 8', /36/.test(body) && /\b8\b/.test(body));
const approve = page.getByTestId('btn-approve-plan');
log('S1 approve available', await approve.count() > 0);
if (await approve.count()) {
  await approve.first().click(); await page.waitForTimeout(600);
  const dlg = page.getByRole('button', { name: /Zatwierdź|Potwierdź/ }).last();
  if (await dlg.count()) { await dlg.click(); await page.waitForTimeout(2000); }
  const bl = await (await fetch(`${API}/api/v1/planning/baseline?siteCode=SITE-01`, { headers:{Authorization:`Bearer ${await tok('ProductionPlanner')}`}})).json();
  log('S1 baseline version bumped', bl.version >= 2, `v${bl.version}`);
}

// ---------- Storyline 2: block lot -> trace-forward -> passports invalidated ----------
await go('/trace/lots/HTS-22-2608');
const lotBody = await page.locator('body').innerText();
log('S2 lot page shows trace-forward', /PMV-2026-0007/.test(lotBody), lotBody.slice(0,0));
const blockBtn = page.getByTestId('btn-block-lot');
log('S2 block button present', await blockBtn.count() > 0);
if (await blockBtn.count()) {
  await blockBtn.first().click(); await page.waitForTimeout(700);
  const reason = page.locator('input[type=text], textarea').last();
  if (await reason.count()) await reason.fill('QA sweep');
  const confirm = page.getByRole('button', { name: /Zablokuj|Potwierdź/ }).last();
  if (await confirm.count()) { await confirm.click(); await page.waitForTimeout(2500); }
}
const q = await tok('QualityInspector');
const p7 = await (await fetch(`${API}/api/v1/passports/PMV-2026-0007`, { headers:{Authorization:`Bearer ${q}`}})).json();
log('S2 passport invalidated', p7.status === 'Invalidated', p7.status);
await go('/passports/PMV-2026-0007');
const pBody = await page.locator('body').innerText();
log('S2 UI shows invalidated', /Uniewa/i.test(pBody));

log('no unexpected console errors', errors.length === 0, errors.slice(0,2).join(' | '));
await browser.close();
const failed = out.filter(o => !o.ok);
console.log(`\n${out.length - failed.length}/${out.length} kroków OK`);
process.exit(failed.length ? 1 : 0);
