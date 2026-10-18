// Packet 046: focused browser checks against deterministic mocked API contracts.
const { chromium } = require('playwright')
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
let viewerMode = false
const project = { id: pid, projectNumber: 'SYN-046', name: 'Synthetic interaction project', client: 'Synthetic client', pmName: 'Synthetic PM', status: 'Active', visibility: 'Open', rowVersion: 1, links: [], disciplines: [{ id: discipline, name: 'Civil', code: 'C', colour: '#467762', isActive: true }], starred: false, myRoles: ['PM'], startDate: '2026-10-01', targetCompletionDate: '2026-11-30', health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] }, permissions: { edit: { ok: true }, healthOverride: { ok: false }, isPm: true, leadOf: [], createTaskIn: [discipline], createDeliverableIn: [discipline], transitions: [], manageMilestones: true } }
const task = { id: taskId, projectId: pid, key: 'SYN-046-T001', name: 'Synthetic task', projectDisciplineId: discipline, disciplineName: 'Civil', disciplineColour: '#467762', status: 'Not Started', priority: 'Medium', progressPct: 0, startDate: '2026-10-20', dueDate: '2026-10-30', originalDueDate: null, dueDateChangeCount: 0, createdAt: '2026-10-18', lastActivityAt: '2026-10-18T12:00:00Z', assigneeId: person, assigneeName: 'Synthetic PM', assigneeActive: true, reviewerId: null, reviewerName: null, requiresReview: false, collaboratorIds: [], deliverableId, deliverableKey: 'SYN-046-D001', deliverableName: 'Synthetic deliverable', deliverableOrder: 1, rowVersion: 2, reviewRound: 0, state: { isOverdue: false, isDueSoon: false, isBlocked: false, isWaiting: false, isBlocking: false, isStale: false, isUnassigned: false, isMissingDueDate: false, isDateInconsistent: false, isInactiveOwner: false, isHeldPastDue: false, isReviewStalled: false, blockingCount: 0, blockedBy: [], notes: [], inconsistencyDetail: [] }, manualBlockType: null }
const deliverable = { id: deliverableId, projectId: pid, key: 'SYN-046-D001', name: 'Synthetic deliverable', projectDisciplineId: discipline, disciplineName: 'Civil', status: 'In Progress', priority: 'Medium', startDate: '2026-10-20', dueDate: '2026-10-30', originalDueDate: null, rowVersion: 2, canEditDates: true, canRebaseline: true, baselineDueDate: '2026-10-28', sinceBaselineDays: 2, tasks: [{ id: taskId, key: task.key, name: task.name, assignee: 'Synthetic PM', dueDate: task.dueDate, status: task.status }], state: { progressPct: 0, taskOpen: 1, taskOverdue: 0, taskBlocked: 0 }, permissions: { edit: { ok: true }, issue: false, transitions: [] }, issues: [], derivedPredecessors: [], derivedSuccessors: [] }
const milestone = { id: milestoneId, key: 'SYN-046-M01', name: 'Synthetic review', milestoneType: 'Review', date: '2026-11-01', originalDate: '2026-10-29', isComplete: false, isCancelled: false, canRebaseline: true, baselineDate: '2026-10-29', sinceBaselineDays: 3, rowVersion: 2, projectId: pid, deliverableIssued: 0, deliverableTotal: 1, taskComplete: 0, taskTotal: 1, statusReasons: [], deliverables: [] }
const me = { id: person, displayName: 'Synthetic PM', email: 'pm@hub.test', roles: [], systemRoles: [], capabilities: { createProject: true, createTask: true, portfolio: true, workload: true, staff: true, allStaff: true, admin: false, templates: true, readOnly: false, directReports: 0 }, settings: { today: '2026-10-18', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8, chainDepthLimit: 20 }, preferences: { denseRows: true }, flags }
const taskDetail = { task, project: { id: pid, projectNumber: project.projectNumber, name: project.name, status: project.status }, collaborators: [], watchers: [], permissions: { edit: { ok: true }, assign: { ok: true }, setReviewer: { ok: true }, dueDate: { ok: true }, dueNeedsReason: false, block: { ok: true }, delete: { ok: true }, restore: false, comment: false, transitions: [{ to: 'In Progress', allowed: true, needsReason: false }], isReviewer: false, dependencies: false, enterTime: false, needsReason: false, allowSelfReview: false }, description: 'Synthetic detail' }
const timeline = { latestPlanned: { date: '2026-10-18', itemKey: 'SYN-046-T001', targetDate: '2026-10-30', rule: 'Working days' }, handoffs: [{ id: id(7), key: 'SYN-046-H001', title: 'Civil to Electrical', status: 'Submitted', sendingDisciplineId: discipline, receivingDisciplineId: discipline, neededBy: '2026-10-25', promisedBy: '2026-10-27', dateMismatch: true, isOverdue: false, daysOverdue: 0 }], permissions: { manageMilestones: false, needsReason: false }, tasks: [{ ...task, affectedMilestoneIds: [milestoneId], canEditDates: false, dueNeedsReason: false, isOverdue: false, slipShown: true }], dependencies: [], deliverableLinks: [], deliverables: [{ id: deliverableId, canEditDates: true, createdOn: '2026-10-18', slipShown: true, baselineStartDate: '2026-10-20', baselineDueDate: '2026-10-28', baselineSlipShown: true }] }

