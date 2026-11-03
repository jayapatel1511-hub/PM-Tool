const fs=require('node:fs');
const {chromium}=require('../../../web/node_modules/playwright');
const base='http://127.0.0.1:8853';
const project='QA20-REAL-001';
const issue='01a19762-fecc-72ac-be72-5d1fd4ffaa5b';
const report={observedAtUtc:new Date().toISOString(),rows:[],errors:[]};
(async()=>{const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});try{
 for(const cfg of [{name:'issue',route:`/projects/${project}/issues?panel=Issue:${issue}`},{name:'changes',route:`/projects/${project}/changes`}]) for(const v of [{name:'desktop',width:1440,height:1000},{name:'phone',width:375,height:812}]){
  const ctx=await browser.newContext({viewport:{width:v.width,height:v.height},reducedMotion:'reduce'});
  await ctx.addInitScript(()=>{sessionStorage.setItem('hub.devUser','priya@hub.test');sessionStorage.setItem('hub.signedIn','1')});
  const page=await ctx.newPage(); const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  try { const resp=await page.goto(base+cfg.route,{waitUntil:'domcontentloaded'}); await page.locator('main').waitFor(); await page.waitForLoadState('networkidle',{timeout:10000}).catch(()=>{});
   if(cfg.name==='changes'){const candidate=page.getByRole('button',{name:'New'}).first(); if(await candidate.count()) await candidate.click(); await page.waitForTimeout(150); const source=page.getByRole('button',{name:/Register source revision/i}).first(); if(await source.count()) await source.click();}
   if(cfg.name==='issue'){const picker=page.getByRole('button',{name:'Choose a person…'}).first(); if(await picker.count()){await picker.scrollIntoViewIfNeeded(); await picker.click({force:true}); await page.waitForTimeout(500); const search=page.locator('input[placeholder="Search people…"], input[placeholder="Search"]').last(); if(await search.count() && await search.isVisible()) await search.fill('Marc');}}
   await page.waitForTimeout(400); const text=await page.locator('body').innerText(); const items=await page.locator('[role="option"]').allTextContents(); const buttons=await page.getByRole('button').allTextContents();
   const file=`specs/053-screen-completeness/evidence/checkpoint20-real-d-${cfg.name}-${v.name}.png`; await page.screenshot({path:file,fullPage:true});
   report.rows.push({name:cfg.name,viewport:v.name,status:resp?.status(),url:page.url(),hasMarc:text.includes('Marc Dubois'),hasNotice:/Co-op|Starts 2026-11-02|Ends 2026-11-11/.test(text),items,buttons:buttons.filter(x=>x.trim()),textSample:text.slice(-3500),errors});
  } catch(e){report.errors.push({name:cfg.name,viewport:v.name,error:e.message,errors})} finally {await page.close();await ctx.close()}
 }
}finally{await browser.close();fs.writeFileSync('specs/053-screen-completeness/evidence/checkpoint20-real-d-browser.json',JSON.stringify(report,null,2)+'\n')}})().catch(e=>{console.error(e);process.exitCode=1})
