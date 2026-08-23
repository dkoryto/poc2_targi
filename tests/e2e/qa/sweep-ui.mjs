import { chromium, request } from '@playwright/test';
const WEB = process.env.E2E_WEB_URL ?? 'http://localhost:5173';
const API = process.env.E2E_API_URL ?? 'http://localhost:5080';
const ROUTES = ['/', '/supply', '/supply/orders/PO-2026-0007', '/inbound', '/planning', '/trace',
  '/trace/serials/PMV-2026-0007', '/trace/lots/HTS-22-2608', '/trace/lots', '/passports',
  '/passports/PMV-2026-0007', '/audit', '/admin', '/notifications', '/demo/summary'];
const ROLES = [['DemoPresenter'], ['ProductionPlanner'], ['InboundCoordinator'], ['QualityInspector'],
  ['OperationsDirector'], ['Auditor'], ['Administrator'], ['SupplierUser', 'SUP-02']];
const PLANTS = ['SITE-01', 'SITE-03'];

const token = async (role, sup) => {
  const anon = await request.newContext({ baseURL: API });
  const q = sup ? `&supplierCode=${sup}` : '';
  const { accessToken } = await (await anon.get(`/api/v1/auth/demo-login?role=${role}${q}`)).json();
  await anon.dispose(); return accessToken;
};

const browser = await chromium.launch();
const out = [];
for (const [role, sup] of ROLES) {
  const t = await token(role, sup);
  const label = sup ? `${role}:${sup}` : role;
  for (const plant of PLANTS) {
    const ctx = await browser.newContext({ viewport: { width: 1600, height: 1000 }, locale: 'pl-PL' });
    await ctx.addInitScript(([tok, pl]) => {
      try { sessionStorage.setItem('dspc.token', tok); localStorage.setItem('dspc.token', tok); localStorage.setItem('dspc.site', pl); localStorage.setItem('dspc.theme', 'dark'); } catch {}
    }, [t, plant]);
    const page = await ctx.newPage();
    for (const route of ROUTES) {
      const errors = [];
      const failed = [];
      page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 150)); });
      page.on('response', (r) => { if (r.status() >= 500) failed.push(`${r.status()} ${new URL(r.url()).pathname}`); });
      await page.goto(WEB + route, { waitUntil: 'domcontentloaded' }).catch(() => {});
      await page.waitForTimeout(1400);
      const info = await page.evaluate(() => {
        const txt = document.body.innerText || '';
        return {
          chars: txt.replace(/\s+/g, ' ').trim().length,
          errorBoundary: !!document.querySelector('[data-testid="error-boundary"]'),
          emptyStates: document.querySelectorAll('[class*=empty], [class*=Empty]').length,
          notFound: /nie znaleziono|not found|404/i.test(txt.slice(0, 4000)),
          url: location.pathname,
        };
      });
      out.push({ role: label, plant, route, ...info,
        consoleErrors: [...new Set(errors.filter(e => !/favicon|negotiation|SignalR|Failed to load resource/i.test(e)))].slice(0, 2),
        http5xx: [...new Set(failed)] });
      page.removeAllListeners('console'); page.removeAllListeners('response');
    }
    await ctx.close();
  }
}
await browser.close();
console.log(JSON.stringify(out));
