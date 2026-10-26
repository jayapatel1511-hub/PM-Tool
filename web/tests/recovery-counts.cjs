// Packet 045: real browser interactions over labelled synthetic API responses.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');
const port = process.env.RECOVERY_TEST_PORT || '19504', base = `http://127.0.0.1:${port}`;
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12,'0')}`;
const pid=id(1), person=id(2), civil=id(3), client=id(4), office=id(5), deleted=id(6), parent=id(7), template=id(8);
const yes={ok:true}, no={ok:false};
let flags={show_deleted:true,status_rollups:true,template_working_days:true}, restoreAllowed=true, restored=false, templateDraft=false;
const me=()=>({id:person,displayName:'Synthetic PM',email:'pm@hub.test',roles:[],systemRoles:[],isTemplateEditor:true,
 capabilities:{createProject:true,createTask:true,portfolio:true,workload:false,staff:true,allStaff:true,admin:false,templates:true,readOnly:false,directReports:2},
 settings:{today:'2026-10-18',dateFormat:'yyyy-MM-dd',orgTimeZone:'America/Halifax',idleTimeoutHours:8,taskDueSoonDays:7,milestoneApproachingDays:14,chainDepthLimit:20,coordinationLookaheadWeeks:6},preferences:{denseRows:true},flags});
const project=()=>({id:pid,projectNumber:'SYN-RCV',name:'Synthetic recovery project',clientId:client,client:'Synthetic client',officeId:office,office:'Synthetic office',pmName:'Synthetic PM',status:'Active',visibility:'Open',rowVersion:1,links:[],disciplines:[{id:civil,disciplineId:civil,name:'Civil',code:'C',colour:'#467762',leadUserId:person,leadName:'Synthetic PM',isActive:true}],starred:false,myRoles:['PM'],
 health:{computed:'Green',reported:'Green',overrideActive:false,reasons:[]},permissions:{edit:yes,healthOverride:no,reviewDates:no,snooze:no,manageMilestones:no,raiseRegister:no,isPm:true,leadOf:[],createTaskIn:[],createDeliverableIn:[],transitions:[],restore:{ok:restoreAllowed}}});
const row=(n)=>({id:id(20+n),projectNumber:`SYN-${n}`,name:`Synthetic project ${n}`,client:{id:client,name:'Synthetic client'},pm:{id:person,displayName:'Synthetic PM'},office:'Synthetic office',officeId:office,status:'Active',computedHealth:n===1?'Red':'Green',reportedHealth:n===1?'Red':'Green',healthReasons:[],overrideActive:false,trend:[],overdueTasks:n,blockedTasks:n,overdueDecisions:n,highIssues:0,attentionCritical:n,attentionWarning:0,overdueDeliverables:n+1,myRoles:[]});
const count=(value=0,link='')=>({value,link});
const dash=()=>({milestones:[],tasks:Object.fromEntries(['total','complete','inProgress','review','overdue','blocked','waiting','unassigned'].map(k=>[k,count()])),deliverables:Object.fromEntries(['upcoming','atRisk','issued','total'].map(k=>[k,count()])),decisions:{pending:count(),overdue:count()},issues:{open:0,high:0},risks:{high:0},registers:{issuesOpen:count(),issuesHigh:count(),risksHigh:count()},disciplineDue14:{[civil]:count()},disciplines:[{disciplineId:civil,name:'Civil',leadId:person,health:'Green',reasons:[],open:0,overdue:0,blocked:0,waiting:0,deliverablesDue14:0}],dueThisWeek:{items:[],total:0,link:''},blocked:{items:[],total:0,link:''},activity:[],...(flags.status_rollups?{disciplineStatus:{[civil]:{notStarted:count(2,`disciplineId=${civil}&status=Not+Started`),inProgress:count(3,`disciplineId=${civil}&status=In+Progress,In+Review`),issued:count(4,`disciplineId=${civil}&status=Issued,Accepted`),onHold:count(1,`disciplineId=${civil}&status=On+Hold`)}}}:{})});
let server,browser;const errors=[],requests=[],unknown=[];
(async()=>{
 server=spawn(process.execPath,['node_modules/vite/bin/vite.js','preview','--host','127.0.0.1','--port',port,'--strictPort'],{cwd:path.resolve(__dirname,'..'),stdio:'inherit'});
 for(let i=0;i<80;i++){try{if((await fetch(base)).ok)break}catch{}await new Promise(r=>setTimeout(r,100))}
 browser=await chromium.launch({headless:true,...(process.env.CHROME_EXECUTABLE_PATH?{executablePath:process.env.CHROME_EXECUTABLE_PATH}:{})});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});await page.addInitScript(()=>sessionStorage.setItem('hub.devUser','pm@hub.test'));
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/**',async route=>{
  const req=route.request(),u=new URL(req.url()),p=u.pathname.replace('/api/v1/','');let data={},status=200;
  if(p==='config')data={authMode:'Development',entra:{}};
  else if(p==='me')data=me();
  else if(p==='me/sign-in')data={};
  else if(p==='me/notifications/pulse')data={stamp:'0'};
  else if(p==='me/notifications/unread-count')data={notifications:0,following:0};
  else if(p==='workspaces')data=[];
  else if(p==='views')data={views:[],canShare:false};
  else if(p==='reference')data={disciplines:[{id:civil,name:'Civil',isActive:true}],clients:[{id:client,name:'Synthetic client',isActive:true}],offices:[{id:office,name:'Synthetic office',isActive:true}],deliverableTypes:[],phases:[],projectTypes:[],limits:{roleLabel:80}};
  else if(p==='projects')data={items:[{...row(1),id:pid,projectNumber:'SYN-RCV'}],totalCount:1};
  else if(p===`projects/${pid}`||p==='projects/SYN-RCV')data=project();
  else if(p.endsWith('/date-review'))data={window:null,tasks:[],deliverables:[]};
  else if(p.endsWith('/follow'))data={level:'My items only',source:'Assignment'};
  else if(p===`tasks/${deleted}/restore`){requests.push({path:p,ifMatch:req.headers()['if-match']});restored=true;data={id:deleted,rowVersion:5};}
  else if(p.endsWith('/deleted'))data={items:restored?[]:[{id:deleted,key:'SYN-RCV-T001',name:'Synthetic deleted task',discipline:'Civil',parentKey:'SYN-RCV-D001',status:'Not Started',deletedAt:'2026-10-18T12:00:00Z',deletedBy:'Synthetic PM',rowVersion:4},{id:parent,key:'SYN-RCV-T002',name:'Synthetic child',discipline:'Civil',parentKey:'SYN-RCV-D002',restoreBlocked:'Restore SYN-RCV-D002 first',status:'Not Started',deletedAt:'2026-10-18T11:00:00Z',deletedBy:'Synthetic PM',rowVersion:2}],totalCount:restored?0:2,page:1,pageSize:200};
  else if(p===`projects/${pid}/tasks`)data={items:[],totalCount:0,page:1,pageSize:50};
  else if(p===`projects/${pid}/deliverables`||p===`projects/${pid}/milestones`)data=[];
  else if(p===`projects/${pid}/dashboard`)data=dash();
  else if(p.endsWith('/attention'))data={items:[],total:0,snoozed:0};
  else if(p==='portfolio'){const rows=[row(1),row(2)];if(!flags.status_rollups)rows.forEach(r=>delete r.overdueDeliverables);data={ownOnly:false,tiles:{active:2,red:1,yellow:0,green:1,submissions14:0,overdueDecisions:3},projects:rows};}
  else if(p==='templates')data={canEdit:false,templates:[{id:template,familyId:template,name:'Synthetic template',version:1,status:'Published',milestones:0,deliverables:0,tasks:0,roles:0,projects:0}]};
  else if(p===`templates/${template}`)data={id:template,familyId:template,name:'Synthetic template',status:templateDraft?'Draft':'Published',version:1,canEdit:templateDraft,rowVersion:1,family:[],disciplines:[],milestones:[],deliverables:[],tasks:[],dependencies:[],basisSuggestions:[],...(flags.template_working_days?{offsetDayKind:'WorkingDays'}:{})};
  else if(p.endsWith('/preview')){requests.push({path:p,body:req.postDataJSON()});data={milestones:[],counts:{milestones:0,deliverables:0,tasks:0,dependencies:0},undated:{milestones:0,deliverables:0,tasks:0}};}
  else if(p==='staff')data={scope:'direct',canSeeAll:false,tiles:{staff:0,assignments:0,withOverdue:0,withBlocked:0,reviewsStalled:0},people:[]};
  else if(p==='time')data={entries:[{id:id(40),taskId:id(41),projectId:pid,projectNumber:'SYN-RCV',projectName:'Synthetic project',taskKey:'SYN-T1',taskName:'Synthetic work',person:'Synthetic PM',userId:person,workDate:'2026-10-13',hours:3,canEdit:false}],total:3,byDay:[],byProject:[{projectId:pid,projectNumber:'SYN-RCV',projectName:'Synthetic project',hours:3}],byTask:[{taskId:id(41),taskKey:'SYN-T1',taskName:'Synthetic work',hours:3}],canReview:false};
  else if(p==='users')data=[];
  else unknown.push(p);
  await route.fulfill({status,contentType:'application/json',body:JSON.stringify(data)});
 });
 const axe=async()=>{await page.addScriptTag({path:require.resolve('axe-core/axe.min.js')});return(await page.evaluate(()=>axe.run(document.querySelector('main'),{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}}))).violations.map(v=>({id:v.id,nodes:v.nodes.map(n=>n.target)}))};
 // AC-RCV-01/03/05/06: deleted replaces live rows; parent-first and concurrency header.
 await page.goto(`${base}/projects/SYN-RCV/tasks?deleted=true`);await page.getByRole('cell',{name:'Synthetic deleted task',exact:true}).waitFor();
 assert.equal(await page.getByRole('button',{name:'Restore',exact:true}).count(),2); // table + phone, one visible presentation
 const visibleRestore=page.getByRole('row').filter({hasText:'Synthetic deleted task'}).getByRole('button',{name:'Restore',exact:true});
 assert.equal(await page.getByRole('row').filter({hasText:'Synthetic child'}).getByRole('button',{name:'Restore',exact:true}).isDisabled(),true);
 const deletedAxe=await axe();await visibleRestore.click();await page.getByText('No deleted items.',{exact:true}).waitFor();assert.equal(requests[0].ifMatch,'"4"');
 flags.show_deleted=false;await page.reload();assert.equal(await page.getByRole('checkbox',{name:'Show deleted'}).count(),0);
 flags.show_deleted=true;restoreAllowed=false;await page.reload();assert.equal(await page.getByRole('checkbox',{name:'Show deleted'}).count(),0);restoreAllowed=true;
 // The Board cannot list deleted tasks, so its Filters must not offer Show deleted (only the task list does).
 await page.goto(`${base}/projects/SYN-RCV/board`);await page.getByRole('main').waitFor();await page.getByRole('button',{name:/^Filters/}).first().click();
 assert.equal(await page.getByRole('checkbox',{name:'Show deleted'}).count(),0,'Board offers no Show deleted toggle');await page.keyboard.press('Escape');
 // AC-RCV-17: the setting is visible and read-only once published; flag-off parity.
 await page.goto(`${base}/templates/${template}`);await page.getByLabel('Offsets count',{exact:true}).waitFor();assert.equal(await page.getByLabel('Offsets count',{exact:true}).isDisabled(),true);
 // The wizard previews with the chosen office and shows its one-line note only for a Working days template.
 const note='Offsets skip weekends and office holidays.',wizard=async()=>{await page.goto(`${base}/projects`);await page.getByRole('button',{name:'Create project',exact:true}).first().click();await page.locator('#p-office').selectOption(office);
  const previewed=page.waitForResponse(r=>r.url().endsWith(`templates/${template}/preview`));await page.locator('#p-template').selectOption(template);await previewed;await page.getByText(/^The template’s disciplines are ticked/).waitFor();await page.waitForTimeout(300);return page.getByText(note,{exact:true}).count()};
 assert.equal(await wizard(),1,'wizard note for Working days');assert.equal(requests.at(-1).body.officeId,office,'preview carries the office');
 flags.template_working_days=false;assert.equal(await wizard(),0,'no wizard note with the flag off');
 await page.goto(`${base}/templates/${template}`);await page.getByRole('main').waitFor();assert.equal(await page.getByLabel('Offsets count',{exact:true}).count(),0);
 // AC-RCV-18: exact segment labels/links, 4 of 10 issued; no bar with flag off.
 await page.goto(`${base}/projects/SYN-RCV/dashboard`);await page.getByText('4 of 10 issued',{exact:true}).waitFor();assert.match(await page.getByRole('link',{name:'In progress: 3',exact:true}).getAttribute('href'),/status=In\+Progress/);
 for(const [name,status] of [['Not started: 2',/status=Not\+Started/],['Issued: 4',/status=Issued,Accepted/],['On hold: 1',/status=On\+Hold/]]){const link=page.getByRole('link',{name,exact:true});assert.equal(await link.getAttribute('title'),name);assert.match(await link.getAttribute('href'),status,name)}const rollupAxe=await axe();
 flags.status_rollups=false;await page.reload();assert.equal(await page.getByText('4 of 10 issued',{exact:true}).count(),0);
 // AC-RCV-10/19/20: row links, group sum, URL and overdue column flag pair.
 flags.status_rollups=true;await page.goto(`${base}/portfolio`);await page.getByLabel('Group',{exact:true}).selectOption('client');await page.waitForURL(/group=client/);await page.locator('[data-portfolio-group]').waitFor();
 const subtotal=await page.locator('[data-portfolio-group]').innerText();assert.match(subtotal,/2 Projects/);assert.match(subtotal,/Red 1/);assert.match(subtotal,/Green 1/);
 // Column sums equal the rows: overdue 1+2, blocked 1+2, decisions 1+2, high issues 0, overdue deliverables 2+3, Critical 1+2; the count opens that client.
 assert.deepEqual((await page.locator('[data-portfolio-group] td').allInnerTexts()).map(x=>x.trim()).slice(0,6),['3','3','3','0','5','■ 3 · ▲ 0']);assert.match(await page.locator('[data-portfolio-group] th').getByRole('link',{name:'2 Projects'}).getAttribute('href'),new RegExp(`group=client.*clientId=${client}`));assert.match(await page.getByRole('row').filter({hasText:'Synthetic project 1'}).getByRole('link',{name:'1',exact:true}).first().getAttribute('href'),/overdue=true/);const portfolioAxe=await axe();
 flags.status_rollups=false;await page.reload();assert.equal(await page.getByLabel('Group',{exact:true}).count(),0);assert.equal(await page.getByRole('columnheader',{name:'Overdue deliverables',exact:true}).count(),0);
 // AC-RCV-11/12: definitions are available on keyboard-focusable tile actions, totals link to the entries.
 await page.goto(`${base}/staff`);await page.getByRole('button',{name:/Reviews waiting too long/}).waitFor();assert.ok(await page.getByRole('button',{name:/Reviews waiting too long/}).getAttribute('aria-describedby'));await page.getByRole('button',{name:/Reviews waiting too long/}).click();await page.waitForURL(/indicator=reviewsStalled/);
 await page.goto(`${base}/time?from=2026-10-12&to=2026-10-18`);await page.getByRole('heading',{name:'By project',exact:true}).waitFor();const totals=page.locator('aside');assert.match(await totals.getByRole('link',{name:'3 h',exact:true}).first().getAttribute('href'),/projectId=/);const timeAxe=await axe();
 assert.deepEqual(errors,[]);for(const [name,value]of Object.entries({deletedAxe,rollupAxe,portfolioAxe,timeAxe}))assert.deepEqual(value,[],name);
 const result={scope:'Mocked synthetic API in Chromium; no server transaction or permission evidence',requests,errors,unknown,deletedAxe,rollupAxe,portfolioAxe,timeAxe};fs.mkdirSync(path.resolve(__dirname,'../test-results/recovery-counts'),{recursive:true});fs.writeFileSync(path.resolve(__dirname,'../test-results/recovery-counts/report.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{await browser?.close();server?.kill()});
