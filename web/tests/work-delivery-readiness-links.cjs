// S03 approved one-target boundary: one shared Handoff source, exact direct/via-constraint membership, no fabricated Task blockers.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const { taskRow, taskState, taskDetail } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { weeklyCoordination } = require('./dashboard.fixture.cjs')
const { coordinationProjection } = require('./coordination.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const secondId = id(971), handoffId = id(972), constraintId = id(973), removalOwnerId = id(974)
const actor = { id: me.id, name: 'Alex Receiver', state: 'Active', kind: 'User' }
const removalOwner = { id: removalOwnerId, name: 'Priya Removal Owner', state: 'Active', kind: 'User' }
const first = taskRow({ ...task, key: 'P-MOCK-T0005', name: 'Direct receiving work', assigneeId: me.id, assigneeName: actor.name, dueDate: '2026-10-03', state: taskState({ isUnassigned: false, isMissingDueDate: false }) })
const second = taskRow({ ...first, id: secondId, key: 'P-MOCK-T0010', name: 'Work with its own constraint' })
const handoff = { kind: 'Handoff', id: handoffId, key: 'P-MOCK-H003', title: 'Shared survey handoff', neededBy: '2026-09-20', nextActors: [actor], nextAction: 'accept', primary: false, actorState: 'Active' }
const constraint = { kind: 'WorkConstraint', id: constraintId, key: 'P-MOCK-CT001', title: 'Verify the survey readiness independently', neededBy: '2026-09-25', nextActors: [removalOwner], nextAction: 'remove', primary: false, actorState: 'Active' }
const context = { owner: actor, nextActors: [actor], neededBy: '2026-10-03', nextAction: 'start', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [] }

;(async () => {
  const savedFlags = { ...me.flags }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = []; page.on('pageerror', error => errors.push(error.message))
  let accepted = false, removed = false
  const inputA = () => ({ code: 'Handoff', checkApplies: true, checkSatisfied: accepted, source: { ...handoff, nextAction: accepted ? 'incorporate' : 'accept' }, status: accepted ? 'Accepted' : 'Submitted', relationship: 'DirectTarget', contextAvailable: true, linkedSource: null, linkedStatus: null, linkedAvailable: true })
  const inputB = () => ({ code: 'Handoff', checkApplies: true, checkSatisfied: false, source: constraint, status: 'Open', relationship: 'Constraint', contextAvailable: true, linkedSource: { ...handoff, nextAction: accepted ? 'incorporate' : 'accept' }, linkedStatus: accepted ? 'Accepted' : 'Submitted', linkedAvailable: true })
  const readiness = inputs => ({ hasAssessment: true, available: true, state: 'Needs Assessment', unknown: ['Basis'], blocked: inputs.some(x => !x.checkSatisfied) ? ['Handoff'] : [], inputs })
  try {
    me.flags.screen_patterns = true
    me.flags.live_events = false
    for (const row of [first, second]) {
      await page.route(`**/api/v1/tasks/${row.id}`, route => route.fulfill({ json: taskDetail({ task: row, context, inputEffects: [], sourceUses: [], readinessInputs: readiness(row.id === first.id ? [inputA()] : removed ? [] : [inputB()]) }) }))
      await page.route(`**/api/v1/tasks/${row.id}/dependencies`, route => route.fulfill({ json: taskDependencies() }))
      await page.route(`**/api/v1/items/Task/${row.id}/comments`, route => route.fulfill({ json: { canComment: false, items: [], commentReason: null } }))
      await page.route(`**/api/v1/items/Task/${row.id}/links`, route => route.fulfill({ json: { canAdd: false, links: [], inherited: [] } }))
    }
    await page.route(`**/api/v1/projects/${project.id}/coordination*`, route => route.fulfill({ json: weeklyCoordination({ blocked: [], dueThisWeek: [first, second],
      contexts: [first, second].map(row => ({ kind: 'Task', id: row.id, context })),
      readinessInputs: [{ taskId: first.id, readiness: readiness([inputA()]) }, { taskId: second.id, readiness: readiness(removed ? [] : [inputB()]) }] }) }))
    await page.route(`**/api/v1/projects/${project.id}/discipline-coordination*`, route => route.fulfill({ json: coordinationProjection() }))
    await page.route(`**/api/v1/projects/${project.id}/attention*`, route => route.fulfill({ json: { items: [], total: 0, snoozed: 0, truncated: false } }))
    await page.route(`**/api/v1/projects/${project.id}/team`, route => route.fulfill({ json: { members: [] } }))
    const url = `${base}/projects/${project.projectNumber}/coordination?discipline=${project.disciplines[0].id}`
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    // Section IDs are stable anchors used by meeting navigation; the visible heading includes its index and count.
    const active = () => page.locator('#wc-readinessInputs')
    await active().getByRole('heading', { name: /Readiness inputs/ }).waitFor()
    await active().getByText('Readiness-linked work: 2', { exact: false }).waitFor()
    assert.equal(await active().getByRole('link', { name: /P-MOCK-H003 · Shared survey handoff/ }).count(), 1, 'shared source appears once')
    await active().getByText('Direct handoff target', { exact: false }).waitFor()
    await active().getByText(/Via constraint P-MOCK-CT001/).waitFor()
    await active().getByText(removalOwner.name, { exact: true }).waitFor()
    const constraintLink = active().getByRole('link', { name: /P-MOCK-CT001 · Verify the survey readiness independently/ })
    assert.ok((await constraintLink.getAttribute('href')).includes(constraintId), 'constraint opens its own stable identity')
    assert.equal(await page.getByText(/Also holds up/).count(), 0, 'readiness does not invent materialized Task blocking effects')
    await page.evaluate(() => { window.__workSummary = ''; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async text => { window.__workSummary = text } } }) })
    await page.getByRole('button', { name: 'More actions', exact: true }).first().click()
    await page.getByRole('menuitem', { name: 'Copy summary', exact: true }).click()
    await page.waitForFunction(() => window.__workSummary?.includes('P-MOCK-H003'))
    const summary = await page.evaluate(() => window.__workSummary)
    assert.equal((summary.match(/P-MOCK-H003/g) ?? []).length, 1, 'copied summary groups the source once')
    assert.ok(summary.includes('Direct handoff target') && summary.includes('Via constraint P-MOCK-CT001'))
    assert.ok(summary.includes(first.key) && summary.includes(second.key))
    await active().getByRole('button', { name: /P-MOCK-T0010 · Work with its own constraint/ }).click()
    const panel = () => page.getByRole('dialog').last()
    await panel().getByRole('heading', { name: second.name, exact: true }).waitFor()
    const inputs = () => panel().locator('section[aria-label="Readiness inputs"]')
    await inputs().getByText('Readiness constraint', { exact: true }).waitFor()
    await inputs().getByText('Linked source', { exact: true }).waitFor()
    assert.equal(await inputs().getByText('Direct handoff target', { exact: true }).count(), 0)
    assert.equal(await panel().locator('section[aria-label="Waiting on"]').count(), 0)
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(url)
    accepted = true; await page.reload()
    await active().getByRole('heading', { name: /Readiness inputs/ }).waitFor()
    await active().getByText(/Accepted · Readiness-linked work: 2/).waitFor()
    await active().getByText(/Open · The constraint remains until its own removal is verified/).waitFor()
    await active().getByRole('button', { name: /P-MOCK-T0005 · Direct receiving work/ }).click()
    await panel().getByRole('heading', { name: first.name, exact: true }).waitFor()
    await inputs().getByText(/Accepted · Handoff: Satisfied/).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    removed = true; await page.reload()
    await active().getByRole('heading', { name: /Readiness inputs/ }).waitFor()
    await active().getByText(/Readiness-linked work: 1/).waitFor()
    assert.equal(await active().getByRole('link', { name: /P-MOCK-CT001/ }).count(), 0)
    me.flags.screen_patterns = false; await page.reload()
    await page.getByRole('heading', { name: /Weekly Coordination/ }).first().waitFor()
    assert.equal(await active().count(), 0)
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: one direct Handoff target; one exact linked constraint; one shared Weekly source; acceptance preserves the open constraint; exact task return; unchanged-off presentation')
  } finally { me.flags = savedFlags; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
