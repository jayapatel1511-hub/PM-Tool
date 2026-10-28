// Packet 046: focused saved-view, capacity-preview and Portfolio/Planner browser workflows.
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const assert = require('node:assert/strict')
const { coordinationOptions, coordinationTask } = require('./reviews.fixture.cjs')
const { myWork, myWorkPerson, myStaffPerson, myStaffList } = require('./my-work.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const { startPreview, project: baseProject, task: baseTask, taskDetail, deliverableDetail, panelIds, mockPlatformApi } = require('./platform-fixtures.cjs')

const port = Number(process.env.WORKFLOW_TEST_PORT || '19508')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const projectId = baseProject.id
const personId = id(2), disciplineId = id(11), taskId = id(1), allocationId = id(40), viewId = id(41)

const project = { ...baseProject, permissions: { ...baseProject.permissions, proposeAllocation: { ok: true }, createDeliverableIn:[], needsReason: false }, disciplines: [{ id: disciplineId, name: 'Civil', isActive: true }] }
const task = { ...baseTask, id: taskId, projectId, projectDisciplineId: disciplineId, assigneeId: personId, assigneeName: 'Alex Chen', ownerId: personId, ownerName: 'Alex Chen', status: 'Not Started', rowVersion: 1, dueDate: '2026-10-28' }
const allocation = { id: allocationId, personId, personName: 'Alex Chen', purpose: 'Production', fromDate: '2026-10-19', throughDate: '2026-10-23', plannedHours: 40, status: 'Proposed', rowVersion: 1, personState: 'Eligible' }
const sharedView=savedView({id:id(43),name:'Shared allocation view',scope:'Project',params:{status:'Proposed',purpose:'Production'},canEdit:false})
const staffView = savedView({ id: viewId, name: 'Staff default', isDefault: true, params: { scope: 'all', inactive: '1' } })
const views = { allocations:[sharedView], staff: [staffView], portfolio: [savedView({ id: id(42), name: 'Portfolio personal', params: { status: 'Active' } })] }

;(async () => {
  const { base, child } = await startPreview(port)
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion:'reduce' })
  page.setDefaultTimeout(10000)
  const fallback = mockPlatformApi(page)
  const errors = [], unknownGets = [], writes = [], userQueries=[]
  let workload = true
  await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
  page.on('pageerror', e => {errors.push(e.message);console.error(e.message)})
  await page.route('**/api/**', async route => {
    const req = route.request(); const url = new URL(req.url()); const p = url.pathname.replace('/api/v1/', ''); const method = req.method(); let status = 200; let data = {}
    if (method !== 'GET') writes.push({ method, path: p, body: req.postDataJSON?.(), ifMatch: req.headers()['if-match'], source: req.headers()['x-hub-source'] })
    if (p === 'config') data = clientConfig()
    else if (p === 'me') data = { id: personId, displayName: 'Alex Chen', email: 'alex@hub.test', roles: [], systemRoles: ['ProjectManager'], capabilities: { createProject: false, createTask: true, portfolio: true, workload, staff: true, allStaff: true, admin: true, templates: true, readOnly: false, directReports: 1 }, settings: { today: '2026-10-18', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8, restrictedProjectsEnabled: true }, preferences: { denseRows: true }, flags: { small_ux: true, allocation_preview: true, org_structure: true } }
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }
    else if (p === 'me/notifications/pulse') data = { stamp: '0' }
    else if (p === 'me/work') data = myWork({ person: myWorkPerson(personId, 'Alex Chen'), today: '2026-10-18', tasks: [task] })
    else if (p === 'reference') data = { disciplines: [{ id: disciplineId, name: 'Civil', isActive: true }], clients: [], offices: [], phases: [], projectTypes: [], limits: { roleLabel: 80 } }
    else if (p === 'organisation') data = { includeIndirectReports: false, units: [] }
    else if (p === 'workspaces') data=[]
    else if (p === 'users') {userQueries.push(url.searchParams.get('projectId'));data=[{id:personId,displayName:'Alex Chen',isActive:true,onProject:url.searchParams.has('projectId')}]}
    else if (p === `tasks/${taskId}`) data={...taskDetail,task}
    else if (p === `deliverables/${panelIds.deliverable}`) data={...deliverableDetail,permissions:{...deliverableDetail.permissions,edit:{ok:true}}}
    else if (p === 'views' && method==='POST') { status = 201; data = viewSaved(id(44)) }
    else if (p === 'views') { const listType = url.searchParams.get('listType'); data = viewList(views[listType] ?? []) }
    else if (p.startsWith('views/') && method==='PATCH') data = viewSaved(p.slice('views/'.length), 2)
    else if (p.startsWith('views/') && method==='DELETE') { status = 204; data = undefined }
    else if (p === `projects/${projectId}` || p === `projects/${project.projectNumber}`) data = project
    else if (p === `projects/${projectId}/allocations` || p === `projects/${project.projectNumber}/allocations`) data = { items: [allocation], page: 1, pageSize: 50, totalCount: 1 }
    else if (p === `projects/${projectId}/allocations/${allocationId}`) data = { ...allocation, personEligible: true, links: [{ workType: 'Task', workId: taskId, workDate: '2026-10-20' }], days: [], overCapacityWarningRecorded: false, canConfirm: true, canManage: true }
    else if (p === `projects/${projectId}/allocations/${allocationId}/confirmation-preview`) data = { id: allocationId, rowVersion: 1, days: [1, 2, 3, 4, 5].map((n, i) => ({ date: `2026-10-${19 + i}`, availableHours: 8, confirmedHours: 0, proposedHours: i < 2 ? 10 : 6, resultingHours: i < 2 ? 10 : 6, overByHours: i < 2 ? 2 : 0, dateVersion: 1 })) }
    else if (p === `projects/${projectId}/allocations/review-options`) data = []
    else if (p === `projects/${projectId}/open-roles`) data = []
    else if (p === `projects/${projectId}/changes/options`) data = coordinationOptions({ actorId:personId,people:[{id:personId,displayName:'Alex Chen'}],disciplines:[{id:disciplineId,name:'Civil'}],sources:[],heads:[], tasks: [coordinationTask({ id: task.id, key: task.key, name: task.name, projectDisciplineId: task.projectDisciplineId, ownerId: task.assigneeId, rowVersion: task.rowVersion, status: task.status })], deliverables: [], manageDisciplineIds: [], canCreate: { changes: false, reviews: false }, canWrite: true })
    else if (p === `projects/${projectId}/tasks`) data = { items: [task], page: 1, pageSize: 200, totalCount: 1 }
    else if (p === `projects/${projectId}/deliverables` || p === `projects/${projectId}/milestones`) data = []
    else if (p === 'portfolio') data = { ownOnly: false, tiles: { active: 1, red: 0, yellow: 0, green: 1, submissions14: 0, overdueDecisions: 0 }, projects: [{ id: projectId, projectNumber: project.projectNumber, name: project.name, status: 'Active', client: { id: id(50), name: 'Synthetic client' }, pm: { id: personId, displayName: 'Alex Chen' }, officeId: null, office: null, phase: null, projectTypeId: null, projectType: null, health: { computed: 'Green', reported: 'Green' }, trend: [], overdueDeliverables: 0, overdueTasks: 0, blockedTasks: 0, decisionsOverdue: 0, highIssues: 0, attention: 0 }] }
    else if (p === 'staff') data = myStaffList([myStaffPerson({ id: personId, displayName: 'Alex Chen', roles: { pm: 1, dl: 0, team: 1, reviewer: 0, viewer: 0 }, open: 1 })], { scope: 'all', canSeeAll: true })
    else if (p.startsWith('staff/')) data = []
    else if (p === 'planning/grid') data = { weeks: [], currentWeek: '2026-10-12', evaluatedAt: '2026-10-18T00:00:00Z', partialView: false, draftAccess: false, settings: { horizonWeeks: 6, overPct: 110, underPct: 40, underWeeks: 1, staleDays: 14, maxHoursPerWeek: 40 }, people: [] }
    else if (method !== 'GET') data = {}
    else {await route.fallback();return}
    await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
  })

  try {
    await page.goto(`${base}/staff`)
    await page.waitForURL(url=>url.searchParams.get('view')===viewId)
    assert.equal(new URL(page.url()).searchParams.get('scope'),'all');assert.equal(new URL(page.url()).searchParams.get('inactive'),'1')
    await page.goto(`${base}/staff?scope=direct&inactive=0`)
    await page.getByRole('heading',{name:/Staff/i}).waitFor()
    await page.getByRole('button',{name:'Views',exact:true}).click()
    assert.equal(new URL(page.url()).searchParams.get('scope'),'direct');assert.equal(new URL(page.url()).searchParams.get('inactive'),'0');assert.equal(new URL(page.url()).searchParams.has('view'),false)
    await page.getByRole('menuitem',{name:/Staff default/}).click()
    await page.waitForURL(url=>url.searchParams.get('view')===viewId)
    assert.equal(new URL(page.url()).searchParams.get('scope'),'all');assert.equal(new URL(page.url()).searchParams.get('inactive'),'1')

    await page.goto(`${base}/projects/${project.projectNumber}/allocations?view=${sharedView.id}&status=Proposed&purpose=Production`)
    await page.getByRole('button',{name:sharedView.name,exact:true}).click()
    await page.getByRole('menuitem',{name:'Copy as personal view',exact:true}).click()
    const sharedBefore=JSON.stringify(sharedView)
    const dialog=page.getByRole('dialog'); await dialog.getByLabel('Name',{exact:true}).fill('Copied shared allocation view');await dialog.getByRole('button',{name:'Save',exact:true}).click()
    await dialog.waitFor({state:'hidden'})
    const copy=writes.find(w=>w.path==='views'&&w.method==='POST')
    assert.equal(copy.body.scope,'Personal');assert.deepEqual(copy.body.params,sharedView.params);assert.equal(copy.body.isDefault,false)
    assert.equal(JSON.stringify(sharedView),sharedBefore);assert.equal(writes.filter(w=>w.path===`views/${sharedView.id}`).length,0)
    assert.equal(copy.source,'UI');assert.equal(copy.ifMatch,undefined);assert.equal(copy.body.projectId,projectId);assert.equal(copy.body.listType,'allocations') // the typed save carries the project and the source

    // A personal view is updated, made the default and deleted: the version it was read at goes in If-Match, every write names its source, and each is the one request.
    const personal=views.portfolio[0]
    await page.goto(`${base}/portfolio?view=${personal.id}&status=Active`)
    const viewWrite=async(item,method)=>{
      await page.getByRole('button',{name:personal.name,exact:true}).click()
      const done=page.waitForResponse(r=>r.request().method()===method&&r.url().endsWith(`/api/v1/views/${personal.id}`))
      await page.getByRole('menuitem',{name:item}).click();await done
      return writes.filter(w=>w.path===`views/${personal.id}`&&w.method===method).at(-1)
    }
    const update=await viewWrite(`Update "${personal.name}" with current filters`,'PATCH')
    assert.equal(update.body.params.status,'Active');assert.equal(update.ifMatch,'"1"');assert.equal(update.source,'UI')
    const makeDefault=await viewWrite('Open this list in this view','PATCH')
    assert.deepEqual(makeDefault.body,{isDefault:true});assert.equal(makeDefault.ifMatch,'"1"');assert.equal(makeDefault.source,'UI')
    const removed=await viewWrite('Delete this view','DELETE')
    assert.equal(removed.ifMatch,undefined);assert.equal(removed.source,'UI');await page.waitForURL(url=>!url.searchParams.has('view'))

    // Manual board order: a card moved with the keyboard saves its lane's new order through the typed PUT (source header, no If-Match), and the board asks for the order it saved.
    const cards=['a','b','c'].map((k,i)=>({...baseTask,id:id(60+i),key:`T-00${i+1}`,name:`Card ${k}`,lane:'To Do',disciplineName:'Civil',disciplineCode:'CIV',disciplineColour:'#2563eb',disciplineOrder:1,projectNumber:project.projectNumber,projectName:project.name,predecessorCount:0,successorCount:0,commentCount:0,assigneeActive:true,createdAt:'2026-10-01T00:00:00Z',lastActivityAt:'2026-10-01T00:00:00Z'}))
    const boardTasks=route=>route.fulfill({json:{items:cards,page:1,pageSize:200,totalCount:cards.length}})
    const puts=[]
    const boardOrder=route=>{const r=route.request();puts.push({path:new URL(r.url()).pathname,body:r.postDataJSON(),ifMatch:r.headers()['if-match'],source:r.headers()['x-hub-source']});return route.fulfill({status:204})}
    await page.route(/\/api\/v1\/projects\/[^/]+\/tasks\?/,boardTasks);await page.route(/\/api\/v1\/projects\/[^/]+\/board-order$/,boardOrder)
    await page.goto(`${base}/projects/${project.projectNumber}/board?sort=board`)
    await page.getByRole('button',{name:/^Move.*Card a/}).waitFor()
    const saved=page.waitForResponse(r=>r.request().method()==='PUT'&&r.url().endsWith(`/api/v1/projects/${projectId}/board-order`))
    await page.getByRole('button',{name:/^Move.*Card a/}).focus()
    for(const key of ['Space','ArrowDown','Space']){await page.keyboard.press(key);await page.waitForTimeout(250)} // the keyboard sensor takes each step on its own frame
    await saved
    assert.equal(puts.length,1);const order=puts[0];assert.equal(order.path,`/api/v1/projects/${projectId}/board-order`)
    assert.deepEqual(order.body,{taskIds:[cards[1].id,cards[0].id,cards[2].id]});assert.equal(order.source,'UI');assert.equal(order.ifMatch,undefined)
    await page.unroute(/\/api\/v1\/projects\/[^/]+\/tasks\?/,boardTasks);await page.unroute(/\/api\/v1\/projects\/[^/]+\/board-order$/,boardOrder)

    await page.goto(`${base}/portfolio`); await page.getByRole('heading', { name: 'Portfolio', exact: true }).waitFor()
    const plannerLink = page.getByRole('link', { name: /capacity.*discipline|planner/i }).first(); assert.equal(await plannerLink.count(), 1); assert.match(await plannerLink.getAttribute('href'), /^\/planner\?/)
    workload = false; await page.reload(); await page.getByRole('heading', { name: 'Portfolio', exact: true }).waitFor(); assert.equal(await page.getByRole('link', { name: /capacity.*discipline|planner/i }).count(), 0)
    workload = true
    await page.reload();await page.getByRole('heading',{name:'Portfolio',exact:true}).waitFor()
    await page.getByRole('button',{name:/^Filters/}).click()
    await page.locator('#pf-pm').click();await page.getByRole('option',{name:/Alex Chen/}).waitFor()
    assert.equal(userQueries.at(-1),null,'Portfolio picker has no project context')
    await page.keyboard.press('Escape');await page.keyboard.press('Escape')
    for(const [kind,itemId,label,title] of [['Task',taskId,'Assignee',task.name],['Deliverable',panelIds.deliverable,'Owner','Synthetic deliverable']]) {
      await page.goto(`${base}/my-work?panel=${kind}:${itemId}`)
      const panel=page.getByRole('dialog');await panel.getByRole('heading',{name:title,exact:true}).waitFor()
      await panel.getByText(label,{exact:true}).locator('..').getByRole('button').first().click()
      await page.getByRole('option',{name:/Alex Chen.*On this project/}).waitFor()
      assert.equal(userQueries.at(-1),projectId,`${kind} picker has project context`)
      await page.keyboard.press('Escape')
    }

    await page.goto(`${base}/projects/${project.projectNumber}/allocations`); await page.waitForTimeout(2000); await page.getByRole('button', { name: /Alex Chen/ }).waitFor(); await page.getByRole('button', { name: /Alex Chen/ }).click(); await page.getByRole('button', { name: 'Review capacity', exact: true }).click(); await page.getByText(/2 of 5 dates exceed available capacity by/i).waitFor(); assert.equal(await page.getByRole('dialog').locator('table tbody tr').count(), 5)
    await page.evaluate(async()=>Promise.all(document.getAnimations().filter(a=>a.effect?.getTiming().iterations!==Infinity).map(a=>a.finished.catch(()=>{}))))
    await page.addScriptTag({path:require.resolve('axe-core/axe.min.js')})
    const axeViolations=await page.evaluate(async()=>(await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}})).violations.map(v=>({id:v.id,targets:v.nodes.map(n=>n.target)})))
    assert.deepEqual(axeViolations,[])
    assert.deepEqual(errors, []); assert.deepEqual(unknownGets, []);assert.deepEqual(fallback.unknownGets,[])
    console.log(JSON.stringify({ scope: 'Chromium mocked workflows',staffDefaultAndRestore:true,sharedViewCopy:true,personalViewWrites:true,boardOrderWrite:true,taskAndDeliverableProjectPickers:true,portfolioPickerUnscoped:true,allocationTwoOfFive:true,portfolioPlannerAccess:true,axeViolations, posts: writes.length, errors, unknownGets }))
  } finally { await browser.close(); child.kill() }
})().catch(e => { console.error(e); process.exitCode = 1 })
