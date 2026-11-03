// Weekly coordination context is projected into both the project and workspace roots.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { coordinationProjection } = require('./coordination.fixture.cjs')
const { weeklyCoordination } = require('./dashboard.fixture.cjs')
const { handoffRow, handoffPage, handoffOptions, handoffDetail } = require('./handoffs.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')
const ownerId = me.id
const receiverId = '00000000-0000-0000-0000-000000000007'
const receiverName = 'Diane Review'
const handoffDraftId = '00000000-0000-0000-0000-000000000041'
const handoffSubmittedId = '00000000-0000-0000-0000-000000000042'
const reviewId = '00000000-0000-0000-0000-000000000043'
const useId = '00000000-0000-0000-0000-000000000044'
const disciplineId = project.disciplines[0].id

const person = (id, name, state = 'Active', kind = 'User') => ({ id, name, state, kind })
const context = (overrides = {}) => ({
  owner: person(ownerId, 'Alex Chen'), nextActors: [person(ownerId, 'Alex Chen')],
  neededBy: '2026-10-07', nextAction: 'submit', terminal: false, actorState: 'Active',
  requiresReviewGate: false, nextTransitions: [], inputs: [], ...overrides,
})
const projectProjection = () => coordinationProjection({
  outgoing: [{ id: handoffDraftId, key: 'P-MOCK-H041', title: 'Draft handoff', status: 'Draft', neededBy: '2026-10-07', promisedBy: null, sendingDisciplineId: disciplineId, receivingDisciplineId: disciplineId, sendingOwnerId: ownerId, receivingOwnerId: receiverId, targetTaskId: null, targetDeliverableId: null, context: context() },
    { id: handoffSubmittedId, key: 'P-MOCK-H042', title: 'Submitted handoff', status: 'Submitted', neededBy: '2026-10-08', promisedBy: null, sendingDisciplineId: disciplineId, receivingDisciplineId: disciplineId, sendingOwnerId: ownerId, receivingOwnerId: receiverId, targetTaskId: null, targetDeliverableId: null, context: context({ owner: person(ownerId, 'Alex Chen'), nextActors: [person(receiverId, receiverName)], nextAction: 'accept', neededBy: '2026-10-08' }) }],
  incoming: [], outgoingTotal: 2, incomingTotal: 0, handoffPageSize: 4,
  reviews: [{ id: reviewId, key: 'P-MOCK-R043', title: 'Unknown package', status: 'In Review', outstandingDisciplines: 1, blockingFindings: 0, coordinatorId: ownerId, context: context({ nextActors: [], actorState: 'Unknown', nextAction: 'not_assessed', neededBy: null }) }], reviewsTotal: 1, reviewsPageSize: 4,
  uses: [{ id: useId, targetType: 'Task', targetId: '00000000-0000-0000-0000-000000000012', targetKey: 'P-MOCK-T012', targetName: 'Unknown input use', sourceRevisionId: '00000000-0000-0000-0000-000000000013', sourceIdentity: 'survey', adoptedAt: '2026-10-01T12:00:00Z', adoptedBy: ownerId, ownerId, rowVersion: 1, intendedUse: 'Unknown use', sourceKey: 'P-MOCK-B013', sourceUrl: null, revision: null, context: context({ nextActors: [], actorState: 'Unknown', nextAction: 'not_assessed', neededBy: null }) }], usesTotal: 1, usesPageSize: 4,
  upcomingSubmissions: Array.from({ length: 4 }, (_, index) => ({ id: `00000000-0000-0000-0000-${String(index + 200).padStart(12, '0')}`, key: `P-MOCK-S${index + 1}`, title: `Draft submission ${index + 1}`, status: 'Draft', effectiveStatus: 'Draft', targetDate: '2026-10-12', coordinatorId: ownerId, failingChecks: [], context: context({ nextAction: 'start_checking', neededBy: '2026-10-12' }) })), upcomingSubmissionsTotal: 4,
  page: 1, pageSize: 4,
})
const workspaceProjection = () => ({ actorId: ownerId, scope: 'workspace', evaluatedAt: '2026-10-01T12:00:00Z', projectChoices: [{ id: project.id, projectNumber: project.projectNumber, name: project.name, projectManagerId: ownerId }], disciplines: [{ id: disciplineId, name: 'Civil' }], owners: [{ id: ownerId, displayName: 'Alex Chen' }, { id: receiverId, displayName: receiverName }], projects: [{ id: project.id, projectNumber: project.projectNumber, name: project.name, disciplineId, data: projectProjection() }] })

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', error => errors.push(error))
    page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
    const handoff = handoffRow({ id: handoffDraftId, key: 'P-MOCK-H041', title: 'Draft handoff', status: 'Draft' })
    await page.route(`**/api/v1/projects/${project.id}/attention*`, route => route.fulfill({ json: { items: [], snoozed: 0, total: 0, truncated: false } }))
    await page.route(`**/api/v1/projects/${project.id}/team`, route => route.fulfill({ json: { members: [{ userId: ownerId, displayName: 'Alex Chen' }, { userId: receiverId, displayName: receiverName }] } }))
    await page.route(`**/api/v1/projects/${project.id}/coordination*`, route => route.fulfill({ json: weeklyCoordination() }))
    await page.route(`**/api/v1/projects/${project.id}/discipline-coordination*`, route => route.fulfill({ json: projectProjection() }))
    await page.route('**/api/v1/discipline-coordination*', route => route.fulfill({ json: workspaceProjection() }))
    await page.route(`**/api/v1/projects/${project.id}/handoffs/options`, route => route.fulfill({ json: handoffOptions() }))
    await page.route(`**/api/v1/projects/${project.id}/handoffs/${handoffDraftId}`, route => route.fulfill({ json: handoffDetail({ row: handoff }) }))
    await page.route(`**/api/v1/projects/${project.id}/handoffs*`, route => route.fulfill({ json: handoffPage([handoff]) }))

    me.flags = { ...me.flags, screen_patterns: false }
    const projectUrl = `${base}/projects/${project.projectNumber}/coordination?discipline=${disciplineId}&owner=${ownerId}&from=2026-10-01&to=2026-10-14&sort=due`
    await page.goto(projectUrl, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: /Coordination/ }).first().waitFor()
    await page.getByRole('link', { name: /P-MOCK-H041$/ }).first().waitFor()
    assert.equal(await page.locator('section[aria-label="Record context"]').count(), 0, 'flag-off keeps the legacy project coordination rows unchanged')
    assert.equal(await page.getByRole('link', { name: /P-MOCK-H041$/ }).count(), 1)

    me.flags.screen_patterns = true
    await page.reload()
    const draftRow = page.getByText('P-MOCK-H041', { exact: true }).locator('..').locator('..')
    await draftRow.locator('section[aria-label="Record context"]').getByText('You', { exact: true }).first().waitFor()
    await draftRow.locator('section[aria-label="Record context"]').getByText('Needed by: 2026-10-07', { exact: true }).waitFor()
    const submittedRow = page.getByText('P-MOCK-H042', { exact: true }).locator('..').locator('..')
    await submittedRow.locator('section[aria-label="Record context"]').getByText(receiverName, { exact: true }).waitFor()
    await page.getByText('P-MOCK-R043', { exact: true }).locator('..').locator('..').locator('section[aria-label="Record context"]').getByText('Unknown', { exact: true }).waitFor()
    await page.getByText('P-MOCK-T012', { exact: true }).locator('..').locator('..').locator('section[aria-label="Record context"]').getByText('Not assessed', { exact: true }).waitFor()

    const before = page.url()
    const sourceLink = page.getByRole('link', { name: /P-MOCK-H041$/ }).first(); await sourceLink.scrollIntoViewIfNeeded(); const sourceScroll = await page.locator('main#content').evaluate(el => { el.scrollTop = 80; return el.scrollTop }); assert.equal(sourceScroll, 80); await sourceLink.click()
    await page.waitForURL(url => url.pathname.endsWith('/handoffs') && url.searchParams.get('panel') === `Handoff:${handoffDraftId}`)
    await page.getByRole('dialog').getByRole('heading', { name: /P-MOCK-H041.*Draft handoff/ }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click()
    await page.waitForURL(before)
    await page.waitForFunction(top => document.querySelector('main#content')?.scrollTop === top, sourceScroll)

    me.flags.screen_patterns = false
    const workspaceUrl = `${base}/my-work?tab=coordination&projectId=${project.id}&disciplineId=${disciplineId}&ownerId=${ownerId}&from=2026-10-01&to=2026-10-14&sort=due`
    await page.goto(workspaceUrl, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: /Coordination/ }).first().waitFor()
    await page.getByRole('link', { name: /P-MOCK-H041$/ }).first().waitFor()
    assert.equal(await page.locator('section[aria-label="Record context"]').count(), 0, 'flag-off keeps the legacy workspace rows unchanged')
    me.flags.screen_patterns = true
    await page.reload()
    await page.getByRole('link', { name: /P-MOCK-H041$/ }).first().locator('xpath=ancestor::li[1]').locator('section[aria-label="Record context"]').getByText('You', { exact: true }).first().waitFor()
    await page.getByRole('link', { name: /P-MOCK-H042$/ }).first().locator('xpath=ancestor::li[1]').locator('section[aria-label="Record context"]').getByText(receiverName, { exact: true }).waitFor()
    const workspaceBefore = page.url(); const workspaceScroll = await page.locator('main#content').evaluate(el => { el.scrollTop = 80; return el.scrollTop }); assert.equal(workspaceScroll, 80); await page.getByRole('link', { name: /P-MOCK-H041$/ }).first().click(); await page.getByRole('dialog').getByRole('heading', { name: /P-MOCK-H041.*Draft handoff/ }).waitFor(); await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(workspaceBefore); await page.waitForFunction(top => document.querySelector('main#content')?.scrollTop === top, workspaceScroll);
    assert.match(page.url(), /tab=coordination/); assert.match(page.url(), /sort=due/)
    assert.equal(await page.getByRole('link', { name: /P-MOCK-H041$/ }).count(), 1, 'context does not change row count or paging')
    assert.deepEqual(mock.unknownGets, [], 'no unmocked GETs')
    assert.deepEqual(mock.apiErrors, [], 'no mocked API errors')
    assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n'))
    console.log('PASS: weekly coordination context project/workspace roots, flag gate, actors, unknowns, navigation and scope retention')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw error } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
