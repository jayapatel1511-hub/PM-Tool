const path = require('node:path')
const { spawn } = require('node:child_process')
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const taskId = '00000000-0000-0000-0000-000000000001'
const project = { id: '00000000-0000-0000-0000-000000000010', projectNumber: 'P-MOCK', name: 'Synthetic live project', status: 'Active', visibility: 'Open', rowVersion: 1, disciplines: [{ id: '00000000-0000-0000-0000-000000000011', disciplineId: '00000000-0000-0000-0000-000000000012', name: 'Civil', leadUserId: null }], projectDisciplines: [{ id: '00000000-0000-0000-0000-000000000011', disciplineId: '00000000-0000-0000-0000-000000000012', name: 'Civil', leadUserId: null }], links: [], permissions: { edit: { ok: true }, reviewDates: { ok: false }, isPm: true, needsReason: false } }
Object.assign(project, { client: 'Synthetic client', pmName: 'Alex Chen', phase: null, nextMilestone: null, starred: false, health: { computed: 'Green', reported: 'Green', overrideActive: false, reasons: [] }, permissions: { ...project.permissions, isPm: false, leadOf: [], createTaskIn: [] } })
const task = { id: taskId, projectId: project.id, projectDisciplineId: '00000000-0000-0000-0000-000000000011', key: 'T-001', name: 'Synthetic task', status: 'Not Started', priority: 'Medium', rowVersion: 1, startDate: null, dueDate: null, assigneeId: null, assigneeName: null, reviewerId: null, reviewerName: null, reviewRound: 0, dueDateChangeCount: 0, effortHours: 0, state: { isWaiting: false, isBlocked: false, daysBlocked: 0, blockedBy: [], affectedMilestoneIds: [], isOverdue: false, isDueSoon: false, isBlocking: false, blockingCount: 0, isStale: false, isUnassigned: true, isMissingDueDate: true, isDateInconsistent: false, notes: [] } }
const work = { person: { id: '00000000-0000-0000-0000-000000000002', displayName: 'Alex Chen', isActive: true }, readOnly: false, today: '2026-10-03', attention: { items: [], total: 0 }, tasks: [task], reviews: { tasks: [], deliverables: [] }, deliverables: [], people: {}, successors: [], decisions: [], actions: [], projects: [{ id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status, roles: ['TeamMember'], leads: [], follow: null, computedHealth: 'Green', reportedHealth: 'Green', nextMilestone: null }], milestones: [], completed: [] }
const views = { views: [], canShare: false }
const planningGrid = { weeks: [], currentWeek: '2026-10-03', partialView: false, draftAccess: false, settings: { horizonWeeks: 6, overPct: 1, underPct: 1, underWeeks: 1, staleDays: 14, maxHoursPerWeek: 40 }, people: [] }
const { documentLinks: links } = require('./document-links.fixture.cjs')
const { comments } = require('./comments.fixture.cjs')
const taskPage = { items: [], page: 1, pageSize: 200, totalCount: 0 }
const me = { id: '00000000-0000-0000-0000-000000000002', displayName: 'Alex Chen', email: 'alex@hub.test', roles: [], systemRoles: ['StandardUser'], capabilities: { createProject: false, createTask: true, portfolio: false, workload: true, staff: false, allStaff: false, admin: true, templates: false, readOnly: false, directReports: 0 }, settings: { today: '2026-10-03', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8, restrictedProjectsEnabled: true }, preferences: { denseRows: true, digestEnabled: true, weeklySummaryEnabled: true }, flags: { live_events: true, live_project_stamp: true } }
const taskDetail = { task, description: 'Synthetic fixture task', project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, collaborators: [], watchers: [], permissions: { edit: { ok: true }, assign: { ok: true }, setReviewer: { ok: true }, dueDate: { ok: true }, dueNeedsReason: false, block: { ok: true }, delete: { ok: true }, restore: true, comment: true, transitions: [], dependencies: true, enterTime: true, needsReason: false, allowSelfReview: true } }

