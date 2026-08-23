import { chromium, request } from '@playwright/test';
const WEB='http://localhost:5173', API='http://localhost:5080';
const tok = async (role,sup) => { const a=await request.newContext({baseURL:API}); const q=sup?`&supplierCode=${sup}`:''; const {accessToken}=await (await a.get(`/api/v1/auth/demo-login?role=${role}${q}`)).json(); await a.dispose(); return accessToken; };
const cases=[['QualityInspector',null,'SITE-01','/admin'],['Administrator',null,'SITE-01','/'],['Auditor',null,'SITE-03','/admin'],['SupplierUser','SUP-02','SITE-01','/planning']];
const b=await chromium.launch();
for (const [role,sup,plant,route] of cases){
  const t=await tok(role,sup);
  const c=await b.newContext({viewport:{width:1400,height:900},locale:'pl-PL'});
  await c.addInitScript(([tk,pl])=>{try{sessionStorage.setItem('dspc.token',tk);localStorage.setItem('dspc.site',pl);localStorage.setItem('dspc.theme','dark');}catch{}},[t,plant]);
  const p=await c.newPage();
  await p.goto(WEB+route,{waitUntil:'domcontentloaded'}); await p.waitForTimeout(2000);
  const r=await p.evaluate(()=>({url:location.pathname, text:(document.body.innerText||'').replace(/\s+/g,' ').trim().slice(0,220), html:document.getElementById('root')?.innerHTML.length}));
  console.log(`${role}${sup?':'+sup:''} ${plant} ${route}\n   -> url=${r.url} rootHtml=${r.html}\n   -> "${r.text}"\n`);
  await c.close();
}
await b.close();
