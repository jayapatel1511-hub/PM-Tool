const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const results = path.resolve(__dirname, '../test-results/handoffs');
fs.mkdirSync(results, { recursive: true });
let server, browser;
const assert = require('assert/strict');
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12,'0')}`;
const pid=id(1), sender=id(2), receiver=id(3), civil=id(4), electrical=id(5), source=id(6), target=id(7), hid=id(8);
const permit={ok:true};
const project={id:pid,projectNumber:'P-DEMO',name:'Harbour service coordination',client:'Pilot client',pmName:'Jay Patel',status:'Active',visibility:'Open',rowVersion:1,links:[],disciplines:[],starred:false,myRoles:['PM'],health:{computed:'Green',reported:'Green',overrideActive:false,reasons:[]},permissions:{edit:permit,healthOverride:{ok:false},isPm:true,leadOf:[],createTaskIn:[civil],createDeliverableIn:[civil],transitions:[]}};
const me={id:sender,displayName:'Alex Engineer',email:'alex@hub.test',roles:[],systemRoles:[],capabilities:{createProject:true,createTask:true,portfolio:true,workload:true,staff:true,admin:false,templates:true,readOnly:false,directReports:0},settings:{today:'2026-09-27',dateFormat:'yyyy-MM-dd',orgTimeZone:'America/Halifax',idleTimeoutHours:8},preferences:{denseRows:true,digestEnabled:true}};
const options={canCreate:true,people:[{id:sender,displayName:'Alex Engineer'},{id:receiver,displayName:'Omar Electrical'}],disciplines:[{id:civil,name:'Civil'},{id:electrical,name:'Electrical'}],sources:[{id:source,key:'P-DEMO-D001',name:'Survey corridor',projectDisciplineId:civil,ownerId:sender,revision:'A',transmittalUrl:'https://example.test/survey-A.pdf',rowVersion:2}],tasks:[{id:target,key:'P-DEMO-T0001',name:'Service alignment',projectDisciplineId:electrical,ownerId:receiver,dueDate:'2026-09-29'}]};
let row=null, history=[], revisions=[], requests=[];
const errors=[], unknown=[];
(async()=>{
 server=spawn(process.execPath, ['node_modules/vite/bin/vite.js','preview','--host','127.0.0.1','--port','5173','--strictPort'], {cwd:path.resolve(__dirname,'..'),stdio:'inherit'});
 for(let attempt=0;attempt<50;attempt++){try{if((await fetch('http://127.0.0.1:5173')).ok)break;}catch{} await new Promise(r=>setTimeout(r,100));}
 browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 await page.addInitScript(()=>sessionStorage.setItem('hub.devUser','alex@hub.test'));
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/**',async route=>{
  const req=route.request(),u=new URL(req.url()),path=u.pathname.replace('/api/v1/',''),method=req.method();let data={};
  if(path==='config')data={authMode:'Development',entra:{}};
  else if(path==='me')data=me;
  else if(path==='me/sign-in')data={};
  else if(path==='me/notifications/pulse')data={stamp:'0'};
  else if(path==='me/notifications/unread-count')data={notifications:0,following:0};
  else if(path==='workspaces')data=[];
  else if(path==='views')data={views:[],canShare:true};
  else if(path==='projects')data={items:[project],totalCount:1};
  else if(path.endsWith('/date-review'))data={window:null,tasks:[],deliverables:[]};
  else if(path.endsWith('/follow'))data={level:'My items only',source:'Assignment'};
  else if(path===`projects/${pid}`||path==='projects/P-DEMO')data=project;
  else if(path.endsWith('/handoffs/options'))data=options;
  else if(path===`projects/${pid}/handoffs`&&method==='POST'){
   const b=req.postDataJSON();requests.push(b);row={...b,id:hid,key:'P-DEMO-H001',rowVersion:0,status:'Draft',sourceKey:'P-DEMO-D001',targetKey:'P-DEMO-T0001',senderName:'Alex Engineer',receiverName:'Omar Electrical',ownersAvailable:true,sourceChanged:false,dateMismatch:b.promisedBy>b.neededBy,isOverdue:false};data={id:hid,rowVersion:0};
  } else if(path===`projects/${pid}/handoffs`)data={items:row?[row]:[],totalCount:row?1:0,page:1,pageSize:50};
  else if(path.endsWith('/transition')){
   const b=req.postDataJSON();requests.push(b);
   history.push({id:id(20+history.length),fromStatus:row.status,toStatus:b.toStatus,actor:b.toStatus==='Submitted'?'Alex Engineer':'Omar Electrical',createdAt:'2026-09-27T02:00:00Z',reason:b.reason,criteriaOutcome:b.criteriaOutcome,revisionId:id(9)});
   row.status=b.toStatus;row.rowVersion++;
   if(b.toStatus==='Submitted'){row.currentRevisionId=id(9);revisions.push({id:id(9),intendedUse:row.intendedUse,acceptanceCriteria:row.acceptanceCriteria,createdAt:'2026-09-27T02:00:00Z',source:{sourceKey:row.sourceKey,revision:'A',url:row.sourceUrl}});}
   if(b.toStatus==='Incorporated')row.incorporatedRevisionId=id(9);
   data={id:hid,rowVersion:row.rowVersion};
  } else if(path===`projects/${pid}/handoffs/${hid}`){
   const next={Draft:['Submitted','Cancelled'],Submitted:['Accepted','Clarification Requested','Returned','Cancelled'],Accepted:['Incorporated','Cancelled'],Incorporated:[]};
   data={row,history,revisions,permissions:{edit:{ok:row.status==='Draft'},assign:permit,transitions:(next[row.status]||[]).map(to=>({to,permission:permit}))}};
  } else {unknown.push(path); data={};}
  await route.fulfill({status:method==='POST'&&path===`projects/${pid}/handoffs`?201:200,contentType:'application/json',body:JSON.stringify(data)});
 });
 await page.goto('http://127.0.0.1:5173/projects/P-DEMO/handoffs');
 await page.getByRole('button',{name:'New handoff',exact:true}).click();
 await page.getByLabel('Source deliverable',{exact:true}).selectOption(source);
 await page.getByLabel('Receiving discipline',{exact:true}).selectOption(electrical);
 await page.getByLabel('Receiving work',{exact:true}).selectOption(`Task:${target}`);
 assert.equal(await page.getByLabel('Sending owner',{exact:true}).inputValue(),sender);
 assert.equal(await page.getByLabel('Receiving owner',{exact:true}).inputValue(),receiver);
 await page.getByLabel('Intended use and scope',{exact:true}).fill('Set the electrical service alignment');
 await page.getByLabel('Acceptance criteria',{exact:true}).fill('The survey covers the full service corridor');
 await page.getByLabel('Promised by',{exact:true}).fill('2026-10-01');
 await page.addScriptTag({path:require.resolve('axe-core/axe.min.js')});
 const formAxe=await page.evaluate(()=>axe.run(document.querySelector('[role="dialog"]'),{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}));
 await page.screenshot({path:path.join(results,'handoff-form-qa.png'),fullPage:true});
 await page.getByRole('button',{name:'Save draft',exact:true}).click();
 await page.getByRole('button',{name:'Submit input',exact:true}).click();
 await page.getByRole('button',{name:'Confirm',exact:true}).click();
 await page.getByRole('button',{name:'Accept for use',exact:true}).click();
 await page.getByLabel('How the acceptance criteria were met',{exact:true}).fill('Both ends of the corridor were checked');
 await page.getByRole('button',{name:'Confirm',exact:true}).click();
 await page.getByRole('button',{name:'Record incorporation',exact:true}).click();
 await page.getByLabel('What was incorporated into the receiving work',{exact:true}).fill('Used revision A in drawing E-101');
 await page.getByRole('button',{name:'Confirm',exact:true}).click();
 await page.getByText('Accepted → Incorporated',{exact:true}).waitFor();
 const receiptAxe=await page.evaluate(()=>axe.run(document.querySelector('[role="dialog"]'),{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}));
 await page.screenshot({path:path.join(results,'handoff-receipt-qa.png'),fullPage:true});
 assert.equal(row.status,'Incorporated');assert.equal(requests.length,4);
 for(const request of requests)assert.match(request.requestId,/^[a-f0-9-]{36}$/);
 assert.equal(new Set(requests.map(x=>x.requestId)).size,4);
 assert.equal(errors.length,0,JSON.stringify(errors));
 const report={scope:'Chromium UI workflow with mocked API responses; not a backend end-to-end test',workflow:'create → submit → accept → incorporate',requests:requests.length,errors,unknown,formViolations:formAxe.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>n.target)})),receiptViolations:receiptAxe.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>n.target)}))};
 fs.writeFileSync(path.join(results,'handoff-ui-qa.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
 assert.deepEqual(report.formViolations,[], 'Draft dialog accessibility');
 assert.deepEqual(report.receiptViolations,[], 'Receipt dialog accessibility');
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{await browser?.close();server?.kill();});