async function startPreview(port = Number(process.env.TUESDAY_MOCK_PORT ?? 5189)) {
  port = Number(process.env.TUESDAY_MOCK_PORT ?? port)
  const cwd = path.resolve(__dirname, '..')
  const child = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', String(port), '--strictPort'], { cwd, stdio: 'ignore' })
  let exited = false; child.once('exit', () => { exited = true })
  const base = `http://127.0.0.1:${port}`
  for (let i = 0; i < 100; i++) { if (exited) break; try { if ((await fetch(base)).ok) return { base, child } } catch {} await new Promise(resolve => setTimeout(resolve, 100)) }
  child.kill(); throw new Error(`preview did not start at ${base}`)
}
function mockPlatformApi(page, { build, buildIds = ['mock-build-a', 'mock-build-b', 'mock-build-a'], events = true } = {}) {
  let currentBuild = build ?? buildIds[0]
  const eventRequests = []
  const pulseRequests = []
  const apiErrors = []; const unknownGets = []
  const routePromise = page.route('**/api/**', async route => {
    const request = route.request(); const url = new URL(request.url()); const p = url.pathname.replace('/api/v1/', '')
    let status = 200; let data = {}
    if (p === 'config') data = { authMode: 'Development', entra: {} }
    else if (p === 'me') { data = me; await route.fulfill({ status, contentType: 'application/json', headers: { 'x-hub-build': currentBuild }, body: JSON.stringify(data) }); return }
    else if (p === 'me/work') data = work
    else if (p === `projects/${project.projectNumber}` || p === `projects/${project.id}`) data = project
    else if (p === `tasks/${taskId}`) data = taskDetail
    else if (p === `projects/${project.projectNumber}/tasks` || p === `projects/${project.id}/tasks`) data = taskPage
    else if (p === `projects/${project.id}/milestones` || p === `projects/${project.projectNumber}/milestones`) data = []
    else if (p === `projects/${project.id}/deliverables` || p === `projects/${project.projectNumber}/deliverables`) data = []
    else if (p === `projects/${project.id}/date-review` || p === `projects/${project.projectNumber}/date-review`) data = { window: null, tasks: [], deliverables: [] }
    else if (p === `tasks/${taskId}/dependencies`) data = { dependsOn: [], blocks: [] }
    else if (p === 'projects') data = { items: [project], page: 1, pageSize: 200, totalCount: 1 }
    else if (p === 'admin/users') data = []
    else if (p === 'views') data = views
    else if (p === 'planning/grid') data = planningGrid
    else if (p === `items/Task/${taskId}/links`) data = links
    else if (p === `items/Task/${taskId}/comments`) data = comments
    else if (p === 'reference') data = { disciplines: [], clients: [], offices: [], deliverableTypes: [], phases: [], projectTypes: [], limits: { roleLabel: 80 } }
    else if (p === 'workspaces') data = []
    else if (p === 'me/notifications/pulse') { pulseRequests.push(url.toString()); data = { stamp: String(pulseRequests.length), projectStamp: String(pulseRequests.length) } }
    else if (p === 'me/notifications/unread-count') data = { notifications: 0, following: 0 }
    else if (p === 'me/sign-in' || p.startsWith('admin/settings/flag.')) data = {}
    else if (p === 'events') { eventRequests.push(request); if (!events) status = 404; else { await route.fulfill({ status: 200, contentType: 'text/event-stream', headers: { 'cache-control': 'no-cache' }, body: ': heartbeat\n\ndata: {"topic":"project","projectId":"'+project.id+'","itemType":"Task","itemId":"'+taskId+'"}\n\n' }); return } }
    else if (request.method() !== 'GET') data = {}
    else { unknownGets.push(`${request.method()} ${p}`); status = 501; data = { error: `Unhandled mocked GET: ${p}` } }
    if (status >= 400) apiErrors.push(`${request.method()} ${p} ${status}`)
    await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
  })
  return { eventRequests, pulseRequests, apiErrors, unknownGets, setBuild: (next) => { currentBuild = next }, stop: () => page.unroute('**/api/**', routePromise) }
}
async function newMockPage(base, opts) {
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'alex@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
  const mock = mockPlatformApi(page, opts)
  return { browser, page, mock }
}
module.exports = { startPreview, newMockPage, mockPlatformApi, project, task, taskDetail, work, views, planningGrid, links, comments }
