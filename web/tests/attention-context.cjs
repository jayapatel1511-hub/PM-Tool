// Project coordination attention rows may show current record facts; My Work remains on its legacy path.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail } = require('./platform-fixtures.cjs')
const { weeklyCoordination } = require('./dashboard.fixture.cjs')
const { attentionRow, attentionList } = require('./evaluation.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const diane = '00000000-0000-0000-0000-000000000007'
const staleId = '00000000-0000-0000-0000-000000000201'
const unknownId = '00000000-0000-0000-0000-000000000202'
const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const currentContext = { owner: person(me.id, 'Alex Chen'), nextActors: [person(diane, 'Diane Review')], neededBy: '2026-10-07', nextAction: 'review', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [], available: true }
const unavailableContext = { owner: person(null, null, 'Unknown'), nextActors: [], neededBy: null, nextAction: 'not_assessed', terminal: false, actorState: 'Unknown', requiresReviewGate: false, nextTransitions: [], inputs: [], available: false }
const attentionRows = () => [
  attentionRow({ id: '00000000-0000-0000-0000-000000000200', projectId: project.id, projectNumber: project.projectNumber, projectDisciplineId: project.disciplines[0].id, itemId: task.id, itemType: 'Task', itemKey: 'P-MOCK-T001', itemName: 'Current task attention', ownerName: 'PM', dueDate: '2026-10-02', context: currentContext }),
  attentionRow({ id: staleId, projectId: project.id, projectNumber: project.projectNumber, projectDisciplineId: project.disciplines[0].id, itemId: unknownId, itemType: 'Issue', itemKey: 'P-MOCK-I201', itemName: 'Stale source attention', ownerName: 'PM', context: unavailableContext }),
  attentionRow({ id: unknownId, projectId: project.id, projectNumber: project.projectNumber, projectDisciplineId: project.disciplines[0].id, itemId: unknownId, itemType: 'Issue', itemKey: 'P-MOCK-I202', itemName: 'Unknown source attention', ownerName: 'PM', context: unavailableContext }),
]

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', e => errors.push(e)); page.on('console', m => { if (m.type() === 'error') errors.push(new Error(m.text())) })
    const rows = attentionRows()
    await page.route(`**/api/v1/projects/${project.id}/coordination*`, route => route.fulfill({ json: weeklyCoordination({ window: { thisWeek: { from: '2026-10-01', to: '2026-10-07' }, nextWeek: { from: '2026-10-08', to: '2026-10-14' }, since: '2026-09-01T00:00:00Z', lastCoordinationReviewedAt: null, reviewedBy: null, canMarkReviewed: true } }) }))
    await page.route(`**/api/v1/projects/${project.id}/attention*`, route => route.fulfill({ json: attentionList(rows) }))
    await page.route(`**/api/v1/projects/${project.id}/team`, route => route.fulfill({ json: { members: [{ userId: me.id, displayName: 'Alex Chen' }, { userId: diane, displayName: 'Diane Review' }] } }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: { ...taskDetail, task: { ...taskDetail.task, key: 'P-MOCK-T001', name: 'Current task attention', status: 'In Review', lane: 'In Review', reviewerId: diane, requiresReview: true, dueDate: '2026-10-07' }, context: currentContext } }))
    await page.route(`**/api/v1/projects/${project.id}/discipline-coordination*`, route => route.fulfill({ json: { evaluatedAt: '2026-10-01T12:00:00Z', outgoing: [], incoming: [], outgoingPage: 1, incomingPage: 1, outgoingTotal: 0, incomingTotal: 0, handoffPage: 1, handoffPageSize: 0, handoffs: [], handoffsTotal: 0, changes: [], changesPage: 1, changesPageSize: 0, changesTotal: 0, reviews: [], reviewsPage: 1, reviewsPageSize: 0, reviewsTotal: 0, uses: [], usesPage: 1, usesPageSize: 0, usesTotal: 0, linkedIssues: [], linkedIssuesPage: 1, linkedIssuesPageSize: 0, linkedIssuesTotal: 0, linkedActions: [], linkedActionsTotal: 0, blockerGroups: [], blockerGroupsPage: 1, blockerGroupsTotal: 0, startability: [], startabilityPage: 1, startabilityPageSize: 0, startabilityReadyTotal: 0, startabilityTotal: 0, startabilityFrom: '2026-10-01', startabilityTo: '2026-10-31', unavailableChangeTargets: [], unavailableChangeTargetsTotal: 0, changeTargets: [], changeTargetsTotal: 0, upcomingSubmissions: [], upcomingSubmissionsPage: 1, upcomingSubmissionsTotal: 0, staffingConflicts: [], staffingConflictsPage: 1, staffingConflictsTotal: 0, page: 1, pageSize: 0 } }))

    const url = `${base}/projects/${project.projectNumber}/coordination?discipline=${project.disciplines[0].id}&meeting=1&sort=due`
    me.flags = { ...me.flags, screen_patterns: false }
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: /Coordination/ }).first().waitFor()
    const attention = page.locator('#wc-attention')
    await attention.getByText('P-MOCK-T001', { exact: true }).waitFor()
    assert.equal(await attention.locator('section[aria-label="Record context"]').count(), 0, 'flag-off leaves attention rows without context')
    assert.equal(await attention.getByText('P-MOCK-T001', { exact: true }).count(), 1)

    me.flags.screen_patterns = true
    await page.reload()
    const current = attention.getByText('P-MOCK-T001', { exact: true }).locator('xpath=ancestor::li[1]')
    await current.locator('section[aria-label="Record context"]').getByText('You', { exact: true }).waitFor()
    await current.locator('section[aria-label="Record context"]').getByText('Diane Review', { exact: true }).waitFor()
    await current.locator('section[aria-label="Record context"]').getByText('Needed by: 2026-10-07', { exact: true }).waitFor()
    await current.getByText(/^Alert owner: PM/).waitFor()
    const unavailable = attention.getByText('P-MOCK-I201', { exact: true }).locator('xpath=ancestor::li[1]')
    await unavailable.getByText('Current record facts unavailable; next step not assessed.', { exact: true }).waitFor()
    assert.equal(await unavailable.locator('section[aria-label="Record context"]').count(), 1); await unavailable.getByText('Needed by: Unknown', { exact: true }).waitFor(); await unavailable.getByText('Not assessed', { exact: true }).waitFor()
    assert.equal(await attention.getByText('P-MOCK-I202', { exact: true }).count(), 1, 'unavailable facts do not collapse the remaining list')
    await attention.getByRole('button', { name: /Why/i }).first().waitFor()
    await attention.getByRole('button', { name: /Snooze/i }).first().waitFor()

    const origin = page.url()
    const source = current.getByRole('button', { name: /^P-MOCK-T001/ }); await source.scrollIntoViewIfNeeded()
    const scroll = await page.locator('main#content').evaluate(el => el.scrollTop)
    assert.ok(scroll > 0, 'attention panel has a real overflowing main scroll container')
    await source.click()
    await page.getByRole('dialog').getByRole('heading').first().waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click()
    await page.waitForURL(origin)
    await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, scroll)
    assert.match(page.url(), /meeting=1/); assert.match(new URL(page.url()).search, /sort=due/)
    assert.equal(await attention.getByText('P-MOCK-T001', { exact: true }).count(), 1)
    assert.deepEqual(mock.unknownGets, [], 'no unmocked GETs'); assert.deepEqual(mock.apiErrors, [], 'no mocked API errors')
    assert.deepEqual(errors, [], errors.map(e => e.stack ?? e.message).join('\n'))
    console.log('PASS: project attention context, unavailable-source notice, controls and panel return state')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw error } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
