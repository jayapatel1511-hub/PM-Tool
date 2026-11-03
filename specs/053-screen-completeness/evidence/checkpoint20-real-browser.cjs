const fs = require('node:fs')
const { chromium } = require('../../../web/node_modules/playwright')
const out = 'specs/053-screen-completeness/evidence/checkpoint20-real-browser.json'
const base = 'http://127.0.0.1:8853'
const routes = ['/staff','/time','/tasks','/projects/QA20-REAL-001/tasks','/reports','/reports/workload-employee?projectId=01a19754-b327-78af-b89f-a3a43edcffd7&unit=fte&period=weeks']
const report = { observedAtUtc:new Date().toISOString(), base, rows:[], errors:[] }
;(async()=>{
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true})
 try { for (const viewport of [{name:'desktop',width:1440,height:1000},{name:'phone',width:375,height:812}]) { const ctx=await browser.newContext({viewport:{width:viewport.width,height:viewport.height},reducedMotion:'reduce'}); await ctx.addInitScript(()=>{sessionStorage.setItem('hub.devUser','sam@hub.test');sessionStorage.setItem('hub.signedIn','1')});
   for (const route of routes) { const page=await ctx.newPage(); page.setDefaultTimeout(15000); const errors=[]; page.on('pageerror',e=>errors.push(e.message)); try { const response=await page.goto(base+route,{waitUntil:'domcontentloaded'}); await page.locator('main').waitFor(); await page.waitForLoadState('networkidle',{timeout:10000}).catch(()=>{}); const text=await page.locator('body').innerText(); const overflow=await page.evaluate(()=>{const main=document.querySelector('main'); const rows=[...document.querySelectorAll('main tbody tr,main [role="row"],main section li')].filter(e=>{const r=e.getBoundingClientRect();return r.width>0&&r.height>0&&e.innerText.trim()}); return {scrollWidth:document.documentElement.scrollWidth,clientWidth:document.documentElement.clientWidth,mainText:main?.innerText.slice(0,500),firstDataY:rows.length?Math.min(...rows.map(e=>e.getBoundingClientRect().top)):null}}); const file=`specs/053-screen-completeness/evidence/checkpoint20-real-${viewport.name}-${route.slice(1).replaceAll('/','-').replaceAll('?','-').replaceAll('=','-').replaceAll('&','-')}.png`; await page.screenshot({path:file,fullPage:true}); report.rows.push({viewport:viewport.name,route,status:response?.status()??null,url:page.url(),hasMain:text.includes(''),textSample:text.slice(0,800),overflow,errors}); } catch(e) { report.errors.push({viewport:viewport.name,route,error:e.message,errors}) } await page.close() }
   await ctx.close()
 } } finally { await browser.close(); fs.writeFileSync(out,JSON.stringify(report,null,2)+'\n') }
 console.log(JSON.stringify({rows:report.rows.length,errors:report.errors.length,failed:report.rows.filter(r=>r.status>=400||r.errors.length||r.overflow.scrollWidth>r.overflow.clientWidth).length}))
})().catch(e=>{console.error(e);process.exitCode=1})
