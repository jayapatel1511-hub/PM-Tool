// Weekly agenda rows consume the optional context projection without changing the agenda itself.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail, deliverableDetail } = require('./platform-fixtures.cjs')
const { weeklyCoordination } = require('./dashboard.fixture.cjs')
const { taskRow } = require('./tasks.fixture.cjs')
const { deliverableRow, deliverableState } = require('./deliverables.fixture.cjs')
const { actionRow } = require('./meetings.fixture.cjs')
const { issueRow, riskRow } = require('./registers.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const alex = me.id
const diane = '00000000-0000-0000-0000-000000000007'
const ids = { normal: task.id, completed: '00000000-0000-0000-0000-000000000101', upcoming: '00000000-0000-0000-0000-000000000102', held: '00000000-0000-0000-0000-000000000103', completedDeliverable: '00000000-0000-0000-0000-000000000104', upcomingDeliverable: '00000000-0000-0000-0000-000000000105', heldDeliverable: '00000000-0000-0000-0000-000000000106', decision: '00000000-0000-0000-0000-000000000107', issue: '00000000-0000-0000-0000-000000000108', risk: '00000000-0000-0000-0000-000000000109', action: '00000000-0000-0000-0000-000000000110' }
const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const ctx = (id, neededBy = '2026-10-07', extra = {}) => ({ kind: 'Task', id, context: { owner: person(alex, 'Alex Chen'), nextActors: [person(diane, 'Diane Review')], neededBy, nextAction: 'review', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [], ...extra } })
const rows = () => {
  const normal = taskRow({ ...task, id: ids.normal, key: 'P-MOCK-T001', name: 'Normal agenda task', assigneeId: alex, assigneeName: 'Alex Chen', dueDate: '2026-10-07', status: 'In Review', lane: 'In Review', reviewerId: diane, requiresReview: true })
  const completed = taskRow({ id: ids.completed, projectId: project.id, key: 'P-MOCK-T101', name: 'Completed agenda task', status: 'Complete', lane: 'Complete', assigneeId: alex, assigneeName: 'Alex Chen', completedAt: '2026-10-04', dueDate: '2026-10-05' })
  const upcoming = taskRow({ id: ids.upcoming, projectId: project.id, key: 'P-MOCK-T102', name: 'Upcoming agenda task', assigneeId: alex, assigneeName: 'Alex Chen', dueDate: '2026-10-12' })
  const held = taskRow({ id: ids.held, projectId: project.id, key: 'P-MOCK-T103', name: 'Held agenda task', status: 'On Hold', lane: 'On Hold', assigneeId: alex, assigneeName: 'Alex Chen', dueDate: '2026-10-09', statusChangedAt: '2026-10-01T00:00:00Z' })
  const completedDeliverable = deliverableRow({ id: ids.completedDeliverable, projectId: project.id, key: 'P-MOCK-D104', name: 'Completed deliverable', status: 'Issued', issuedDate: '2026-10-04', dueDate: '2026-10-06', ownerId: alex, ownerName: 'Alex Chen', state: deliverableState() })
  const upcomingDeliverable = deliverableRow({ id: ids.upcomingDeliverable, projectId: project.id, key: 'P-MOCK-D105', name: 'Upcoming deliverable', dueDate: '2026-10-12', ownerId: alex, ownerName: 'Alex Chen', state: deliverableState() })
  const heldDeliverable = deliverableRow({ id: ids.heldDeliverable, projectId: project.id, key: 'P-MOCK-D106', name: 'Held deliverable', status: 'On Hold', dueDate: '2026-10-09', ownerId: alex, ownerName: 'Alex Chen', state: deliverableState({ isHeld: true }) })
  const decision = { decisionText: 'Synthetic completed decision', id: ids.decision, projectId: project.id, key: 'P-MOCK-DEC107', subject: 'Completed decision', status: 'Decided', ownerUserId: alex, ownerExternalPartyId: null, ownerName: 'Alex Chen', requiredByDate: '2026-10-06', decisionDate: '2026-10-05' }
  const issue = issueRow({ id: ids.issue, projectId: project.id, key: 'P-MOCK-I108', title: 'Agenda issue', targetResolutionDate: '2026-10-09', ownerId: alex, ownerName: 'Alex Chen' })
  const risk = riskRow({ id: ids.risk, projectId: project.id, key: 'P-MOCK-R109', title: 'Agenda risk', reviewDate: '2026-10-10', probability: 3, impact: 3, ownerId: alex, ownerName: 'Alex Chen' })
  const action = actionRow({ id: ids.action, projectId: project.id, key: 'P-MOCK-A110', text: 'Agenda action', ownerType: 'External Party', ownerTypeLabel: 'External party', ownerUserId: null, ownerExternalPartyId: '00000000-0000-0000-0000-000000000120', ownerName: 'Synthetic client', dueDate: '2026-10-07' })
  return { normal, completed, upcoming, held, completedDeliverable, upcomingDeliverable, heldDeliverable, decision, issue, risk, action }
}
const buildWeekly = () => {
  const r = rows()
  const normalRows = [r.normal, ...Array.from({ length: 9 }, (_, i) => taskRow({ id: `00000000-0000-0000-0000-${String(200 + i).padStart(12, '0')}`, projectId: project.id, key: `P-MOCK-T2${String(i).padStart(2, '0')}`, name: `Overflow agenda task ${i + 1}`, assigneeId: alex, assigneeName: 'Alex Chen', dueDate: '2026-10-07', status: 'In Review', lane: 'In Review', reviewerId: diane, requiresReview: true }))]
  const contexts = [
    ctx(ids.normal), ...normalRows.slice(1).map(x => ctx(x.id)), ctx(ids.completed, '2026-10-05', { nextActors: [], terminal: true, nextAction: 'none', actorState: 'None' }), ctx(ids.upcoming, '2026-10-12', { nextActors: [person(alex, 'Alex Chen')], nextAction: 'start' }), ctx(ids.held, '2026-10-09', { nextActors: [person(alex, 'Alex Chen')], nextAction: 'resume' }),
    { ...ctx(ids.completedDeliverable, '2026-10-06', { nextActors: [], terminal: true, nextAction: 'none', actorState: 'None' }), kind: 'Deliverable' }, { ...ctx(ids.upcomingDeliverable, '2026-10-12', { nextActors: [person(alex, 'Alex Chen')], nextAction: 'transition' }), kind: 'Deliverable' }, { ...ctx(ids.heldDeliverable, '2026-10-09', { nextActors: [person(alex, 'Alex Chen')], nextAction: 'transition' }), kind: 'Deliverable' },
    { kind: 'Decision', id: ids.decision, context: { owner: person(alex, 'Alex Chen'), nextActors: [], neededBy: '2026-10-06', nextAction: 'none', terminal: true, actorState: 'None', requiresReviewGate: false, nextTransitions: [], inputs: [] } },
    { kind: 'Issue', id: ids.issue, context: { owner: person(alex, 'Alex Chen'), nextActors: [person(alex, 'Alex Chen')], neededBy: '2026-10-09', nextAction: 'review_resolve_complete', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [] } },
    { kind: 'Risk', id: ids.risk, context: { owner: person(alex, 'Alex Chen'), nextActors: [person(alex, 'Alex Chen')], neededBy: '2026-10-10', nextAction: 'review', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [] } },
    { kind: 'Action', id: ids.action, context: { owner: person('00000000-0000-0000-0000-000000000120', 'Synthetic client', 'External', 'ExternalParty'), nextActors: [person('00000000-0000-0000-0000-000000000120', 'Synthetic client', 'External', 'ExternalParty')], neededBy: '2026-10-07', nextAction: 'complete', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [] } },
  ]
  return weeklyCoordination({
    dueThisWeek: normalRows, issues: [r.issue], risks: [r.risk], waiting: [r.action], completed: { tasks: [r.completed], deliverables: [r.completedDeliverable], decisions: [r.decision] },
    upcoming: { tasks: [r.upcoming], deliverables: [r.upcomingDeliverable] }, held: { tasks: [r.held], deliverables: [r.heldDeliverable] }, contexts,
  })
}

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', e => errors.push(e)); page.on('console', m => { if (m.type() === 'error') errors.push(new Error(m.text())) })
    const weekly = buildWeekly()
    await page.route(`**/api/v1/projects/${project.id}/coordination*`, route => route.fulfill({ json: weekly }))
    await page.route(`**/api/v1/projects/${project.id}/discipline-coordination*`, route => route.fulfill({ json: { evaluatedAt: '2026-10-01T12:00:00Z', outgoing: [], incoming: [], outgoingPage: 1, incomingPage: 1, outgoingTotal: 0, incomingTotal: 0, handoffPage: 1, handoffPageSize: 0, handoffs: [], handoffsTotal: 0, changes: [], changesPage: 1, changesPageSize: 0, changesTotal: 0, reviews: [], reviewsPage: 1, reviewsPageSize: 0, reviewsTotal: 0, uses: [], usesPage: 1, usesPageSize: 0, usesTotal: 0, linkedIssues: [], linkedIssuesPage: 1, linkedIssuesPageSize: 0, linkedIssuesTotal: 0, linkedActions: [], linkedActionsTotal: 0, blockerGroups: [], blockerGroupsPage: 1, blockerGroupsTotal: 0, startability: [], startabilityPage: 1, startabilityPageSize: 0, startabilityReadyTotal: 0, startabilityTotal: 0, startabilityFrom: '2026-10-01', startabilityTo: '2026-10-31', unavailableChangeTargets: [], unavailableChangeTargetsTotal: 0, changeTargets: [], changeTargetsTotal: 0, upcomingSubmissions: [], upcomingSubmissionsPage: 1, upcomingSubmissionsTotal: 0, staffingConflicts: [], staffingConflictsPage: 1, staffingConflictsTotal: 0, page: 1, pageSize: 0 } }))
    await page.route(`**/api/v1/projects/${project.id}/attention*`, route => route.fulfill({ json: { items: [], snoozed: 0, total: 0, truncated: false } }))
    await page.route(`**/api/v1/projects/${project.id}/team`, route => route.fulfill({ json: { members: [{ userId: alex, displayName: 'Alex Chen' }, { userId: diane, displayName: 'Diane Review' }] } }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: { ...taskDetail, task: rows().normal, context: ctx(ids.normal).context } }))
    await page.route(`**/api/v1/projects/${project.id}/tasks*`, route => route.fulfill({ json: { items: [task], page: 1, pageSize: 200, totalCount: 1 } }))

    const url = `${base}/projects/${project.projectNumber}/coordination?discipline=${project.disciplines[0].id}&meeting=1&sort=due`
    me.flags = { ...me.flags, screen_patterns: false }
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: /Coordination/ }).first().waitFor()
    assert.equal(await page.locator('section[aria-label="Record context"]').count(), 0, 'flag-off keeps agenda context hidden')
    me.flags.screen_patterns = true
    await page.reload()
    const row = key => page.getByText(key, { exact: true }).first().locator('..').locator('..')
    await row('P-MOCK-T001').locator('section[aria-label="Record context"]').getByText('Diane Review', { exact: true }).waitFor()
    await row('P-MOCK-T001').locator('section[aria-label="Record context"]').getByText('Needed by: 2026-10-07', { exact: true }).waitFor()
    for (const section of ['Completed', 'Upcoming', 'Held']) {
      const heading = page.getByRole('heading', { name: new RegExp(section, 'i') }).last()
      await heading.getByRole('button').click()
    }
    const completed = row('P-MOCK-T101')
    await completed.locator('section[aria-label="Record context"]').getByText('No next action', { exact: true }).waitFor()
    const decision = row('P-MOCK-DEC107')
    await decision.locator('section[aria-label="Record context"]').getByText('No next action', { exact: true }).waitFor()
    await decision.locator('section[aria-label="Record context"]').getByText('Needed by: 2026-10-06', { exact: true }).waitFor()
    assert.equal(await decision.locator('section[aria-label="Record context"]').getByText('2026-10-05').count(), 0, 'terminal decision context uses required-by, not decision date')
    for (const [key, date] of [['P-MOCK-I108', '2026-10-09'], ['P-MOCK-R109', '2026-10-10'], ['P-MOCK-A110', '2026-10-07']]) {
      const item = row(key)
      const expected = key.includes('A110') ? 'Synthetic client (No account)' : 'You'; await item.locator('section[aria-label="Record context"]').getByText(expected, { exact: true }).first().waitFor()
      await item.locator('section[aria-label="Record context"]').getByText(`Needed by: ${date}`, { exact: true }).waitFor()
    }
    const origin = page.url()
    const comment = row('P-MOCK-T001').getByRole('button', { name: /comment/i }); await comment.scrollIntoViewIfNeeded()
    const scroll = await page.locator('main#content').evaluate(el => el.scrollTop)
    assert.ok(scroll > 0, 'weekly agenda has a real overflowing main scroll container')
    await comment.click()
    await page.getByRole('dialog').getByRole('heading').first().waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click()
    await page.waitForURL(origin)
    await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, scroll)
    assert.match(page.url(), /meeting=1/); assert.match(new URL(page.url()).search, /sort=due/)
    assert.equal(await page.getByText('P-MOCK-T001', { exact: true }).count(), 1, 'context does not alter agenda count')
    assert.deepEqual(mock.unknownGets, [], 'no unmocked GETs'); assert.deepEqual(mock.apiErrors, [], 'no mocked API errors')
    assert.deepEqual(errors, [], errors.map(e => e.stack ?? e.message).join('\n'))
    console.log('PASS: weekly agenda context across normal/completed/upcoming/held rows, terminal decision, flag gate and meeting navigation')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw error } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
