import { chromium } from '@playwright/test';
const WEB='http://localhost:5173', API='http://localhost:5080';
const tok = async r => (await (await fetch(`${API}/api/v1/auth/demo-login?role=${r}`)).json()).accessToken;
await fetch(`${API}/api/v1/demo/reset`,{method:'POST',headers:{Authorization:`Bearer ${await tok('DemoPresenter')}`}});
const b = await chromium.launch(); const ctx = await b.newContext({viewport:{width:1600,height:1000},locale:'pl-PL'}); const page = await ctx.newPage();
page.on('request', q => { if (q.url().includes('/block')) console.log('REQ', q.method(), q.url(), q.postData()); });
page.on('response', async res => { if (res.url().includes('/block')) console.log('RES', res.status(), (await res.text()).slice(0,400)); });
await page.goto(WEB+'/trace/lots/HTS-22-2608',{waitUntil:'domcontentloaded'}); await page.waitForTimeout(1500);
await page.getByTestId('btn-block-lot').first().click(); await page.waitForTimeout(800);
// enumerate the dialog's fields so we see what the form actually asks for
const fields = await page.evaluate(() => [...document.querySelectorAll('[role=dialog] input,[role=dialog] textarea,[role=dialog] select')]
  .map(e => ({ tag:e.tagName, type:e.type||'', name:e.name||'', id:e.id||'', placeholder:e.placeholder||'', label:(e.labels&&e.labels[0]?e.labels[0].innerText:'').trim(), required:e.required })));
console.log('POLA FORMULARZA:', JSON.stringify(fields,null,1));
const btns = await page.evaluate(() => [...document.querySelectorAll('[role=dialog] button')].map(b=>b.innerText.trim()));
console.log('PRZYCISKI:', JSON.stringify(btns));
// try submitting with everything empty (what a hurried presenter does)
const submit = page.getByRole('button',{name:/Zablokuj/}).last();
await submit.click(); await page.waitForTimeout(1800);
const after = await page.evaluate(() => ({ dialogOpen: !!document.querySelector('[role=dialog]'),
  visibleError: (document.querySelector('[role=dialog]')?.innerText||'').split('\n').filter(t=>/wymag|błąd|error|min|puste/i.test(t)).slice(0,3) }));
console.log('PO PUSTYM SUBMIT:', JSON.stringify(after));
await b.close();
