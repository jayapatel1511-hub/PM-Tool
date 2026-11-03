// Coordination record-context coverage for handoffs and work constraints.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const { handoffRow, handoffPage, handoffOptions, handoffDetail } = require('./handoffs.fixture.cjs')
const { readinessWindow, windowConstraint, readinessDetail, readinessConstraintRow, readinessAssessment } = require('./readiness.fixture.cjs')
const { coordinationOptions } = require('./reviews.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const sender = '00000000-0000-0000-0000-000000000013'
const receiver = '00000000-0000-0000-0000-000000000007'
const handoffId = '00000000-0000-0000-0000-000000000041'
const constraintId = '00000000-0000-0000-0000-000000000042'
const inputTask = '00000000-0000-0000-0000-000000000012'
const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const context = (overrides = {}) => ({ owner: person(sender, 'Draft sender'), nextActors: [person(sender, 'Draft sender')], neededBy: '2026-11-04', nextAction: 'submit', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], reviewPackageGate: null, inputs: [], ...overrides })
let handoffState = 'Draft'
let constraintState = 'Open'
const handoff = handoffRow({ id: handoffId, projectId: project.id, key: 'H-001', title: 'Survey handoff', status: 'Draft', senderName: 'Draft sender', receiverName: 'Diane Receiver', sourceKey: 'D-001', targetKey: 'T-002', targetTaskId: inputTask, neededBy: '2026-11-04' })
const constraint = windowConstraint({ id: constraintId, targetId: task.id, key: 'CT-001', description: 'Permit constraint', state: 'Open', neededBy: '2026-11-05', removalOwnerId: sender, affectedOwnerId: receiver })

