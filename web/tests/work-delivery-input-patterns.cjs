// S03: current resolver facts, exact source revisions independent of task status, and one Weekly cause.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task } = require('./platform-fixtures.cjs')
const { taskRow, taskState, taskPage, taskDetail } = require('./tasks.fixture.cjs')
const { handoffRow, handoffPage, handoffOptions, handoffDetail } = require('./handoffs.fixture.cjs')
const { weeklyCoordination, dashboardDiscipline } = require('./dashboard.fixture.cjs')
const { coordinationProjection } = require('./coordination.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { decisionDetail } = require('./decisions.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const secondId = id(801), sourceTaskId = id(802), handoffId = id(803), assessmentId = id(804), noticeId = id(805)
const omar = id(806)
const waitingId = id(831), decisionId = id(832), waitingOtherId = id(833)
const predecessorId = id(834), mixedBlockedId = id(835), mixedWaitingId = id(836)
const receiver = { id: omar, name: 'Omar Receiver', state: 'Active', kind: 'User' }
const input = { kind: 'Handoff', id: handoffId, key: 'P-MOCK-H003', title: 'Survey for electrical', neededBy: '2026-09-20', nextActors: [receiver], nextAction: 'accept', primary: true, actorState: 'Active' }
const person = { id: me.id, name: 'Alex Chen', state: 'Active', kind: 'User' }
const context = { owner: person, nextActors: [receiver], nextAction: 'resolve_input', actorState: 'Active', neededBy: '2026-09-24', terminal: false, requiresReviewGate: false, nextTransitions: [], inputs: [input] }
const blockers = [{ type: 'handoff', id: handoffId, key: 'STALE-KEY', name: 'Stale blocker title', ownerId: me.id, due: '2000-01-01', overdue: true, blocking: true },
  { type: 'Handoff', id: handoffId, key: 'STALE-KEY', name: 'Stale blocker title', ownerId: me.id, due: '2000-01-01', overdue: true, blocking: true }]
const first = taskRow({ ...task, key: 'P-MOCK-T0005', name: 'First waiting work', assigneeId: me.id, assigneeName: 'Alex Chen', state: taskState({ isBlocked: true, blockedBy: blockers }) })
const second = taskRow({ ...first, id: secondId, key: 'P-MOCK-T0010', name: 'Second waiting work' })
const unstarted = taskRow({ ...task, id: sourceTaskId, key: 'P-MOCK-T0002', name: 'Survey-dependent work', status: 'Not Started', state: taskState() })
const waitingOnly = taskRow({ ...task, id: waitingId, key: 'P-MOCK-T0031', name: 'Waiting on a future decision', dueDate: '2026-10-07',
  state: taskState({ isWaiting: true, isBlocked: false, blockedBy: [{ type: 'decision', id: decisionId, key: 'DEC-031', name: 'Future scope decision', blocking: false, overdue: false }] }) })
const waitingOther = taskRow({ ...waitingOnly, id: waitingOtherId, key: 'P-MOCK-T0032', name: 'Second work awaiting the same decision' })
const waitingInput = { kind: 'Decision', id: decisionId, key: 'DEC-031', title: 'Future scope decision', neededBy: '2026-12-01', nextActors: [receiver], nextAction: 'decide', primary: true, actorState: 'Active' }
const waitingContext = { ...context, inputs: [waitingInput], unavailableInputCount: 1, actorState: 'Partial' }
const predecessor = taskRow({ ...task, id: predecessorId, key: 'P-MOCK-T0040', name: 'Shared survey predecessor', assigneeId: omar, assigneeName: receiver.name,
  status: 'In Progress', dueDate: '2026-11-24' })
const mixedInput = { kind: 'Task', id: predecessorId, key: predecessor.key, title: predecessor.name, neededBy: predecessor.dueDate,
  nextActors: [receiver], nextAction: 'complete', primary: true, actorState: 'Active' }
const mixedBlocked = taskRow({ ...task, id: mixedBlockedId, key: 'P-MOCK-T0041', name: 'Started successor with blocker', assigneeId: me.id, assigneeName: person.name,
  status: 'In Progress', startDate: '2026-10-01', dueDate: '2026-10-07', state: taskState({ isBlocked: true, isWaiting: false,
    blockedBy: [{ type: 'task', id: predecessorId, key: predecessor.key, name: predecessor.name, blocking: true, overdue: false }] }) })
const mixedWaiting = taskRow({ ...task, id: mixedWaitingId, key: 'P-MOCK-T0042', name: 'Future successor waiting for survey', assigneeId: me.id, assigneeName: person.name,
  startDate: '2026-10-12', dueDate: '2026-10-14', state: taskState({ isWaiting: true, isBlocked: false,
    blockedBy: [{ type: 'task', id: predecessorId, key: predecessor.key, name: predecessor.name, blocking: false, overdue: false }] }) })
const revision = (idValue, name) => ({ id: idValue, key: 'Survey-42', title: 'Survey corridor', revision: name, registeredAt: '2026-09-01T12:00:00Z', sourceCheckedAt: '2026-08-31T12:00:00Z' })
const sourceUse = { id: id(807), intendedUse: 'Set the corridor alignment', revisionUsed: revision(id(808), '2025A'), currentRegisteredRevision: revision(id(809), '2026B'), assessmentAvailable: true,
  assessments: [{ id: assessmentId, noticeId, key: 'P-MOCK-CN001', status: 'Pending Assessment', noticeStatus: 'Open' }] }

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  let unavailable = false, manual = false
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  try {
    page.setDefaultTimeout(12000)
    await page.addInitScript(() => Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async text => { window.__workCopiedSummary = text } } }))
    await page.route(`**/api/v1/projects/${project.id}/tasks*`, route => route.fulfill({ json: taskPage([first, second, unstarted]) }))
    await page.route('**/api/v1/projects/P-MOCK/tasks*', route => route.fulfill({ json: taskPage([first, second, unstarted]) }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: taskDetail({ task: manual ? { ...first, manualBlockType: 'Client', manualBlockReason: 'Await recorded instruction' } : first,
      context: manual ? { ...context, inputs: [], unavailableInputCount: 1, nextActors: [], nextAction: 'not_assessed', actorState: 'Unknown' } : context,
      inputEffects: manual ? [] : [{ kind: 'Handoff', id: handoffId, otherOpenTasks: [{ id: secondId, key: second.key, name: second.name }] }], sourceUses: [] }) }))
    await page.route(`**/api/v1/tasks/${secondId}`, route => route.fulfill({ json: taskDetail({ task: second, context, inputEffects: [{ kind: 'Handoff', id: handoffId, otherOpenTasks: [{ id: task.id, key: first.key, name: first.name }] }], sourceUses: [] }) }))
    await page.route(`**/api/v1/tasks/${sourceTaskId}`, route => route.fulfill({ json: taskDetail({ task: unstarted, context: { ...context, nextActors: [person], nextAction: 'start', inputs: [] }, inputEffects: [],
      sourceUses: [unavailable ? { ...sourceUse, revisionUsed: null, currentRegisteredRevision: null, assessmentAvailable: false, assessments: [] } : sourceUse] }) }))
    await page.route(`**/api/v1/tasks/${predecessorId}`, route => route.fulfill({ json: taskDetail({ task: predecessor,
      context: { ...context, owner: receiver, nextActors: [receiver], nextAction: 'complete', neededBy: predecessor.dueDate, inputs: [] }, inputEffects: [], sourceUses: [] }) }))
    for (const taskId of [secondId, sourceTaskId, predecessorId]) {
      await page.route(`**/api/v1/tasks/${taskId}/dependencies`, route => route.fulfill({ json: taskDependencies() }))
      await page.route(`**/api/v1/items/Task/${taskId}/links`, route => route.fulfill({ json: { canAdd: false, links: [], inherited: [] } }))
      await page.route(`**/api/v1/items/Task/${taskId}/comments`, route => route.fulfill({ json: { canComment: false, commentReason: null, items: [] } }))
    }
    const handoff = handoffRow({ id: handoffId, key: input.key, title: input.title, status: 'Submitted', senderName: 'Alex Chen', receiverName: receiver.name, neededBy: input.neededBy, targetTaskId: task.id })
    await page.route(`**/api/v1/projects/${project.id}/handoffs**`, route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith('/options')) return route.fulfill({ json: handoffOptions() })
      if (path.endsWith(`/${handoffId}`)) return route.fulfill({ json: handoffDetail({ row: handoff }) })
      return route.fulfill({ json: handoffPage([handoff]) })
    })
    const weekly = weeklyCoordination({ blocked: [first, second, mixedBlocked], dueThisWeek: [first, second, mixedBlocked, waitingOnly, waitingOther], upcoming: { tasks: [waitingOnly, mixedWaiting], deliverables: [] },
      disciplines: [{ discipline: dashboardDiscipline({ disciplineId: project.disciplines[0].id, name: 'Civil' }), top: [waitingOnly, waitingOther, mixedWaiting] }],
      contexts: [...[first, second].map(row => ({ kind: 'Task', id: row.id, context })), { kind: 'Task', id: waitingId, context: waitingContext },
        { kind: 'Task', id: waitingOtherId, context: { ...waitingContext, unavailableInputCount: 0 } },
        ...[mixedBlocked, mixedWaiting].map(row => ({ kind: 'Task', id: row.id, context: { ...context, inputs: [mixedInput], neededBy: row.dueDate } }))] })
    await page.route(`**/api/v1/decisions/${decisionId}`, route => route.fulfill({ json: { ...decisionDetail,
      decision: { ...decisionDetail.decision, id: decisionId, key: waitingInput.key, subject: waitingInput.title, ownerUserId: omar, ownerExternalPartyId: null, ownerName: receiver.name,
        ownerOrganisation: null, ownerIsClient: false, requiredByDate: waitingInput.neededBy, originalRequiredByDate: waitingInput.neededBy, isOverdue: false },
      context: { ...context, owner: receiver, nextAction: 'decide', neededBy: waitingInput.neededBy, inputs: [] } } }))
    weekly.headline.blockedTasks.value = 3
    await page.route(`**/api/v1/projects/${project.id}/coordination*`, route => route.fulfill({ json: weekly }))
    await page.route(`**/api/v1/projects/${project.id}/discipline-coordination*`, route => route.fulfill({ json: coordinationProjection() }))
    await page.route(`**/api/v1/projects/${project.id}/attention*`, route => route.fulfill({ json: { items: [], snoozed: 0, total: 0, truncated: false } }))
    await page.route(`**/api/v1/projects/${project.id}/team`, route => route.fulfill({ json: { members: [{ userId: me.id, displayName: 'Alex Chen' }, { userId: omar, displayName: receiver.name }] } }))

    const firstUrl = `${base}/projects/${project.projectNumber}/tasks?sort=dueDate%3Aasc&status=Not%20Started&panel=Task:${task.id}`
    const panel = () => page.getByRole('dialog').last()
    me.flags.screen_patterns = false
    await page.goto(firstUrl, { waitUntil: 'domcontentloaded' })
    await panel().getByRole('heading', { name: first.name, exact: true }).waitFor()
    assert.equal(await panel().locator('section[aria-label="Waiting on"]').count(), 0, 'enabled input presentation stays dark')
    me.flags.screen_patterns = true
    await page.reload()
    const waiting = () => panel().locator('section[aria-label="Waiting on"]')
    await waiting().getByText(receiver.name, { exact: true }).waitFor()
    await waiting().getByText('2026-09-20', { exact: true }).waitFor()
    assert.equal(await waiting().getByRole('link', { name: /P-MOCK-H003/ }).count(), 1, 'one canonical input despite duplicate historical blocker entries')
    assert.equal(await waiting().getByText('STALE-KEY', { exact: true }).count(), 0)
    await waiting().getByText('Also holds up 1 task', { exact: true }).click()
    await waiting().getByRole('button', { name: /P-MOCK-T0010 · Second waiting work/ }).click()
    await panel().getByRole('heading', { name: second.name, exact: true }).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(firstUrl)
    await panel().getByRole('heading', { name: first.name, exact: true }).waitFor()
    await waiting().getByRole('link', { name: /P-MOCK-H003/ }).click()
    await panel().getByRole('heading', { name: /P-MOCK-H003 · Survey for electrical/ }).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(firstUrl)

    const sourceUrl = `${base}/projects/${project.projectNumber}/tasks?panel=Task:${sourceTaskId}`
    await page.goto(sourceUrl, { waitUntil: 'domcontentloaded' })
    const sources = () => panel().locator('section[aria-label="Source uses"]')
    await sources().getByText('2025A → 2026B', { exact: true }).waitFor()
    await sources().getByRole('link', { name: /P-MOCK-CN001 · Pending Assessment/ }).waitFor()
    assert.ok((await sources().getByRole('link').getAttribute('href')).includes(`ChangeAssessment:${assessmentId}`), 'assessment opens the exact child record')
    assert.equal(await panel().locator('section[aria-label="Waiting on"]').count(), 0, 'Not Started unblocked work still exposes its Source Use')
    await sources().getByText(/Manually registered:/).first().waitFor()
    await sources().getByText(/Source checked:/).first().waitFor()
    unavailable = true
    await page.reload()
    await sources().getByText('Assessment status unavailable', { exact: true }).waitFor()
    assert.equal(await sources().getByText('No assessment recorded for the current registered revision').count(), 0, 'missing source evidence is not an empty known assessment')
    assert.equal(await sources().getByText(/2025A|2026B/).count(), 0)

    manual = true
    await page.goto(firstUrl, { waitUntil: 'domcontentloaded' })
    await waiting().getByText(/Await recorded instruction/).waitFor()
    await waiting().getByText(/Who must resolve it: Unknown.*Needed by: Unknown/).waitFor()
    manual = false
    const weeklyUrl = `${base}/projects/${project.projectNumber}/coordination?meeting=1&sort=dueDate%3Aasc`
    await page.goto(weeklyUrl, { waitUntil: 'domcontentloaded' })
    const cause = page.getByRole('link', { name: /P-MOCK-H003 · Survey for electrical/ })
    await cause.waitFor()
    assert.equal(await cause.count(), 1, 'Weekly discusses the shared handoff once, across both blocked and due rows')
    assert.equal(await page.getByText('STALE-KEY', { exact: true }).count(), 0)
    await cause.click()
    await panel().getByRole('heading', { name: /P-MOCK-H003 · Survey for electrical/ }).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(weeklyUrl)
    await cause.waitFor()
    assert.equal(await page.getByRole('link', { name: /P-MOCK-H003 · Survey for electrical/ }).count(), 1)
    const waitingCard = page.locator('#wc-thisWeek li').filter({ has: page.getByRole('button', { name: waitingOnly.name, exact: true }) })
    const waitingFacts = waitingCard.locator('section[aria-label="Record context"]')
    const waitingGroup = page.locator('#wc-waitingInputs')
    const decisionCause = () => page.getByRole('button', { name: 'DEC-031 · Future scope decision', exact: true })
    await waitingGroup.getByRole('button', { name: 'DEC-031 · Future scope decision', exact: true }).waitFor()
    await waitingGroup.getByText(receiver.name, { exact: true }).waitFor()
    await waitingGroup.getByText('2026-12-01', { exact: true }).waitFor()
    assert.equal(await decisionCause().count(), 1, 'shared Waiting-only source is rendered once across Due and discipline cards')
    assert.equal(await waitingGroup.getByRole('button', { name: /^P-MOCK-T003[12] ·/ }).count(), 2, 'shared cause names its two distinct affected tasks')
    await waitingFacts.getByText('1 input details unavailable; their next steps are not assessed.', { exact: true }).waitFor()
    await decisionCause().click()
    await panel().getByRole('heading', { name: waitingInput.title, exact: true }).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(weeklyUrl)
    assert.equal(await page.locator('#wc-h-blocked span').last().innerText(), '3', 'Waiting-only task does not change the canonical blocked count')
    await page.locator('#wc-h-upcoming').getByRole('button').click()
    await page.waitForFunction(() => document.querySelector('#wc-h-upcoming button')?.getAttribute('aria-expanded') === 'true')
    assert.equal(await decisionCause().count(), 1, 'expanding repeated Upcoming task keeps one Waiting source')
    assert.equal(await waitingGroup.getByRole('button', { name: /^P-MOCK-T003[12] ·/ }).count(), 2, 'Upcoming repetition does not inflate affected-task membership')
    await page.locator('#wc-h-round').getByRole('button').click()
    await page.locator('#wc-h-thisWeek').getByRole('button').click()
    await page.waitForFunction(() => document.querySelector('#wc-h-round button')?.getAttribute('aria-expanded') === 'false' && document.querySelector('#wc-h-thisWeek button')?.getAttribute('aria-expanded') === 'false')
    assert.equal(await decisionCause().count(), 1, 'Upcoming alone still owns one source group')
    assert.equal(await waitingGroup.getByRole('button', { name: /^P-MOCK-T003[12] ·/ }).count(), 1, 'collapsed sections do not contribute hidden tasks')
    await page.locator('#wc-h-upcoming').getByRole('button').click()
    await waitingGroup.waitFor({ state: 'detached' })
    assert.equal(await waitingGroup.count(), 0, 'no Waiting source group remains when all of its task sections are hidden')
    await page.locator('#wc-h-thisWeek').getByRole('button').click()
    await decisionCause().waitFor()
    assert.equal(await decisionCause().count(), 1)
    await page.getByRole('button', { name: 'More actions', exact: true }).first().click()
    await page.getByRole('menuitem', { name: 'Copy summary', exact: true }).click()
    await page.waitForFunction(() => window.__workCopiedSummary?.includes('DEC-031'))
    const summary = await page.evaluate(() => window.__workCopiedSummary)
    assert.equal(summary.split('DEC-031').length - 1, 1, 'Copy Summary discusses the Waiting-only decision once despite duplicate agenda appearances')
    assert.equal(summary.split('P-MOCK-H003').length - 1, 1, 'Copy Summary retains one shared blocked handoff cause')

    // A normal predecessor can hold both a started Blocked successor and a future
    // Waiting successor. Membership is unified before choosing its one placement.
    await page.locator('#wc-h-upcoming').getByRole('button').click()
    await page.waitForFunction(() => document.querySelector('#wc-h-upcoming button')?.getAttribute('aria-expanded') === 'true')
    await page.locator('#wc-h-round').getByRole('button').click()
    await page.waitForFunction(() => document.querySelector('#wc-h-round button')?.getAttribute('aria-expanded') === 'true')
    const mixedCause = page.getByRole('button', { name: `${predecessor.key} · ${predecessor.name}`, exact: true })
    await mixedCause.waitFor()
    assert.equal(await mixedCause.count(), 1, 'mixed Blocked/Waiting predecessor has one source placement')
    const mixedHeader = mixedCause.locator('..').locator('..')
    const members = mixedHeader.getByRole('list', { name: 'Other open tasks', exact: true })
    assert.equal(await members.getByRole('button', { name: /^P-MOCK-T004[12] ·/ }).count(), 2, 'one mixed-source group includes both distinct successors despite repeated sections')
    await members.getByRole('button', { name: `${mixedBlocked.key} · ${mixedBlocked.name}`, exact: true }).locator('..').getByText(/Blocked$/).waitFor()
    await members.getByRole('button', { name: `${mixedWaiting.key} · ${mixedWaiting.name}`, exact: true }).locator('..').getByText(/Waiting$/).waitFor()
    assert.equal(await page.locator('#wc-h-blocked span').last().innerText(), '3', 'mixed membership never promotes a Waiting task into the Blocked figure')
    await mixedCause.click()
    await panel().getByRole('heading', { name: predecessor.name, exact: true }).waitFor()
    await panel().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(weeklyUrl)
    await page.getByRole('button', { name: 'More actions', exact: true }).first().click()
    await page.getByRole('menuitem', { name: 'Copy summary', exact: true }).click()
    await page.waitForFunction(key => window.__workCopiedSummary?.includes(`${key} Future successor waiting for survey [Waiting]`), mixedWaiting.key)
    const mixedSummary = await page.evaluate(() => window.__workCopiedSummary)
    assert.equal(mixedSummary.split(predecessor.key).length - 1, 1, 'Copy Summary places a mixed source once')
    const mixedLine = mixedSummary.split('\n').find(line => line.includes(predecessor.key))
    assert.ok(mixedLine.includes(`${mixedBlocked.key} ${mixedBlocked.name} [Blocked]`), 'mixed source summary retains its Blocked member/state')
    assert.ok(mixedLine.includes(`${mixedWaiting.key} ${mixedWaiting.name} [Waiting]`), 'mixed source summary retains its Waiting member/state')
    await page.locator('#wc-h-blocked').getByRole('button').click()
    await page.waitForFunction(() => document.querySelector('#wc-h-blocked button')?.getAttribute('aria-expanded') === 'false')
    const relocated = page.locator('#wc-waitingInputs').getByRole('button', { name: `${predecessor.key} · ${predecessor.name}`, exact: true })
    await relocated.waitFor()
    assert.equal(await mixedCause.count(), 1, 'collapsing Blocked moves the mixed source to one Waiting-on placement')
    assert.equal(await relocated.locator('..').locator('..').getByRole('list', { name: 'Other open tasks', exact: true }).getByRole('button', { name: /^P-MOCK-T004[12] ·/ }).count(), 2,
      'placement change retains both still-visible mixed members')
    assert.equal(await page.locator('#wc-h-blocked span').last().innerText(), '3', 'placement change leaves the canonical Blocked figure intact')

    assert.deepEqual(mock.unknownGets, [], `unmocked GETs: ${mock.unknownGets.join('; ')}`)
    assert.deepEqual(mock.apiErrors, [])
    assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n'))
    console.log('PASS: work/delivery blocking inputs and source-use patterns')
  } catch (error) {
    console.error('work-input-diagnostics', { url: page.url(), unknownGets: mock.unknownGets, apiErrors: mock.apiErrors })
    console.error(await page.locator('body').innerText())
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
