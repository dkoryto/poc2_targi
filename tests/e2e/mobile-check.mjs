import { chromium, devices } from '@playwright/test';

const WEB = 'http://localhost:5173';
const API = 'http://localhost:5080';
const ROUTES = ['/', '/supply', '/inbound', '/planning', '/trace', '/passports', '/audit', '/admin', '/notifications'];
const VIEWPORTS = [
  { name: 'iPhone-390', width: 390, height: 844, dpr: 3, touch: true },
  { name: 'small-360', width: 360, height: 740, dpr: 3, touch: true },
  { name: 'tablet-768', width: 768, height: 1024, dpr: 2, touch: true },
];

const browser = await chromium.launch();
let failures = 0;

for (const vp of VIEWPORTS) {
  for (const theme of ['dark', 'light']) {
    const ctx = await browser.newContext({
      viewport: { width: vp.width, height: vp.height },
      deviceScaleFactor: vp.dpr,
      hasTouch: vp.touch,
      isMobile: true,
      locale: 'pl-PL',
      timezoneId: 'Europe/Warsaw',
    });
    const page = await ctx.newPage();
    await page.addInitScript((t) => { try { localStorage.setItem('dspc.theme', t); } catch {} }, theme);
    for (const route of ROUTES) {
      const errors = [];
      page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
      await page.goto(WEB + route, { waitUntil: 'networkidle' }).catch(() => {});
      await page.waitForTimeout(700);
      const r = await page.evaluate(() => {
        const de = document.documentElement;
        const clipped = [...document.querySelectorAll('h1,h2,h3,button,[class*=kpi] *,th,label')]
          .filter((el) => el.scrollWidth > el.clientWidth + 1 && getComputedStyle(el).overflow !== 'auto' && el.clientWidth > 0)
          .map((el) => (el.textContent || '').trim().slice(0, 32)).filter(Boolean).slice(0, 4);
        const small = [...document.querySelectorAll('button,a[href]')]
          .filter((el) => { const b = el.getBoundingClientRect(); return b.width > 0 && b.height > 0 && (b.height < 40 || b.width < 24); }).length;
        return { scrollW: de.scrollWidth, innerW: window.innerWidth, clipped, small };
      });
      const overflow = r.scrollW > r.innerW + 1;
      const bad = overflow || r.clipped.length > 0;
      if (bad) failures++;
      const flag = bad ? 'FAIL' : ' ok ';
      console.log(`[${flag}] ${vp.name} ${theme.padEnd(5)} ${route.padEnd(14)} scrollW=${r.scrollW}/${r.innerW}${overflow ? ' OVERFLOW' : ''}${r.clipped.length ? ' clipped=' + JSON.stringify(r.clipped) : ''}${r.small ? ' smallTargets=' + r.small : ''}${errors.length ? ' consoleErrors=' + errors.length : ''}`);
    }
    await ctx.close();
  }
}
await browser.close();
console.log(failures ? `\n${failures} checks FAILED` : '\nall checks passed');
