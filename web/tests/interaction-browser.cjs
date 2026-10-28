// Packet 046: focused browser checks against deterministic mocked API contracts.
const { chromium } = require('playwright')
const { clientConfig } = require('./config.fixture.cjs')
const { savedView, viewList } = require('./views.fixture.cjs')
const { meView, referenceData, referenceDiscipline, referenceDeliverableType } = require('./me.fixture.cjs')
const assert = require('node:assert/strict')
const { spawn } = require('node:child_process')
const path = require('node:path')
const fs = require('node:fs')

const port = process.env.INTERACTION_TEST_PORT || '19505'
const base = `http://127.0.0.1:${port}`
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const pid = id(1), person = id(2), discipline = id(3), taskId = id(4), deliverableId = id(5), milestoneId = id(6)
const flags = { small_ux: true, row_quick_actions: true, date_context: true, timeline_handoffs_chain: true }
const writes = []
const dateContextRequests = []
let viewerMode = false, renameConflict = false, releaseTransition = null, delayedTransition = false, related = false
const project = { id: pid, projectNumber: 'SYN-046', name: 'Synthetic interaction project', client: 'Synthetic client', pmName: 'Synthetic PM', status: 'Active', visibility: 'Open', rowVersion: 1, links: [], disciplines: [{ id: discipline, name: 'Civil', code: 'C', colour: '#467762', isActive: true, leadUserId:person, leadName:'Synthetic PM' }], starred: false, myRoles: ['PM'], startDate: '2026-10-01', targetCompletionDate: '2026-11-30', health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] }, permissions: { edit: { ok: true }, healthOverride: { ok: false }, reviewDates: { ok: false }, snooze: { ok: false }, isPm: true, leadOf: [], createTaskIn: [discipline], createDeliverableIn: [discipline], transitions: [], manageMilestones: true } }
const task = { milestoneId, id: taskId, projectId: pid, key: 'SYN-046-T001', name: 'Synthetic task', projectDisciplineId: discipline, disciplineName: 'Civil', disciplineColour: '#467762', status: 'Not Started', priority: 'Medium', progressPct: 0, startDate: '2026-10-20', dueDate: '2026-10-30', originalDueDate: null, dueDateChangeCount: 0, createdAt: '2026-10-18', lastActivityAt: '2026-10-18T12:00:00Z', assigneeId: person, assigneeName: 'Synthetic PM', assigneeActive: true, reviewerId: null, reviewerName: null, requiresReview: false, collaboratorIds: [], deliverableId, deliverableKey: 'SYN-046-D001', deliverableName: 'Synthetic deliverable', deliverableOrder: 1, rowVersion: 2, reviewRound: 0, can: { edit: true, assign: true, changeDue: true, block: true }, state: { isOverdue: false, isDueSoon: false, isBlocked: false, isWaiting: false, isBlocking: false, isStale: false, isUnassigned: false, isMissingDueDate: false, isDateInconsistent: false, isInactiveOwner: false, isHeldPastDue: false, isReviewStalled: false, blockingCount: 0, blockedBy: [], notes: [], inconsistencyDetail: [] }, manualBlockType: null }
const deliverable = { id: deliverableId, projectId: pid, key: 'SYN-046-D001', name: 'Synthetic deliverable', projectDisciplineId: discipline, disciplineName: 'Civil', status: 'In Progress', priority: 'Medium', startDate: '2026-10-20', dueDate: '2026-10-30', originalDueDate: null, rowVersion: 2, canEditDates: true, canRebaseline: true, baselineDueDate: '2026-10-28', sinceBaselineDays: 2, tasks: [{ id: taskId, key: task.key, name: task.name, assignee: 'Synthetic PM', dueDate: task.dueDate, status: task.status }], state: { progressPct: 0, taskOpen: 1, taskOverdue: 0, taskBlocked: 0 }, permissions: { edit: { ok: true }, issue: false, transitions: [] }, issues: [], derivedPredecessors: [], derivedSuccessors: [] }
const milestone = { id: milestoneId, key: 'SYN-046-M01', name: 'Synthetic review', milestoneType: 'Review', date: '2026-11-01', originalDate: '2026-10-29', isComplete: false, isCancelled: false, canRebaseline: true, baselineDate: '2026-10-29', sinceBaselineDays: 3, rowVersion: 2, projectId: pid, deliverableIssued: 0, deliverableTotal: 1, taskComplete: 0, taskTotal: 1, statusReasons: [], deliverables: [] }
const me = meView({ id: person, displayName: 'Synthetic PM', email: 'pm@hub.test', capabilities: { createProject: true, createTask: true, portfolio: true, workload: true, staff: true, allStaff: true, templates: true }, settings: { today: '2026-10-18', chainDepthLimit: 20 }, flags })
const taskDetail = { task, project: { id: pid, projectNumber: project.projectNumber, name: project.name, status: project.status }, collaborators: [], watchers: [], permissions: { edit: { ok: true }, assign: { ok: true }, setReviewer: { ok: true }, dueDate: { ok: true }, dueNeedsReason: false, block: { ok: true }, delete: { ok: true }, restore: false, comment: false, transitions: [{ to: 'In Progress', allowed: true, needsReason: false }], isReviewer: false, dependencies: false, enterTime: false, needsReason: false, allowSelfReview: false }, description: 'Synthetic detail' }
const { projectTimeline, ganttPage } = require('./timeline.fixture.cjs')
const timeline = projectTimeline({ discipline, task: taskId, deliverable: deliverableId, milestone: milestoneId, handoff: id(7) })

