// FR-SCR01 / FR-SCR02: the three registered record panels consume the same context contract.
// This is intentionally a real Chromium UI check against the local preview; API responses are synthetic,
// but panel routing, flags, labels, and the milestone command error are exercised through the actual SPA.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds } = require('./platform-fixtures.cjs')
const { deliverableRow, deliverableState, deliverableDetail } = require('./deliverables.fixture.cjs')
const { milestoneRow, milestoneDetail } = require('./milestones.fixture.cjs')
const { taskRow, taskDetail } = require('./tasks.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const alex = me.id
const diane = '00000000-0000-0000-0000-000000000007'
const inputTask = '00000000-0000-0000-0000-000000000012'
const inputDecision = '00000000-0000-0000-0000-000000000023'
const id = panelIds

const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const context = (overrides = {}) => ({
  owner: person(alex, 'Alex Chen'), nextActors: [person(diane, 'Diane Review')], neededBy: '2026-10-20',
  nextAction: 'transition', terminal: false, actorState: 'Active', requiresReviewGate: true,
  nextTransitions: ['Revision Required', 'On Hold', 'Cancelled'],
  // reviewPackageGate is present in the post-052 schema; keeping it in the fixture also catches a stale generated DTO.
  reviewPackageGate: { id: '00000000-0000-0000-0000-000000000099', key: 'PKG-001', title: 'Required review package', status: 'Draft', satisfied: false, reason: 'Required review package has not been approved.' },
  inputs: [
    { kind: 'Task', id: inputTask, key: 'P-MOCK-T002', title: 'Survey input', neededBy: null, nextActors: [], nextAction: 'start', primary: true },
    { kind: 'Decision', id: inputDecision, key: 'P-MOCK-D001', title: 'Scope decision', neededBy: null, nextActors: [], nextAction: 'decide', primary: false },
  ], ...overrides,
})

const deliverable = deliverableRow({ id: id.deliverable, projectId: project.id, key: 'D-001', name: 'Synthetic deliverable', ownerId: alex, ownerName: 'Alex Chen', reviewerId: diane, reviewerName: 'Diane Review', status: 'In Review', state: deliverableState() })
const milestone = milestoneRow({ id: id.milestone, projectId: project.id, key: 'M-001', name: 'Synthetic milestone', milestoneType: 'Design Submission', date: '2026-10-20', rowVersion: 1 })
project.permissions.manageMilestones = { ok: true, reason: null }
const inputTaskDetail = taskDetail({ task: taskRow({ id: inputTask, projectId: project.id, key: 'P-MOCK-T002', name: 'Survey input', projectNumber: project.projectNumber, projectName: project.name, projectStatus: project.status }) })
let terminalDecision = false
let workflowReview = false

const detailFor = (type) => {
  if (type === 'Deliverable') return deliverableDetail({ deliverable, description: 'Synthetic deliverable detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, permissions: { edit: { ok: false, reason: null }, issue: false, transitions: [], comment: true }, context: context({ nextActors: [person(alex, 'Alex Chen')], requiresReviewGate: workflowReview, inputs: [] }) })
  if (type === 'Milestone') return { ...milestoneDetail({ milestone, project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, permissions: { manage: { ok: true, reason: null }, comment: true } }), context: context({ owner: person('00000000-0000-0000-0000-000000000003', 'Priya Nair'), nextActors: [person('00000000-0000-0000-0000-000000000003', 'Priya Nair')], nextAction: 'review_prerequisites', nextTransitions: [], requiresReviewGate: false, reviewPackageGate: null, inputs: [] }) }
  return {
    decision: { id: id.decision, projectId: project.id, key: 'DEC-001', subject: 'Synthetic decision', status: terminalDecision ? 'Decided' : 'Pending', requestedById: alex, requestedByName: 'Alex Chen', ownerUserId: null, ownerExternalPartyId: '00000000-0000-0000-0000-000000000088', ownerName: 'Vendor party', ownerOrganisation: 'Vendor', ownerIsClient: false, dateRequested: '2026-10-03', requiredByDate: '2026-10-20', originalRequiredByDate: '2026-10-20', impactLevel: 'Medium', impactDescription: '', decisionText: null, rowVersion: 1, daysUntil: 1, isOverdue: false, daysOverdue: 0, isDueSoon: false, isInactiveOwner: true, blockingTaskIds: [], deliverables: [], milestones: [] },
    description: 'Synthetic decision detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, links: [], deferralReason: null, cancelledReason: null, decidedBy: null,
    permissions: { edit: { ok: false, reason: null }, transitions: [], comment: true },
    context: terminalDecision ? context({ terminal: true, owner: person('00000000-0000-0000-0000-000000000088', 'Vendor party', 'Inactive', 'ExternalParty'), nextActors: [], nextAction: 'none', actorState: 'None', requiresReviewGate: false, reviewPackageGate: null, inputs: [], nextTransitions: [] }) : context({ owner: person('00000000-0000-0000-0000-000000000088', 'Vendor party', 'Inactive', 'ExternalParty'), nextActors: [person('00000000-0000-0000-0000-000000000088', 'Vendor party', 'Inactive', 'ExternalParty')], actorState: 'Inactive', nextAction: 'decide', requiresReviewGate: false, reviewPackageGate: null, inputs: [], nextTransitions: [] }),
  }
}

;(async () => {
  const ui = await newMockPage(base)
  const { page } = ui
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error' && !/server responded with a status of 422/.test(message.text())) errors.push(new Error(message.text())) })
  try {
    page.setDefaultTimeout(12000)
    for (const type of ['Deliverable', 'Milestone', 'Decision']) {
      await page.route(`**/api/v1/${type === 'Deliverable' ? 'deliverables' : type === 'Milestone' ? 'milestones' : 'decisions'}/${id[type.toLowerCase()]}`, route => route.fulfill({ json: detailFor(type) }))
    }
    await page.route(`**/api/v1/projects/${project.id}/milestones`, route => route.fulfill({ json: [milestone] }))
    await page.route(`**/api/v1/tasks/${inputTask}`, route => route.fulfill({ json: inputTaskDetail }))
    await page.route(`**/api/v1/milestones/${id.milestone}/complete`, route => route.fulfill({ status: 422, contentType: 'application/problem+json', json: { code: 'too_early', detail: 'Milestone prerequisites are not satisfied.' } }))

    Object.assign(me, { flags: { ...me.flags, screen_patterns: false } })
    for (const type of ['Deliverable', 'Milestone', 'Decision']) {
      await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=${type}:${id[type.toLowerCase()]}`, { waitUntil: 'domcontentloaded' })
      const dialog = page.getByRole('dialog')
      await dialog.getByText(type === 'Decision' ? 'Synthetic decision' : type === 'Milestone' ? milestone.name : deliverable.name, { exact: true }).first().waitFor()
      assert.equal(await dialog.locator('section[aria-label="Record context"]').count(), 0, `${type}: flag-off hides stale context`)
    }

    Object.assign(me, { flags: { ...me.flags, screen_patterns: true } })
    await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Deliverable:${id.deliverable}`, { waitUntil: 'domcontentloaded' })
    let dialog = page.getByRole('dialog')
    const section = dialog.locator('section[aria-label="Record context"]')
    await section.getByText('You', { exact: true }).first().waitFor()
    assert.equal(await section.getByText('You', { exact: true }).count(), 2, 'owner and next actor both resolve to the current user')
    assert.equal(await section.getByText('Diane Review', { exact: true }).count(), 0, 'deliverable context does not substitute reviewer for owner/actor')
    await section.getByText(/PKG-001/).waitFor()
    await section.getByText(/Required review package has not been approved/).waitFor()
    assert.equal(await section.getByText('Review required.', { exact: true }).count(), 0, 'package gate does not invent the separate workflow review requirement')
    workflowReview = true
    await page.reload()
    await section.getByText('Review required.', { exact: true }).waitFor()
    await section.getByText(/Workflow transitions/).waitFor()
    assert.equal(await section.getByRole('button').count(), 0, 'context grants no mutation command')

    await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Milestone:${id.milestone}`, { waitUntil: 'domcontentloaded' })
    dialog = page.getByRole('dialog'); await dialog.waitFor()
    const milestoneContext = dialog.locator('section[aria-label="Record context"]')
    await milestoneContext.getByText(/Review prerequisites/).waitFor()
    assert.equal(await milestoneContext.getByText('Priya Nair', { exact: true }).count(), 2, 'milestone owner and next actor are its recorded project PM')
    assert.equal(await milestoneContext.getByText(/Workflow transitions/).count(), 0)
    assert.equal(await milestoneContext.getByText('Next actor', { exact: true }).count(), 1)
    await dialog.getByRole('button', { name: 'Close' }).click()
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
    await page.goto(`${base}/projects/${project.projectNumber}/milestones`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: `Actions for ${milestone.key}` }).waitFor()
    await page.getByRole('button', { name: `Actions for ${milestone.key}` }).click()
    await page.getByRole('menuitem', { name: 'Mark complete', exact: true }).click()
    const complete = page.getByRole('dialog').last()
    await complete.getByRole('button', { name: 'Mark complete', exact: true }).click()
    await complete.getByText(/Milestone prerequisites are not satisfied/).waitFor()
    assert.equal(await complete.getByText(/too_early/i).count(), 0, 'raw API code is not exposed as a command')

    await page.goto(`${base}/projects/${project.projectNumber}/tasks?panel=Decision:${id.decision}`, { waitUntil: 'domcontentloaded' })
    dialog = page.getByRole('dialog'); await dialog.waitFor()
    const decisionContext = dialog.locator('section[aria-label="Record context"]')
    await decisionContext.getByText(/Vendor party \(Inactive · No account\)/).first().waitFor()
    assert.equal(await decisionContext.getByText('Vendor party (Inactive · No account)', { exact: true }).count(), 2, 'decision owner remains its next actor')
    assert.equal(await decisionContext.getByText('You', { exact: true }).count(), 0, 'external actor is not substituted with PM')
    terminalDecision = true
    await page.reload()
    await dialog.locator('section[aria-label="Record context"]').getByText('No next action', { exact: true }).waitFor()
    assert.equal(await dialog.locator('section[aria-label="Record context"]').getByText('Next actor', { exact: true }).count(), 0, 'terminal decision has no next actor')
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked reads')
    assert.deepEqual(ui.mock.apiErrors, [], 'no mocked API errors')
    assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n'))
    console.log('PASS: deliverable, milestone, and decision record-context panels')
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
