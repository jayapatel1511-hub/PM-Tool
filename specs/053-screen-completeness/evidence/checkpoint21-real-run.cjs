/* Real QA21 Board + Team harness. Guard closed until root loans execution. */
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const API = process.env.CHECKPOINT21_API || 'http://127.0.0.1:5088/api/v1'
const UI = process.env.CHECKPOINT21_PREVIEW || 'http://127.0.0.1:8853'
const evidence = __dirname
const result = { observedAtUtc: new Date().toISOString(), commands: {}, reads: {}, browser: [], failures: [], limits: [] }

async function request(user, route, method = 'GET', body) {
  const response = await fetch(API + route, { method, headers: { 'X-Dev-User': `${user}@hub.test`, ...(body === undefined ? {} : { 'content-type': 'application/json' }) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text(); let value = null
  try { value = text ? JSON.parse(text) : null } catch { value = text }
  return { status: response.status, value }
}
function rows(value) { return Array.isArray(value) ? value : (value?.items || value?.rows || value?.people || value?.members || []) }
function id(value) { return value?.id || value?.value?.id }
function record(name, response) { result.commands[name] = { status: response.status, id: id(response.value), key: response.value?.key }; assert.ok(response.status >= 200 && response.status < 300, `${name} HTTP ${response.status}`); return response.value }
function dateOffset(today, days) { const d = new Date(`${today}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10) }
function weekOf(today) { const d = new Date(`${today}T00:00:00Z`); d.setUTCDate(d.getUTCDate() - (d.getUTCDay() + 6) % 7); return d.toISOString().slice(0, 10) }

async function createFixture() {
  const users = await request('jordan', '/admin/users'); const reference = await request('jordan', '/reference'); const me = await request('priya', '/me')
  const userRows = rows(users.value); const uid = email => userRows.find(person => person.email === `${email}@hub.test`)?.id
  const today = me.value?.settings?.today; const civil = reference.value?.disciplines?.find(x => x.code === 'CIV')?.id; const clientId = reference.value?.clients?.[0]?.id; const officeId = reference.value?.offices?.[0]?.id
  assert.ok(today && civil && clientId && officeId, 'actual /me and /reference must provide today, Civil, client, office')
  const existingProjects = await request('priya', '/projects?q=QA21-REAL-001&pageSize=20'); const existingProject = rows(existingProjects.value).find(project => project.projectNumber === 'QA21-REAL-001')
  const project = existingProject || record('projectCreate', await request('priya', '/projects', 'POST', { projectNumber: 'QA21-REAL-001', name: 'Checkpoint 21 real Board Team project', clientId, officeId, startDate: dateOffset(today, -14), targetCompletionDate: dateOffset(today, 29), disciplines: [{ disciplineId: civil, leadUserId: uid('marc') }], members: [{ userId: uid('alex'), roles: ['TeamMember'], disciplineId: civil }, { userId: uid('jill'), roles: ['TeamMember'], disciplineId: civil }] }))
  const projectId = project.id; const detail = await request('priya', `/projects/${projectId}`); if (detail.value?.status !== 'Active') record('projectActivate', await request('priya', `/projects/${projectId}/transition`, 'POST', { toStatus: 'Active', rowVersion: detail.value.rowVersion }))
  const projectDisciplineId = detail.value.disciplines?.[0]?.id
  const deliverables = await request('priya', `/projects/${projectId}/deliverables`); const existingDeliverable = rows(deliverables.value).find(item => item.name === 'QA21 current deliverable')
  const deliverable = existingDeliverable || record('deliverableCreate', await request('priya', `/projects/${projectId}/deliverables`, 'POST', { name: 'QA21 current deliverable', projectDisciplineId, deliverableTypeId: reference.value.deliverableTypes?.[0]?.id, ownerId: uid('priya'), startDate: dateOffset(today, -14), dueDate: dateOffset(today, 19), requiresReview: false }))
  const taskSpec = [
    ['overdue', uid('alex'), 3, dateOffset(today, -12), dateOffset(today, -19)],
    ['current deliverable', uid('alex'), 3, dateOffset(today, 19), dateOffset(today, 9)],
    ['PM-owned', uid('priya'), 2, dateOffset(today, 17), dateOffset(today, 9)],
    ['reviewer zero-day', uid('alex'), 2, today, dateOffset(today, -6)],
    ['review timestamp', uid('alex'), 2, dateOffset(today, 11), dateOffset(today, 2)]
  ]
  const existingTasks = rows((await request('priya', `/projects/${projectId}/tasks?pageSize=200`)).value)
  const tasks = []
  for (const [index, [label, assigneeId, estimatedHours, dueDate, startDate]] of taskSpec.entries()) {
    const existing = existingTasks.find(task => task.name === `QA21 ${label}`)
    tasks.push(existing || record(`taskCreate${index + 1}`, await request('priya', `/projects/${projectId}/tasks`, 'POST', { name: `QA21 ${label}`, projectDisciplineId, deliverableId: deliverable.id, assigneeId, reviewerId: label.startsWith('reviewer') || label.startsWith('review ') ? uid('marc') : null, requiresReview: label.startsWith('reviewer') || label.startsWith('review '), estimatedHours, dueDate, startDate })))
  }
  for (const task of tasks.slice(0, 3)) if (task.status !== 'In Progress') record(`start-${task.id}`, await request('priya', `/tasks/${task.id}/transition`, 'POST', { toStatus: 'In Progress', rowVersion: task.rowVersion, reason: 'QA21 real acceptance', acknowledgeReadiness: true }))
  const reviewReady = {}
  for (const task of [tasks[3], tasks[4]]) {
    const started = task.status === 'Not Started' ? record(`start-${task.id}`, await request('priya', `/tasks/${task.id}/transition`, 'POST', { toStatus: 'In Progress', rowVersion: task.rowVersion, reason: 'QA21 real acceptance', acknowledgeReadiness: true })) : task
    const ready = task.status === 'Ready for Review' || task.status === 'In Review' ? task : record(`ready-${task.id}`, await request('alex', `/tasks/${task.id}/transition`, 'POST', { toStatus: 'Ready for Review', reviewerId: uid('marc'), rowVersion: started.rowVersion }))
    const readyRead = await request('priya', `/tasks/${task.id}`)
    assert.equal(readyRead.status, 200, 'ready task read must be 200')
    reviewReady[task.id] = readyRead.value?.reviewRequestedAt ?? readyRead.value?.board?.reviewRequestedAt
    assert.ok(reviewReady[task.id], 'ready task must expose ReviewRequestedAt')
    if (task === tasks[4] && task.status !== 'In Review') {
      record(`in-review-${task.id}`, await request('marc', `/tasks/${task.id}/transition`, 'POST', { toStatus: 'In Review', rowVersion: ready.rowVersion }))
      const after = await request('priya', `/tasks/${task.id}`)
      assert.equal(after.status, 200, 'In Review task read must be 200')
      assert.equal(after.value?.reviewRequestedAt ?? after.value?.board?.reviewRequestedAt, reviewReady[task.id], 'ReviewRequestedAt must remain unchanged')
    }
  }
  const existingRoles = rows((await request('priya', `/projects/${projectId}/open-roles`)).value)
  for (const [index, role] of [['Civil reviewer', 10], ['Civil technologist', 20]].entries()) if (!existingRoles.some(item => item.roleLabel === `QA21 ${role[0]}`)) record(`openRole${index + 1}`, await request('priya', `/projects/${projectId}/open-roles`, 'POST', { roleLabel: `QA21 ${role[0]}`, projectDisciplineId, fromDate: dateOffset(today, 1), throughDate: dateOffset(today, 5), plannedHours: role[1], note: 'Disposable QA21 role' }))
  const week = weekOf(today); for (const [index, personId] of [uid('alex'), uid('jill')].entries()) record(`planning${index + 1}`, await request('sam', '/planning/entries', 'POST', { personId, hoursPerWeek: index ? 12 : 8, startWeek: week, endWeek: week, label: `QA21 current-week ${index ? 'Jill' : 'Alex'}`, sourceCategory: 'Other', confidence: 'Expected' }))
  return { projectId, projectNumber: project.projectNumber, taskIds: tasks.map(task => task.id), reviewerZeroTaskId: tasks[3].id, reviewTaskId: tasks[4].id, reviewReady, personIds: { alex: uid('alex'), jill: uid('jill'), marc: uid('marc'), priya: uid('priya') }, today, week }
}

async function readAndJoin(fixture) {
  const { projectId, taskIds } = fixture
  // Jobs/evaluation is disabled in QA21; this normal authorised read invokes EnsureFresh
  // and establishes canonical TaskState before the board join. Never infer overdue from dates.
  result.reads.attention = await request('priya', `/projects/${projectId}/attention`)
  result.reads.board = await request('priya', `/projects/${projectId}/tasks?sort=board&pageSize=200`)
  result.reads.projectDashboard = await request('priya', `/projects/${projectId}/dashboard`)
  result.reads.projectTeam = await request('priya', `/projects/${projectId}/team`)
  result.reads.workspaceTeam = await request('priya', '/team')
  result.reads.openRoles = await request('priya', `/projects/${projectId}/open-roles`)
  result.reads.grid = await request('sam', `/planning/grid?from=${fixture.week}&weeks=1`)
  result.reads.taskDetails = await Promise.all(taskIds.map(taskId => request('priya', `/tasks/${taskId}`)))
  for (const [name, read] of Object.entries(result.reads)) {
    if (name === 'taskDetails') continue
    assert.equal(read.status, 200, `${name} reader must return HTTP 200`)
  }
  for (const read of result.reads.taskDetails) assert.equal(read.status, 200, 'task detail reader must return HTTP 200')
  const board = rows(result.reads.board.value); const roles = rows(result.reads.openRoles.value); const projectTeam = rows(result.reads.projectTeam.value); const workspaceTeam = rows(result.reads.workspaceTeam.value)
  const canonicalState = board.every(card => card.state && typeof card.state.isOverdue === 'boolean')
  if (!canonicalState) { result.failures.push({ blocked: 'canonical board State.IsOverdue unavailable after normal /attention EnsureFresh read' }); result.blocked = true }
  const overdue = canonicalState ? board.filter(card => card.state.isOverdue).length : null
  const inReview = board.filter(card => card.status === 'In Review').length
  const currentDeliverable = board.filter(card => card.deliverableId && card.deliverableName).length
  const pmCards = board.filter(card => card.board?.projectManager?.id === fixture.personIds.priya).length
  const marcLeadCards = board.filter(card => card.board?.disciplineLead?.id === fixture.personIds.marc).length
  const reviewerZero = board.filter(card => card.status === 'Ready for Review' && card.reviewerId === fixture.personIds.marc && card.board?.reviewWaitingDays === 0).length
  assert.equal(board.length, 5, 'Board must expose exactly five source cards')
  assert.equal(board.reduce((sum, card) => sum + Number(card.estimatedHours || 0), 0), 12, 'Board estimates must total 12h')
  if (canonicalState) assert.equal(overdue, 1, 'Board must expose exactly one canonical overdue source card')
  assert.ok(currentDeliverable >= 1, 'current deliverable must be represented')
  assert.equal(pmCards, 5, 'all five cards must carry the actual PM ID')
  assert.equal(marcLeadCards, 5, 'all five cards must carry the actual Civil lead ID')
  assert.equal(reviewerZero, 1, 'one Ready for Review card must carry Marc and wait 0 days')
  assert.equal(inReview, 1, 'one card must remain In Review')
  assert.equal(roles.length, 2, 'Team must expose two open roles'); assert.equal(new Set(roles.map(role => role.id)).size, 2, 'open role IDs must remain distinct')
  assert.ok(projectTeam.length >= 2 && workspaceTeam.length >= 2, 'Team member joins must be nonempty')
  result.joins = { boardCards: board.length, boardHours: 12, overdue, currentDeliverable, pmCards, inReview, reviewerZero, projectTeamMembers: projectTeam.length, workspaceTeamMembers: workspaceTeam.length, openRoles: roles.length, gridPeople: rows(result.reads.grid.value).length }
}

async function browser(fixture) {
  const { chromium } = require('../../../web/node_modules/playwright'); const browser = await chromium.launch({ executablePath: process.env.CHROME_EXECUTABLE_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true })
  try { for (const route of ['/boards', '/team', `/projects/${fixture.projectNumber}/team`]) for (const [width, height] of [[1440, 1000], [375, 812]]) {
    const context = await browser.newContext({ viewport: { width, height }, reducedMotion: 'reduce' }); await context.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'priya@hub.test'); sessionStorage.setItem('hub.signedIn', '1') }); const page = await context.newPage(); const errors = []; page.on('pageerror', error => errors.push(error.message))
    try { const response = await page.goto(UI + route, { waitUntil: 'domcontentloaded' }); await page.locator('main').waitFor(); await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {}); const text = await page.locator('body').innerText(); const overflow = await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1); assert.equal(response?.status(), 200, `${route} HTTP status`); assert.ok(overflow, `${route} ${width}px document containment`); if (route === '/boards') { for (const label of ['QA21 overdue', 'QA21 current deliverable', 'QA21 PM-owned', 'QA21 reviewer zero-day', 'QA21 review timestamp']) assert.ok(text.includes(label), `board must render ${label}`); assert.ok(text.includes('In Review') && text.includes('Ready for Review'), 'board must render review lanes'); } if (route === '/team') assert.ok(text.includes('Alex Chen') && text.includes('Jill Martin'), 'workspace Team must render actual people'); if (route.includes('/team') && route !== '/team') { assert.ok(text.includes('Civil'), 'project Team must render actual discipline'); assert.ok(text.includes('QA21 Civil reviewer') && text.includes('QA21 Civil technologist'), 'project Team must render two actual open roles'); } const file = path.join(evidence, `checkpoint21-real-${route === '/boards' ? 'board' : route === '/team' ? 'team' : 'project-team'}-${width}.png`); await page.screenshot({ path: file, fullPage: true }); result.browser.push({ route, viewport: [width, height], status: response?.status(), overflow, textSample: text.slice(-1800), errors }) } finally { await page.close(); await context.close() }
  } } finally { await browser.close() }
}

async function main() {
  if (process.env.CHECKPOINT21_REAL_EXECUTE !== '1') { console.log('Guard closed: no API, browser, DB, service, flag, build, or test action.'); return }
  result.reads.config = await request('jordan', '/config')
  for (const flag of ['visual_system', 'small_ux', 'work_screens', 'project_screens', 'people_screens', 'org_structure', 'capacity_screens', 'capacity_reports']) record(`flag-${flag}`, await request('jordan', `/admin/settings/flag.${flag}`, 'PUT', { value: true }))
  try { const fixture = await createFixture(); result.fixture = fixture; await readAndJoin(fixture); await browser(fixture) } catch (error) { result.failures.push({ fatal: error.message }); process.exitCode = 1 } finally { result.limits.push('QA21 disposable DB only; normal API commands; no SQL; browser contexts closed'); fs.writeFileSync(path.join(evidence, 'checkpoint21-real-api.json'), JSON.stringify(result, null, 2) + '\n'); fs.writeFileSync(path.join(evidence, 'checkpoint21-real-summary.json'), JSON.stringify({ observedAtUtc: result.observedAtUtc, status: result.failures.length ? 'FAIL_OR_UNPROVEN' : 'PASS', fixture: result.fixture, joins: result.joins, browser: result.browser, failures: result.failures, limits: result.limits }, null, 2) + '\n') }
}
main().catch(error => { result.failures.push({ fatal: error.message }); process.exitCode = 1 })