const handoffContext = () => {
  if (handoffState === 'Draft') return context({ owner: person(sender, 'Draft sender'), nextActors: [person(sender, 'Draft sender')], nextAction: 'submit' })
  if (handoffState === 'Submitted') return context({ owner: person(sender, 'Draft sender'), nextActors: [person(receiver, 'Diane Receiver')], nextAction: 'accept' })
  if (handoffState === 'Accepted') return context({ owner: person(sender, 'Draft sender'), nextActors: [person(receiver, 'Diane Receiver')], nextAction: 'incorporate' })
  if (handoffState === 'Clarification Requested' || handoffState === 'Returned') return context({ owner: person(sender, 'Draft sender'), nextActors: [person(sender, 'Draft sender')], nextAction: 'respond_and_resubmit' })
  return context({ owner: person(sender, 'Draft sender'), terminal: true, nextActors: [], actorState: 'None', nextAction: 'none' })
}
const constraintContext = () => {
  if (constraintState === 'Open') return context({ owner: person(sender, 'Removal owner'), nextActors: [person(sender, 'Removal owner')], neededBy: constraint.neededBy, nextAction: 'remove', inputs: [] })
  if (constraintState === 'Resolution Proposed') return context({ owner: person(sender, 'Removal owner'), nextActors: [person(receiver, 'Affected owner')], neededBy: constraint.neededBy, nextAction: 'verify_removal', inputs: [] })
  return context({ owner: person(sender, 'Removal owner'), terminal: true, nextActors: [], actorState: 'None', neededBy: constraint.neededBy, nextAction: 'none', inputs: [] })
}

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  try {
    page.setDefaultTimeout(12000)
    await page.route(`**/api/v1/projects/${project.id}/handoffs**`, async route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith('/options')) return route.fulfill({ json: handoffOptions() })
      if (path.endsWith(`/${handoffId}`)) return route.fulfill({ json: handoffDetail({ row: { ...handoff, status: handoffState }, context: handoffContext() }) })
      return route.fulfill({ json: handoffPage([handoff]) })
    })
    await page.route(`**/api/v1/projects/${project.id}/readiness/window*`, route => route.fulfill({ json: readinessWindow({ constraints: [{ ...constraint, state: constraintState }, { ...constraint, id: '00000000-0000-0000-0000-000000000044', key: 'CT-002', description: 'Second constraint' }], constraintsTotal: 2 }) }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/constraints/${constraintId}*`, route => route.fulfill({ json: { ...constraint, state: constraintState, title: constraint.description, context: constraintContext() } }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/options`, route => route.fulfill({ json: coordinationOptions({ actorId: me.id, tasks: [{ id: task.id, key: task.key, name: task.name, projectDisciplineId: project.disciplines[0].id, ownerId: sender, status: 'In Progress', rowVersion: 1, canCommit: false, canManage: false }] }) }))
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: coordinationOptions({ actorId: me.id, tasks: [{ id: task.id, key: task.key, name: task.name, projectDisciplineId: project.disciplines[0].id, ownerId: sender, status: 'In Progress', rowVersion: 1, canCommit: false, canManage: false }] }) }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/Task/${task.id}`, route => route.fulfill({ json: readinessDetail({ assessment: readinessAssessment({ id: '00000000-0000-0000-0000-000000000043', targetId: task.id, ownerId: sender }) }) }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/Task/${task.id}/constraints`, route => route.fulfill({ json: [readinessConstraintRow({ id: constraintId, targetId: task.id, key: constraint.key, description: constraint.description, state: constraintState, neededBy: constraint.neededBy, removalOwnerId: sender, affectedOwnerId: receiver }), readinessConstraintRow({ id: '00000000-0000-0000-0000-000000000044', targetId: task.id, key: 'CT-002', description: 'Second constraint' })] }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/Task/${task.id}/submission-prerequisites`, route => route.fulfill({ json: [] }))
    await page.route(`**/api/v1/projects/${project.id}/weekly-commitments*`, route => route.fulfill({ json: { commitments: [], snapshots: [], total: 0, pageSize: 50 } }))
    await page.route(`**/api/v1/projects/${project.id}/design-basis*`, route => route.fulfill({ json: { items: [], page: 1, pageSize: 200, totalCount: 0 } }))
    Object.assign(me, { flags: { ...me.flags, screen_patterns: false } })
    const handoffUrl = `${base}/projects/${project.projectNumber}/handoffs?status=Draft&sort=neededBy:asc&panel=Handoff:${handoffId}`
    await page.goto(handoffUrl, { waitUntil: 'domcontentloaded' }); let dialog = page.getByRole('dialog'); await dialog.getByRole('heading', { name: /H-001 · Survey handoff/ }).waitFor(); assert.equal(await dialog.locator('section[aria-label="Record context"]').count(), 0, 'flag-off hides stale handoff context after headline loaded')
    me.flags.screen_patterns = true
    const checkContext = async (action, actor) => { const section = page.getByRole('dialog').locator('section[aria-label="Record context"]'); await section.locator('dt').filter({ hasText: 'Owner' }).locator('..').getByText('Draft sender', { exact: true }).waitFor(); await section.locator('dt').filter({ hasText: 'Next actor' }).locator('..').getByText(actor, { exact: true }).waitFor(); await section.locator('dt').filter({ hasText: 'Next action' }).locator('..').getByText(action, { exact: true }).waitFor(); await section.getByText(/2026-11-04/).waitFor() }
    for (const [state, action, actor] of [['Draft', 'Submit', 'Draft sender'], ['Submitted', 'Accept, clarify or return', 'Diane Receiver'], ['Accepted', 'Incorporate', 'Diane Receiver'], ['Clarification Requested', 'Respond and resubmit', 'Draft sender'], ['Returned', 'Respond and resubmit', 'Draft sender']]) {
      handoffState = state; await page.goto(handoffUrl, { waitUntil: 'domcontentloaded' }); dialog = page.getByRole('dialog'); await checkContext(action, actor)
    }
    handoffState = 'Incorporated'; await page.reload(); let section = page.getByRole('dialog').locator('section[aria-label="Record context"]'); await section.getByText('No next action', { exact: true }).waitFor(); assert.equal(await section.getByText('Next actor', { exact: true }).count(), 0); await section.locator('dt').filter({ hasText: 'Owner' }).locator('..').getByText('Draft sender', { exact: true }).waitFor(); await section.getByText(/2026-11-04/).waitFor()
    constraintState = 'Open'; me.flags.screen_patterns = false; await page.goto(`${base}/projects/${project.projectNumber}/readiness?from=2026-11-01&to=2026-11-14&panel=WorkConstraint:${constraintId}`, { waitUntil: 'domcontentloaded' }); let inspector = page.getByRole('dialog'); await inspector.getByRole('heading', { name: /Inspect readiness/ }).waitFor(); assert.equal(await inspector.locator('section[aria-label="Record context"]').count(), 0, 'flag-off hides stale constraint context after inspector loaded'); me.flags.screen_patterns = true
    for (const state of ['Open', 'Resolution Proposed', 'Verified Removed']) { constraintState = state; await page.goto(`${base}/projects/${project.projectNumber}/readiness?from=2026-11-01&to=2026-11-14&panel=WorkConstraint:${constraintId}`, { waitUntil: 'domcontentloaded' }); inspector = page.getByRole('dialog'); await inspector.getByRole('heading', { name: /Inspect readiness/ }).waitFor(); const csection = inspector.locator('section[aria-label="Record context"]'); await csection.getByText(/2026-11-05/).waitFor(); await csection.locator('dt').filter({ hasText: 'Owner' }).locator('..').getByText('Removal owner', { exact: true }).waitFor(); if (state === 'Open') { await csection.locator('dt').filter({ hasText: 'Next actor' }).locator('..').getByText('Removal owner', { exact: true }).waitFor(); await csection.locator('dt').filter({ hasText: 'Next action' }).locator('..').getByText('Remove', { exact: true }).waitFor() } else if (state === 'Resolution Proposed') { await csection.locator('dt').filter({ hasText: 'Next actor' }).locator('..').getByText('Affected owner', { exact: true }).waitFor(); await csection.locator('dt').filter({ hasText: 'Next action' }).locator('..').getByText('Verify removal', { exact: true }).waitFor() } else { await csection.getByText('No next action', { exact: true }).waitFor(); assert.equal(await csection.getByText('Next actor', { exact: true }).count(), 0) } assert.equal(await inspector.locator('#readiness-fixed-work').getAttribute('readonly'), '', 'exact constraint keeps its work fixed'); const focused = inspector.locator('li.ring-2'); await focused.getByText('CT-001', { exact: true }).waitFor(); assert.equal(await inspector.locator('li').filter({ hasText: 'CT-002' }).count(), 1, 'second constraint remains visible') }
    assert.deepEqual(mock.unknownGets, [], `unmocked GETs: ${mock.unknownGets.join('; ')}`); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n')); console.log('PASS: coordination record context')
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