const { activityPage } = require('./activity.fixture.cjs')
const { startReadiness } = require('./task-start.fixture.cjs')
const activity = activityPage(id, { actorId: person, itemId: taskId, itemKey: task.key, itemName: task.name })
let server, browser
;(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' })
  for (let i = 0; i < 80; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  page.setDefaultTimeout(10000)
  const errors = []; const unknown = []
  const untested = []
  page.on('console', m => { if (m.type() === 'error') console.error(`browser console: ${m.text()}`) })
  page.on('pageerror', e => errors.push(e.message))
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'pm@hub.test'))
  await page.route('**/api/**', async route => {
    const req = route.request(); const u = new URL(req.url()); const p = u.pathname.replace('/api/v1/', ''); const method = req.method(); let data = {}
    if (p === `items/Task/${taskId}/date-context`) dateContextRequests.push(u.search)
    if (p === `tasks/${taskId}` && method === 'PATCH' && renameConflict) { writes.push({ method, path: p, body: req.postDataJSON() }); await route.fulfill({status:409,contentType:'application/json',body:JSON.stringify({code:'concurrency_conflict',detail:'Synthetic rename conflict'})}); return }
    if (p === 'config') data = clientConfig()
    else if (p === 'me') data = me
    else if (p === 'me/sign-in') data = {}
    else if (p.startsWith('me/notifications/')) data = p.endsWith('unread-count') ? { notifications: 0, following: 0 } : { stamp: '0' }
    else if (p === 'workspaces') data = []
    else if (p === 'views') data = viewList([savedView({ id: id(8), name: 'My saved view', params: { group: 'status' } })])
    else if (p === 'reference') data = referenceData({ disciplines: [referenceDiscipline(discipline, 'Civil')], deliverableTypes: [referenceDeliverableType(id(60), 'Synthetic drawing')] })
    else if (p === `projects/${pid}/activity` || p === `items/Task/${taskId}/activity`) data = activity
    else if (p === 'users') data = [{ id: person, displayName: 'Synthetic PM', isActive: true }]
    else if (p === `projects/${pid}` || p === `projects/${project.projectNumber}`) data = viewerMode ? { ...project, status: 'Archived', permissions: { ...project.permissions, edit: { ok: false }, createTaskIn: [], createDeliverableIn: [] } } : project
    else if (p === `projects/${pid}/tasks`) data = { items: [viewerMode ? { ...task, can: { edit: false, assign: false, changeDue: false, block: false } } : task], totalCount: 1, page: 1, pageSize: 200 }
    else if (p === `projects/${pid}/deliverables` && method === 'POST') { writes.push({method,path:p,body:req.postDataJSON()}); related=true; data={id:deliverableId,key:deliverable.key,warnings:[]} }
    else if (p === `projects/${pid}/deliverables`) data = [deliverable]
    else if (p === `projects/${pid}/milestones`) data = [milestone]
    else if (p === `tasks/${taskId}` && method === 'GET') data = {...taskDetail,relatedDeliverables:related?[{id:deliverableId,key:deliverable.key,name:deliverable.name}]:[]}
    else if (p === `tasks/${taskId}/start-readiness`) data = startReadiness({ taskId, key: task.key })
    else if (p === `tasks/${taskId}/dependencies`) data = { dependsOn: [], blocks: [] }
    else if (p === `items/Task/${taskId}/links` || p === `items/Deliverable/${deliverableId}/links`) data = { links: [], inherited: [], canAdd: false }
    else if (p === `items/Task/${taskId}/comments` || p === `items/Deliverable/${deliverableId}/comments`) data = { items: [], canComment: false }
    else if (p === `deliverables/${deliverableId}`) data = { relatedTasks:related?[{id:taskId,key:task.key,name:task.name}]:[], deliverable:viewerMode?{...deliverable,canRebaseline:false}:deliverable, project, tasks: deliverable.tasks, issues: [], derivedPredecessors: [], derivedSuccessors: [], permissions: deliverable.permissions }
    else if (p === `milestones/${milestoneId}`) data = { milestone, permissions: { manage: { ok: true } }, deliverables: [], completesPhase: null }
    else if (p === `milestones/${milestoneId}/history` || p === `deliverables/${deliverableId}/history` || p === `tasks/${taskId}/history`) data = []
    else if (p === 'timeline') data = ganttPage(project, timeline)
    else if (p === 'projects') data = [project]
    else if (p === `items/Deliverable/${deliverableId}/date-context`) data = {dates:{inconsistencies:[],newInconsistencies:[{text:'Synthetic new date inconsistency'}],successors:[{id:id(61),key:'SYN-046-T061',reason:{text:'Synthetic successor moves out of sequence'}}]},tasks:[{id:taskId,key:task.key,reason:{text:'Synthetic task lies outside deliverable dates'}}]}
    else if (p === `projects/${pid}/timeline`) data = timeline
    else if (p === `projects/${pid}/date-review`) data = { window: null, tasks: [], deliverables: [] }
    else if (p === `items/Task/${taskId}/date-context`) data = { dates: { earliestStart: { date: '2026-10-22' }, earliestDue: { date: '2026-11-03' }, latestDue: { date: '2026-11-10' }, inconsistencies: [], successors: [], newInconsistencies: [] } }
    else if (p.endsWith('/follow')) data = { level: 'My items only', source: 'Assignment' }
    else if (p === `tasks/${taskId}/transition`) { if (delayedTransition) await new Promise(resolve => { releaseTransition = resolve }); writes.push({ method, path: p, body: req.postDataJSON?.() }); data = delayedTransition ? { code:'forbidden',detail:'Synthetic transition refusal' } : { code: 'start_authorisation_required', detail: 'A start authorisation is required.' } }
    else if (method === 'POST' || method === 'PATCH' || method === 'DELETE') { writes.push({ method, path: p, body: req.postDataJSON?.() }); data = p === `tasks/${taskId}` ? { rowVersion: 3 } : {} }
    else { unknown.push(p); data = {} }
    await route.fulfill({ status: p === `tasks/${taskId}/transition` ? 403 : method === 'POST' ? 201 : 200, contentType: 'application/json', body: JSON.stringify(data) })
  })

  // Row actions, saved-view deep link, and flag parity.
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`)
  if (process.env.DEBUG_INTERACTION) console.error((await page.locator('body').innerText()).slice(0, 2000))
  await page.getByText(task.name, { exact: true }).first().waitFor()
  assert.equal(await page.getByRole('button', { name: `Open ${task.key}`, exact: true }).count(), 1)
  const row = page.getByRole('row').filter({ hasText: task.key }).last(); const open = page.getByRole('button', { name: `Open ${task.key}`, exact: true }); const actionBox = open.locator('..')
  assert.equal(await actionBox.evaluate(el => getComputedStyle(el).opacity), '0'); await row.hover(); assert.equal(await actionBox.evaluate(el => getComputedStyle(el).opacity), '1'); await open.focus(); assert.equal(await actionBox.evaluate(el => getComputedStyle(el).opacity), '1')
  await page.getByRole('button', { name: `Open ${task.key}`, exact: true }).click(); await page.getByRole('heading', { name: task.name, exact: true }).waitFor()
  assert.equal(new URL(page.url()).searchParams.get('panel')?.startsWith('Task:'), true)
  await page.getByRole('button', { name: 'Close', exact: true }).click()
  viewerMode = true; await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`); await page.getByText(task.name, { exact: true }).first().waitFor(); assert.equal(await page.getByRole('button', { name: `Open ${task.key}`, exact: true }).count(), 1); assert.equal(await page.getByRole('button', { name: `Start ${task.key}`, exact: true }).count(), 0)
  assert.equal(await page.getByRole('button', { name: `Rename ${task.key}`, exact: true }).count(), 0)
  assert.equal(await page.getByLabel(`Assignee of ${task.key}`, { exact: true }).count(), 0)
  assert.equal(await page.locator(`button[title="Due date of ${task.key}"]`).count(), 0)
  viewerMode = false
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`); await page.getByText(task.name, { exact: true }).first().waitFor()
  const nameWrites = () => writes.filter(x => x.method === 'PATCH' && x.path === `tasks/${taskId}` && x.body?.name)
  const rename = page.getByRole('button', { name: `Rename ${task.key}`, exact: true }); assert.equal(await rename.count(), 1, 'inline name editor is exposed'); await rename.click(); const nameInput = page.getByRole('textbox', { name: 'Name', exact: true }); await nameInput.press('Escape'); assert.equal(nameWrites().length, 0); await rename.click(); await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Synthetic task renamed'); await page.getByRole('textbox', { name: 'Name', exact: true }).press('Enter'); await page.waitForTimeout(100); assert.equal(nameWrites().length, 1)
  const startBefore = writes.filter(x => x.path === `tasks/${taskId}/transition`).length; await page.getByRole('button', { name: `Start ${task.key}`, exact: true }).click(); await page.getByRole('dialog').getByRole('status').waitFor(); assert.equal(await page.getByRole('row').filter({ hasText: task.key }).getByText(/Not Started/).count(), 1); assert.ok(writes.some(x => x.path === `tasks/${taskId}/transition`), JSON.stringify(writes)); assert.equal(writes.filter(x => x.path === `tasks/${taskId}/transition`).length, startBefore + 1); await page.getByRole('button', { name: 'Cancel', exact: true }).click()
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?view=My%20saved%20view`); await page.getByText(task.name, { exact: true }).first().waitFor()
  // A task creator may edit but not block (ManualBlock is PM, Discipline Lead or owner), so Block follows can.block.
  task.can = { ...task.can, block: false }; await page.reload(); await page.getByText(task.name, { exact: true }).first().waitFor()
  assert.equal(await page.getByRole('button', { name: `Block ${task.key}`, exact: true }).count(), 0, 'no Block action without can.block')
  task.can = { ...task.can, block: true }
  flags.row_quick_actions = false; await page.reload(); assert.equal(await page.getByRole('button', { name: `Open ${task.key}`, exact: true }).count(), 0)
  flags.row_quick_actions = true

  // Empty and stale names preserve the existing task; optimistic status is visible until a refusal arrives.
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`)
  await page.getByRole('button', { name: `Rename ${task.key}`, exact: true }).click()
  const emptyWrites = nameWrites().length
  await page.getByRole('textbox', { name: 'Name', exact: true }).fill('   ')
  await page.getByRole('textbox', { name: 'Name', exact: true }).press('Enter')
  await page.getByRole('row').filter({ hasText: task.key }).getByText(task.name, { exact: true }).waitFor()
  assert.equal(nameWrites().length, emptyWrites, 'blank name never reaches the API')
  await page.reload()
  renameConflict = true
  await page.getByRole('button', { name: `Rename ${task.key}`, exact: true }).click()
  await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Stale synthetic name')
  await page.getByRole('textbox', { name: 'Name', exact: true }).press('Enter')
  await page.getByText('Synthetic rename conflict', { exact: true }).first().waitFor()
  const conflictedRow = page.getByRole('row').filter({hasText:task.key})
  await conflictedRow.getByText('Synthetic task', {exact:true}).waitFor()
  assert.equal(await conflictedRow.getByText('Stale synthetic name', {exact:true}).count(), 0, 'rejected draft is not displayed as the saved name')
  await conflictedRow.getByText('Synthetic task', {exact:true}).click()
  assert.equal(await page.getByRole('textbox', {name:'Name',exact:true}).inputValue(), 'Stale synthetic name', 'rejected draft remains available for correction')
  await page.getByRole('textbox', {name:'Name',exact:true}).press('Escape')
  renameConflict = false
  task.status = 'In Progress'; taskDetail.permissions.transitions = [{ to:'Complete',allowed:true,needsReason:false }]
  await page.reload()
  delayedTransition = true
  await page.getByRole('button', { name: /Change status.*SYN-046-T001|SYN-046-T001.*In Progress/ }).click()
  await page.getByRole('menuitem', {name:'Complete', exact:true}).click()
  await page.getByRole('row').filter({hasText:task.key}).getByRole('status').filter({hasText:'Saving'}).waitFor()
  assert.match(await page.getByRole('row').filter({hasText:task.key}).innerText(), /Complete/)
  assert.ok(releaseTransition, 'request is waiting for the synthetic server')
  releaseTransition()
  await page.getByText('Synthetic transition refusal', {exact:true}).first().waitFor()
  assert.match(await page.getByRole('row').filter({hasText:task.key}).innerText(), /In Progress/)
  delayedTransition = false; task.status = 'Not Started'; taskDetail.permissions.transitions = [{to:'In Progress',allowed:true,needsReason:false}]

  // The row action uses the existing block dialog and requires its reason.
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`)
  await page.getByRole('button',{name:`Block ${task.key}`,exact:true}).click()
  const block=page.getByRole('dialog')
  assert.equal(await block.getByRole('button',{name:'Set block',exact:true}).isDisabled(),true)
  await block.getByLabel('Reason',{exact:true}).fill('Synthetic client input outstanding')
  await block.getByRole('button',{name:'Set block',exact:true}).click()
  await block.waitFor({state:'hidden'})
  assert.deepEqual(writes.at(-1).body,{type:'Client',reason:'Synthetic client input outstanding',rowVersion:2})

  // Draft date hint comes from the date-context contract before any save.
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none&panel=Task:${taskId}`); await page.getByRole('heading', { name: task.name, exact: true }).waitFor()
  const dialogTask = page.getByRole('dialog'); const startRow = dialogTask.getByText('Start date', { exact: true }).locator('..'); await startRow.locator('button').click(); const startInput = page.getByLabel('Start date', { exact: true }); await startInput.fill('2026-10-22'); await page.getByRole('dialog').getByText(/Oct 22, 2026|2026-10-22|22 Oct 2026/).waitFor(); assert.ok(dateContextRequests.some(q => /startDate=2026-10-22/.test(q)), JSON.stringify(dateContextRequests)); const writesBeforeClear = writes.length; await startInput.fill(''); await page.getByRole('dialog').getByText(/Nov 3, 2026|2026-11-03|3 Nov 2026/).waitFor(); assert.ok(dateContextRequests.some(q => /startDate=(&|$)/.test(q)), JSON.stringify(dateContextRequests)); await startInput.press('Escape'); assert.equal(writes.length, writesBeforeClear)

  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none&panel=Task:${taskId}`)
  await page.getByRole('heading', {name:task.name,exact:true}).waitFor()
  const dateWritesBefore = writes.length
  await startRow.locator('button').click()
  await page.getByLabel('Start date', {exact:true}).fill('2026-10-21')
  await page.getByRole('dialog').getByText(/Oct 22, 2026|2026-10-22|22 Oct 2026/).waitFor()
  await page.getByLabel('Start date', {exact:true}).press('Enter')
  await page.getByLabel('Start date', {exact:true}).waitFor({state:'hidden',timeout:10000})
  assert.equal(writes.length, dateWritesBefore + 1, 'inconsistent date hint does not prevent saving')
  assert.equal(writes.at(-1).body.startDate, '2026-10-21')

  // Re-baseline requires a reason and sends the explicit clear/set intent.
  await page.goto(`${base}/projects/${project.projectNumber}/deliverables?panel=Deliverable:${deliverableId}`); await page.getByRole('button', { name: 'Re-baseline', exact: true }).waitFor(); await page.getByRole('button', { name: 'Re-baseline', exact: true }).click(); const dialog = page.getByRole('dialog'); assert.equal(await dialog.getByRole('button',{name:'Confirm',exact:true}).isDisabled(),true); await dialog.getByRole('textbox').fill('Synthetic baseline reason'); await dialog.getByRole('button', { name: 'Confirm', exact: true }).click(); await page.waitForTimeout(50); assert.deepEqual(writes.at(-1).body, { reason: 'Synthetic baseline reason', clear: false, rowVersion: 2 })

  // Timeline handoff links and chain highlight are URL/UI state only; no writes on filter interaction.
  await page.goto(`${base}/projects/${project.projectNumber}/timeline?chainTo=${milestoneId}`); await page.getByRole('status').filter({ hasText: '1 of 1 tasks' }).waitFor(); assert.equal(new URL(page.url()).searchParams.get('chainTo'), milestoneId)
  const handoff = page.getByRole('link', { name: /SYN-046-H001.*Civil.*Civil/ }).first(); await handoff.waitFor(); assert.match(await handoff.getAttribute('href'), /handoffs\?panel=Handoff:/); assert.equal(writes.filter(x => x.path.includes('timeline') || x.path.includes('handoff')).length, 0)

  await page.getByText('Planned completion after target',{exact:true}).waitFor()
  assert.match(await page.locator('abbr').filter({hasText:'Why?'}).getAttribute('title'),/latest planned date.*does not change project health/)
  await page.getByText('Promised 2 d after needed',{exact:false}).waitFor()
  await page.getByText('3 d overdue',{exact:false}).waitFor()
  const handoffRow=page.getByRole('row').filter({has:page.getByRole('link',{name:/SYN-046-H001.*Civil.*Civil/})})
  assert.equal(await handoffRow.locator('button[style*="cursor: grab"]').count(),0)
  assert.equal(await handoffRow.locator('a[aria-label*="SYN-046-H001"]').count(),2)

  // Four nearby changes form one expandable group; the entry eleven minutes earlier remains separate.
  await page.goto(`${base}/projects/${project.projectNumber}/activity`)
  await page.getByRole('checkbox',{name:'Group changes',exact:true}).waitFor()
  assert.equal(await page.getByRole('checkbox',{name:'Group changes',exact:true}).isChecked(),false)
  assert.equal(await page.getByRole('main').locator('tbody tr').count(),5)
  await page.getByRole('checkbox',{name:'Group changes',exact:true}).click()
  await page.getByText(/Synthetic PM made 4 changes/).waitFor()
  assert.equal(new URL(page.url()).searchParams.get('groupChanges'),'true')
  await page.getByText(/Synthetic PM made 4 changes/).click()
  await page.getByText(/Synthetic PM made 1 changes/).click()
  for(let i=0;i<5;i++) assert.ok((await page.getByRole('main').innerText()).includes(`After ${i}`),`expanded activity ${i} remains visible`)

  // Original and PM-baseline ghosts obey the server's threshold decisions independently.
  deliverable.originalStartDate='2026-10-20'; deliverable.originalDueDate='2026-10-28'
  Object.assign(timeline.deliverables[0],{originalStartDate:'2026-10-20',originalDueDate:'2026-10-28',slipShown:false,baselineSlipShown:false})
  milestone.slipShown=false; milestone.baselineSlipShown=false
  Object.assign(timeline.milestones[0],{slipShown:false,baselineSlipShown:false})
  await page.goto(`${base}/projects/${project.projectNumber}/timeline`)
  await page.getByRole('heading',{name:'Timeline',exact:true}).waitFor()
  assert.equal(await page.locator('[title^="Original dates "]').count(),0)
  assert.equal(await page.locator('[title^="PM baseline dates "]').count(),0)
  deliverable.originalDueDate='2026-10-26'; Object.assign(timeline.deliverables[0],{originalDueDate:'2026-10-26',slipShown:true,baselineSlipShown:true})
  await page.reload()
  await page.locator('[title^="Original dates "]').first().waitFor()
  await page.locator('[title^="PM baseline dates "]').first().waitFor()
  timeline.tasks.push({...timeline.tasks[0],id:id(39),key:'SYN-046-T002',name:'Outside the chain',affectedMilestoneIds:[]})
  const beforeFilters=writes.length
  await page.goto(`${base}/projects/${project.projectNumber}/timeline?chainTo=${milestoneId}`)
  await page.getByRole('status').filter({hasText:'1 of 2 tasks'}).waitFor()
  await page.getByRole('button',{name:`Show the 2 tasks of ${deliverable.key}`,exact:true}).click()
  const highlighted=page.locator('[data-chain="highlight"]').filter({has:page.getByRole('button',{name:new RegExp(task.key+' '+task.name)})})
  assert.equal(await highlighted.count(),1)
  assert.equal(await page.locator('[data-chain="dim"]').filter({hasText:'Outside the chain'}).count(),1)
  await page.getByRole('button',{name:'Remove filter: Chain to milestone',exact:true}).click()
  await page.waitForURL(url=>!url.searchParams.has('chainTo'))
  assert.equal(new URL(page.url()).searchParams.has('chainTo'),false)
  await page.waitForFunction(()=>document.querySelectorAll('[data-chain]').length===0)
  assert.equal(await page.locator('[data-chain]').count(),0)
  // With its flag off a chainTo link does nothing, so it shows no filter token either.
  flags.timeline_handoffs_chain=false
  await page.goto(`${base}/projects/${project.projectNumber}/timeline?chainTo=${milestoneId}`)
  await page.getByRole('heading',{name:'Timeline',exact:true}).waitFor()
  assert.equal(await page.getByRole('button',{name:'Remove filter: Chain to milestone',exact:true}).count(),0,'no chain token while timeline_handoffs_chain is off')
  flags.timeline_handoffs_chain=true
  timeline.handoffs[0].status='Incorporated'
  await page.goto(`${base}/projects/${project.projectNumber}/timeline?hideCompleted=true`)
  await page.getByRole('heading',{name:'Timeline',exact:true}).waitFor()
  assert.equal(await page.getByRole('link',{name:/SYN-046-H001/}).count(),0)
  timeline.handoffs[0].status='Submitted'
  project.disciplines.push({id:id(38),name:'Electrical',isActive:true,colour:'#467762'})
  timeline.handoffs[0].receivingDisciplineId=id(38)
  for(const side of [discipline,id(38)]) {
    await page.goto(`${base}/projects/${project.projectNumber}/timeline?disciplineId=${side}`)
    await page.getByRole('link',{name:/SYN-046-H001.*Civil.*Electrical/}).first().waitFor()
  }
  assert.equal(writes.length,beforeFilters,'chain and handoff filters never write')

  // Create from a task preserves its fields and the related-item links in both panels.
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Task:${taskId}`)
  await page.getByRole('button',{name:'Create deliverable from this task',exact:true}).click()
  const create=page.getByRole('dialog').filter({has:page.getByRole('heading',{name:'New deliverable',exact:true})})
  for(const [field,value] of Object.entries({'d-name':task.name,'d-disc':discipline,'d-ms':milestoneId,'d-prio':task.priority,'d-start':task.startDate,'d-due':task.dueDate})) assert.equal(await create.locator(`#${field}`).inputValue(),value,field)
  await create.locator('#d-type').selectOption(id(60))
  await create.getByRole('button',{name:'Save',exact:true}).click()
  await create.waitFor({state:'hidden'})
  const creation=writes.find(x=>x.path===`projects/${pid}/deliverables`&&x.method==='POST')
  assert.equal(creation.body.fromTaskId,taskId); assert.equal(creation.body.milestoneId,milestoneId); assert.equal(creation.body.ownerId,person)
  await page.getByRole('dialog').getByRole('button',{name:`${deliverable.key} ${deliverable.name}`,exact:true}).click()
  await page.getByRole('dialog').getByText('Related tasks:',{exact:false}).waitFor()
  await page.getByRole('dialog').getByRole('button',{name:`${task.key} ${task.name}`,exact:true}).click()
  await page.getByRole('dialog').getByRole('heading',{name:task.name,exact:true}).waitFor()
  viewerMode=true
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Task:${taskId}`)
  await page.getByRole('dialog').getByRole('heading',{name:task.name,exact:true}).waitFor()
  assert.equal(await page.getByRole('button',{name:'Create deliverable from this task',exact:true}).count(),0)
  await page.goto(`${base}/projects/${project.projectNumber}/deliverables?panel=Deliverable:${deliverableId}`)
  await page.getByRole('dialog').getByRole('heading',{name:deliverable.name,exact:true}).waitFor()
  assert.equal(await page.getByRole('button',{name:'Re-baseline',exact:true}).count(),0)
  viewerMode=false

  // Drag previews include affected successors and tasks; cancellation leaves every date untouched.
  await page.goto(`${base}/projects/${project.projectNumber}/timeline`)
  const dragBar=page.locator('button[style*="cursor: grab"]').filter({hasText:'0%'}).first()
  await dragBar.waitFor(); const box=await dragBar.boundingBox(); assert.ok(box)
  const beforeDrag=writes.length
  await page.mouse.move(box.x+box.width/2,box.y+box.height/2); await page.mouse.down(); await page.mouse.move(box.x+box.width/2+48,box.y+box.height/2,{steps:6}); await page.mouse.up()
  const move=page.getByRole('dialog')
  await move.getByText('Synthetic new date inconsistency',{exact:true}).waitFor()
  await move.getByText(/SYN-046-T061: Synthetic successor/).waitFor()
  await move.getByText(/SYN-046-T001: Synthetic task lies outside/).waitFor()
  await move.getByRole('button',{name:'Cancel',exact:true}).click()
  assert.equal(writes.length,beforeDrag)
  assert.equal(deliverable.dueDate,'2026-10-30'); assert.equal(task.dueDate,'2026-10-30')

  // Global timeline expansion survives reload and a browser history round trip.
  await page.goto(`${base}/timeline?projects=${pid}`)
  await page.getByRole('button',{name:'Collapse SYN-046',exact:true}).click()
  await page.waitForURL(url=>url.searchParams.get('collapsed')===pid)
  await page.getByRole('button',{name:'Expand SYN-046',exact:true}).waitFor()
  await page.reload(); await page.getByRole('button',{name:'Expand SYN-046',exact:true}).waitFor()
  await page.goto(`${base}/projects/${project.projectNumber}/timeline`)
  await page.goBack(); await page.getByRole('button',{name:'Expand SYN-046',exact:true}).waitFor()
  assert.equal(new URL(page.url()).searchParams.get('collapsed'),pid)
  await page.goto(`${base}/timeline?projects=${pid}&collapsed=all`)
  await page.getByRole('button',{name:'Expand SYN-046',exact:true}).waitFor()
  await page.getByRole('button',{name:'Expand projects',exact:true}).click()
  await page.getByRole('button',{name:'Collapse SYN-046',exact:true}).waitFor()
  await page.getByRole('button',{name:'Collapse projects',exact:true}).click()
  await page.waitForURL(url=>url.searchParams.get('collapsed')==='all')
  await page.reload(); await page.getByRole('button',{name:'Expand SYN-046',exact:true}).waitFor()

  assert.deepEqual(errors, [], `page errors: ${errors.join('; ')}`)
  assert.deepEqual(unknown, [], `unmocked API paths: ${unknown.join(', ')}`)
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  const axeViolations = await page.evaluate(async () => (await axe.run(document.querySelector('main'), { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } })).violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) })))
  assert.deepEqual(axeViolations, [], 'timeline accessibility')
  const result = { scope: 'Mocked synthetic API in Chromium; no production or native evidence', rowActions: true, blockDialog:true, latestPlannedRule:true, handoffMarks:true, createDeliverableFromTask:true, deniedCreationAndBaseline:true, dragWarningsCancel:true, globalTimelineCollapse:true, groupedActivity:true, thresholdGhosts:true, exactChain:true, handoffFilters:true, emptyName:true, nameConflict:true, pendingStatusRefusal:true, inconsistentDateSave:true, viewerDeepLink: true, closedProjectOpenOnly: true, flagParity: true, draftDateHint: true, rebaselineReason: true, timelineHandoffChain: true, startAuthorizationRefusal: true, axeViolations, writes: writes.length, errors, unknown, untested }
  const out = path.resolve(__dirname, '../test-results/interaction-browser'); fs.mkdirSync(out, { recursive: true }); fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify(result, null, 2)); console.log(JSON.stringify(result))
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() })
