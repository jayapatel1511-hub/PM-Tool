const path = require('node:path')
const { spawn } = require('node:child_process')
const assert = require('node:assert/strict')
const { chromium } = require('playwright')

const taskId = '00000000-0000-0000-0000-000000000001'
const panelIds = { milestone: '00000000-0000-0000-0000-000000000021', deliverable: '00000000-0000-0000-0000-000000000022', decision: '00000000-0000-0000-0000-000000000023', action: '00000000-0000-0000-0000-000000000024', risk: '00000000-0000-0000-0000-000000000025', issue: '00000000-0000-0000-0000-000000000026' }
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
const panelPermissions = { edit: { ok: false, reason: 'Synthetic read-only panel' }, manage: { ok: false }, comment: true, changeType: { ok: false }, transitions: [{ to: 'Open', ok: false }, { to: 'Closed', ok: false }] }
const milestoneDetail = { milestone: { id: panelIds.milestone, projectId: project.id, key: 'M-001', name: 'Synthetic milestone', milestoneType: 'Design Submission', date: '2026-10-20', originalDate: '2026-10-20', description: 'Synthetic milestone detail', isClientFacing: true, isComplete: false, isCancelled: false, rowVersion: 1, isSubmission: false, status: 'Open', statusReasons: [], slipDays: 0, deliverableTotal: 0, deliverableIssued: 0, taskTotal: 0, taskComplete: 0, taskOpen: 0, taskOverdue: 0, taskBlocked: 0 }, permissions: { ...panelPermissions, manage: { ok: false } }, deliverables: [], completesPhase: null }
const deliverableDetail = { deliverable: { id: panelIds.deliverable, projectId: project.id, key: 'D-001', name: 'Synthetic deliverable', projectDisciplineId: project.projectDisciplines[0].id, deliverableTypeId: '00000000-0000-0000-0000-000000000030', priority: 'Medium', status: 'In Review', requiresReview: false, rowVersion: 1, createdAt: '2026-10-01T00:00:00Z', disciplineOrder: 1, ownerActive: true, state: { isAtRisk: false, atRiskReasons: [], progressPct: 0, taskOpen: 0, taskOverdue: 0, taskBlocked: 0, derivedPredecessorIds: [], derivedSuccessorIds: [] }, tasks: [] }, description: 'Synthetic deliverable detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name }, permissions: { ...panelPermissions, transitions: [], issueNeedsConfirm: false }, predecessors: [], successors: [], issues: [], derivedPredecessors: [], derivedSuccessors: [], relatedTasks: [], people: [] }
const decisionDetail = { decision: { id: panelIds.decision, projectId: project.id, key: 'DEC-001', subject: 'Synthetic decision', status: 'Pending', impactLevel: 'Medium', impactDescription: '', blockingTaskIds: [], ownerUserId: null, ownerName: 'Alex Chen', requestedById: null, requestedByName: 'Alex Chen', dateRequested: '2026-10-01', requiredByDate: '2026-10-20', originalRequiredByDate: '2026-10-20', isOverdue: false, daysOverdue: 0, daysUntil: 1, isDueSoon: false, isInactiveOwner: false, rowVersion: 1 }, description: 'Synthetic decision detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name }, permissions: { ...panelPermissions, transitions: [] }, links: [], decidedBy: null, deferralReason: null, cancelledReason: null }
const actionDetail = { action: { id: panelIds.action, projectId: project.id, projectNumber: project.projectNumber, key: 'A-001', meetingId: '00000000-0000-0000-0000-000000000031', meetingTitle: 'Synthetic meeting', meetingDate: '2026-10-01', text: 'Synthetic action', ownerType: 'User', ownerTypeLabel: 'User', ownerName: 'Alex Chen', noLead: false, partyIsClient: false, dueDate: '2026-10-20', isOverdue: false, daysOverdue: 0, status: 'Open', converted: false, rowVersion: 1 }, meeting: { id: '00000000-0000-0000-0000-000000000031', title: 'Synthetic meeting', meetingDate: '2026-10-01' }, permissions: { ...panelPermissions, transitions: [], convert: false }, links: [], task: null, decision: null }
const riskDetail = { risk: { id: panelIds.risk, projectId: project.id, key: 'R-001', title: 'Synthetic risk', status: 'Open', band: 'Medium', score: 4, probability: 2, impact: 2, ownerId: null, ownerName: 'Alex Chen', projectDisciplineId: project.projectDisciplines[0].id, reviewDate: '2026-10-20', isReviewOverdue: false, mitigation: 'Synthetic mitigation', triggerIndicator: 'Synthetic trigger', rowVersion: 1 }, description: 'Synthetic risk detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name }, permissions: { ...panelPermissions, transitions: [] }, links: [], realisedIssue: null }
const issueDetail = { issue: { id: panelIds.issue, projectId: project.id, key: 'I-001', title: 'Synthetic issue', status: 'Open', issueType: 'General', raisedById: null, raisedByName: 'Alex Chen', ownerId: null, ownerName: 'Alex Chen', severity: 'Medium', dateRaised: '2026-10-01', targetResolutionDate: '2026-10-20', isOverdue: false, daysOverdue: 0, rowVersion: 1, affectedDisciplineIds: [] }, description: 'Synthetic issue detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name }, permissions: { ...panelPermissions, transitions: [], comment: true }, links: [], originRisk: null }

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
    else if (p === `milestones/${panelIds.milestone}`) data = milestoneDetail
    else if (p === `deliverables/${panelIds.deliverable}`) data = deliverableDetail
    else if (p === `decisions/${panelIds.decision}`) data = decisionDetail
    else if (p === `actions/${panelIds.action}`) data = actionDetail
    else if (p === `risks/${panelIds.risk}`) data = riskDetail
    else if (p === `issues/${panelIds.issue}`) data = issueDetail
    else if (p === `projects/${project.id}/external-parties`) data = []
    else if (p === `projects/${project.id}/changes/options`) data = { actorId: me.id, canWrite: false, manageDisciplineIds: [], people: [], disciplines: [], sources: [], heads: [], deliverables: [], tasks: [] }
    else if (p === `issues/${panelIds.issue}/locations`) data = []
    else if (p === `issues/${panelIds.issue}/documents`) data = []
    else if (p === `issues/${panelIds.issue}/verification`) data = []
    else if (p === `issues/${panelIds.issue}/reference-impacts`) data = []
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
    else if (/^items\/(Milestone|Deliverable|Decision|Action|Risk|Issue)\/[^/]+\/comments$/.test(p)) data = comments
    else if (/^items\/(Milestone|Deliverable|Decision|Action|Risk|Issue)\/[^/]+\/links$/.test(p)) data = links
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
module.exports = { startPreview, newMockPage, mockPlatformApi, project, task, taskDetail, work, views, planningGrid, links, comments, panelIds, deliverableDetail }