let server, browser
;(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' })
  for (let i = 0; i < 80; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
  browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  const errors = []; const unknown = []
  const untested = []
  page.on('console', m => { if (m.type() === 'error') console.error(`browser console: ${m.text()}`) })
  page.on('pageerror', e => errors.push(e.message))
  await page.addInitScript(() => sessionStorage.setItem('hub.devUser', 'pm@hub.test'))
  await page.route('**/api/**', async route => {
    const req = route.request(); const u = new URL(req.url()); const p = u.pathname.replace('/api/v1/', ''); const method = req.method(); let data = {}
    if (p === `items/Task/${taskId}/date-context`) dateContextRequests.push(u.search)
    if (p === 'config') data = { authMode: 'Development', entra: {} }
    else if (p === 'me') data = me
    else if (p === 'me/sign-in') data = {}
    else if (p.startsWith('me/notifications/')) data = p.endsWith('unread-count') ? { notifications: 0, following: 0 } : { stamp: '0' }
    else if (p === 'workspaces') data = []
    else if (p === 'views') data = { views: [{ id: id(8), name: 'My saved view', scope: 'Personal', isDefault: false, rowVersion: 1, params: { group: 'status' }, dropped: [], canEdit: true }], canShare: false }
    else if (p === 'reference') data = { disciplines: [{ id: discipline, name: 'Civil', isActive: true }], clients: [], offices: [], deliverableTypes: [], phases: [], projectTypes: [], limits: { roleLabel: 80 } }
    else if (p === 'users') data = [{ id: person, displayName: 'Synthetic PM', isActive: true }]
    else if (p === `projects/${pid}` || p === `projects/${project.projectNumber}`) data = viewerMode ? { ...project, status: 'Archived', permissions: { ...project.permissions, edit: { ok: false }, createTaskIn: [], createDeliverableIn: [] } } : project
    else if (p === `projects/${pid}/tasks`) data = { items: [task], totalCount: 1, page: 1, pageSize: 200 }
    else if (p === `projects/${pid}/deliverables`) data = [deliverable]
    else if (p === `projects/${pid}/milestones`) data = [milestone]
    else if (p === `tasks/${taskId}` && method === 'GET') data = taskDetail
    else if (p === `tasks/${taskId}/start-readiness`) data = { readinessState: 'Needs Assessment', assessed: false, unknown: [], blocked: [], note: null, needsAuthorisation: true, canAuthorise: false, authorisation: null, unusableAuthorisation: null }
    else if (p === `tasks/${taskId}/dependencies`) data = { dependsOn: [], blocks: [] }
    else if (p === `items/Task/${taskId}/links` || p === `items/Deliverable/${deliverableId}/links`) data = { links: [], inherited: [], canAdd: false }
    else if (p === `items/Task/${taskId}/comments` || p === `items/Deliverable/${deliverableId}/comments`) data = { items: [], canComment: false }
    else if (p === `deliverables/${deliverableId}`) data = { deliverable, project, tasks: deliverable.tasks, issues: [], derivedPredecessors: [], derivedSuccessors: [], permissions: deliverable.permissions }
    else if (p === `milestones/${milestoneId}`) data = { milestone, permissions: { manage: { ok: true } }, deliverables: [], completesPhase: null }
    else if (p === `milestones/${milestoneId}/history` || p === `deliverables/${deliverableId}/history` || p === `tasks/${taskId}/history`) data = []
    else if (p === `projects/${pid}/timeline`) data = timeline
    else if (p === `projects/${pid}/date-review`) data = { window: null, tasks: [], deliverables: [] }
    else if (p === `items/Task/${taskId}/date-context`) data = { dates: { earliestStart: { date: '2026-10-22' }, earliestDue: { date: '2026-11-03' }, latestDue: { date: '2026-11-10' }, inconsistencies: [], successors: [], newInconsistencies: [] } }
    else if (p.endsWith('/follow')) data = { level: 'My items only', source: 'Assignment' }
    else if (p === `tasks/${taskId}/transition`) { writes.push({ method, path: p, body: req.postDataJSON?.() }); data = { code: 'start_authorisation_required', detail: 'A start authorisation is required.' } }
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
  viewerMode = true; await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`); await page.getByText(task.name, { exact: true }).first().waitFor(); assert.equal(await page.getByRole('button', { name: `Open ${task.key}`, exact: true }).count(), 1); assert.equal(await page.getByRole('button', { name: `Start ${task.key}`, exact: true }).count(), 0); viewerMode = false
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none`); await page.getByText(task.name, { exact: true }).first().waitFor()
  const nameWrites = () => writes.filter(x => x.method === 'PATCH' && x.path === `tasks/${taskId}` && x.body?.name)
  const rename = page.getByRole('button', { name: `Rename ${task.key}`, exact: true }); assert.equal(await rename.count(), 1, 'inline name editor is exposed'); await rename.click(); const nameInput = page.getByRole('textbox', { name: 'Name', exact: true }); await nameInput.press('Escape'); assert.equal(nameWrites().length, 0); await rename.click(); await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Synthetic task renamed'); await page.getByRole('textbox', { name: 'Name', exact: true }).press('Enter'); await page.waitForTimeout(100); assert.equal(nameWrites().length, 1)
  const startBefore = writes.filter(x => x.path === `tasks/${taskId}/transition`).length; await page.getByRole('button', { name: `Start ${task.key}`, exact: true }).click(); await page.getByRole('dialog').getByRole('status').waitFor(); assert.equal(await page.getByRole('row').filter({ hasText: task.key }).getByText(/Not Started/).count(), 1); assert.ok(writes.some(x => x.path === `tasks/${taskId}/transition`), JSON.stringify(writes)); assert.equal(writes.filter(x => x.path === `tasks/${taskId}/transition`).length, startBefore + 1); await page.getByRole('button', { name: 'Cancel', exact: true }).click()
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?view=My%20saved%20view`); await page.getByText(task.name, { exact: true }).first().waitFor()
  flags.row_quick_actions = false; await page.reload(); assert.equal(await page.getByRole('button', { name: `Open ${task.key}`, exact: true }).count(), 0)
  flags.row_quick_actions = true

  // Draft date hint comes from the date-context contract before any save.
  await page.goto(`${base}/projects/${project.projectNumber}/tasks?group=none&panel=Task:${taskId}`); await page.getByRole('heading', { name: task.name, exact: true }).waitFor()
  const dialogTask = page.getByRole('dialog'); const startRow = dialogTask.getByText('Start date', { exact: true }).locator('..'); await startRow.locator('button').click(); const startInput = page.getByLabel('Start date', { exact: true }); await startInput.fill('2026-10-22'); await page.getByRole('dialog').getByText(/Oct 22, 2026|2026-10-22|22 Oct 2026/).waitFor(); assert.ok(dateContextRequests.some(q => /startDate=2026-10-22/.test(q)), JSON.stringify(dateContextRequests)); const writesBeforeClear = writes.length; await startInput.fill(''); await page.getByRole('dialog').getByText(/Nov 3, 2026|2026-11-03|3 Nov 2026/).waitFor(); assert.ok(dateContextRequests.some(q => /startDate=(&|$)/.test(q)), JSON.stringify(dateContextRequests)); await startInput.press('Escape'); assert.equal(writes.length, writesBeforeClear)

  // Re-baseline requires a reason and sends the explicit clear/set intent.
  await page.goto(`${base}/projects/${project.projectNumber}/deliverables?panel=Deliverable:${deliverableId}`); await page.getByRole('button', { name: 'Re-baseline', exact: true }).waitFor(); await page.getByRole('button', { name: 'Re-baseline', exact: true }).click(); const dialog = page.getByRole('dialog'); await dialog.getByRole('textbox').fill('Synthetic baseline reason'); await dialog.getByRole('button', { name: 'Confirm', exact: true }).click(); await page.waitForTimeout(50); assert.deepEqual(writes.at(-1).body, { reason: 'Synthetic baseline reason', clear: false, rowVersion: 2 })

  // Timeline handoff links and chain highlight are URL/UI state only; no writes on filter interaction.
  await page.goto(`${base}/projects/${project.projectNumber}/timeline?chainTo=${milestoneId}`); await page.getByRole('status').filter({ hasText: '1 of 1 tasks' }).waitFor(); assert.equal(new URL(page.url()).searchParams.get('chainTo'), milestoneId)
  const handoff = page.getByRole('link', { name: /SYN-046-H001.*Civil.*Civil/ }).first(); await handoff.waitFor(); assert.match(await handoff.getAttribute('href'), /handoffs\?panel=Handoff:/); assert.equal(writes.filter(x => x.path.includes('timeline') || x.path.includes('handoff')).length, 0)

  assert.deepEqual(errors, [], `page errors: ${errors.join('; ')}`)
  assert.deepEqual(unknown, [], `unmocked API paths: ${unknown.join(', ')}`)
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
  const axeViolations = await page.evaluate(async () => (await axe.run(document.querySelector('main'), { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } })).violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) })))
  assert.deepEqual(axeViolations, [], 'timeline accessibility')
  const result = { scope: 'Mocked synthetic API in Chromium; no production or native evidence', rowActions: true, viewerDeepLink: true, closedProjectOpenOnly: true, flagParity: true, draftDateHint: true, rebaselineReason: true, timelineHandoffChain: true, startAuthorizationRefusal: true, axeViolations, writes: writes.length, errors, unknown, untested }
  const out = path.resolve(__dirname, '../test-results/interaction-browser'); fs.mkdirSync(out, { recursive: true }); fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify(result, null, 2)); console.log(JSON.stringify(result))
})().catch(e => { console.error(e); process.exitCode = 1 }).finally(async () => { await browser?.close(); server?.kill() })
