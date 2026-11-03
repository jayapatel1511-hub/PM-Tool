// FR-SCR01: the real TaskPanel renders context only when the screen-pattern flag is on.
const assert = require('node:assert/strict')
const { newMockPage, me, task, taskDetail, project } = require('./platform-fixtures.cjs')
const { taskDetail: makeDetail, taskRow, taskPage: makeTaskPage } = require('./tasks.fixture.cjs')
const { handoffRow, handoffPage, handoffOptions, handoffDetail } = require('./handoffs.fixture.cjs')
const { readinessWindow, windowConstraint } = require('./readiness.fixture.cjs')
const { commitmentList } = require('./weekly-commitments.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Set TUESDAY_UI_BASE_URL to the local preview')

;(async () => {
  const ui = await newMockPage(base)
  try {
    const { page } = ui
    page.setDefaultTimeout(10000)
    const errors = []
    page.on('pageerror', error => errors.push(error))
    page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
    const ownerAlex = '00000000-0000-0000-0000-000000000002'
    const reviewerDiane = '00000000-0000-0000-0000-000000000007'
    const inputTask = '00000000-0000-0000-0000-000000000012'
    const inputDecision = '00000000-0000-0000-0000-000000000023'
    const inputHandoff = '00000000-0000-0000-0000-000000000008'
    const inputConstraint = '00000000-0000-0000-0000-000000000009'
    const context = {
      owner: { id: ownerAlex, name: 'Alex Chen', state: 'Active', kind: 'User' },
      nextActors: [{ id: reviewerDiane, name: 'Diane Review', state: 'Active', kind: 'User' }],
      neededBy: '2026-10-20', nextAction: 'review', terminal: false, actorState: 'Active', requiresReviewGate: true, nextTransitions: [],
      inputs: [
        { kind: 'Task', id: inputTask, key: 'P-MOCK-T002', title: 'Survey input', neededBy: null, nextActors: [], nextAction: 'start', primary: true },
        { kind: 'Decision', id: inputDecision, key: 'P-MOCK-D001', title: 'Scope decision', neededBy: null, nextActors: [], nextAction: 'decide', primary: false },
        { kind: 'Handoff', id: inputHandoff, key: 'P-MOCK-H001', title: 'Survey handoff', neededBy: null, nextActors: [], nextAction: 'accept', primary: false },
        { kind: 'WorkConstraint', id: inputConstraint, key: 'P-MOCK-CT001', title: 'Permit constraint', neededBy: null, nextActors: [], nextAction: 'remove', primary: false },
      ],
    }
    const listRows = [task, ...Array.from({ length: 60 }, (_, index) => taskRow({
      id: `00000000-0000-0000-0000-${String(index + 100).padStart(12, '0')}`,
      key: `P-MOCK-T${String(index + 3).padStart(3, '0')}`,
      name: `Scrollable task ${index + 1}`,
    }))]
    const handoff = handoffRow({ id: inputHandoff, key: 'P-MOCK-H001', title: 'Survey handoff', sourceKey: 'P-MOCK-D001', targetKey: 'P-MOCK-T002', senderName: 'Alex Chen', receiverName: 'Diane Review', targetTaskId: inputTask })
    const constraint = windowConstraint({ id: inputConstraint, key: 'P-MOCK-CT001', targetId: inputTask, description: 'Permit constraint' })
    await page.route(`**/api/v1/tasks/${inputTask}`, route => route.fulfill({ json: makeDetail({ task: taskRow({ ...task, id: inputTask, key: 'P-MOCK-T002', name: 'Survey input' }) }) }))
    await page.route(`**/api/v1/projects/${project.id}/tasks*`, route => route.fulfill({ json: makeTaskPage(listRows) }))
    await page.route('**/api/v1/projects/P-MOCK/tasks*', route => route.fulfill({ json: makeTaskPage(listRows) }))
    await page.route(`**/api/v1/projects/${project.id}/handoffs**`, async route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith('/options')) return route.fulfill({ json: handoffOptions() })
      if (path.endsWith(`/${inputHandoff}`)) return route.fulfill({ json: handoffDetail({ row: handoff }) })
      return route.fulfill({ json: handoffPage([handoff]) })
    })
    await page.route(`**/api/v1/projects/${project.id}/readiness/window**`, route => route.fulfill({ json: readinessWindow({ constraints: [constraint], constraintsTotal: 1 }) }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/constraints/${inputConstraint}**`, route => route.fulfill({ json: { targetType: 'Task', targetId: inputTask } }))
    await page.route(`**/api/v1/projects/${project.id}/weekly-commitments**`, route => route.fulfill({ json: commitmentList([]) }))
    Object.assign(me, { flags: { ...me.flags, screen_patterns: false } })
    Object.assign(taskDetail, makeDetail({ task: { ...task, assigneeId: ownerAlex, assigneeName: 'Alex Chen', reviewerId: reviewerDiane, reviewerName: 'Diane Review' }, context }))
    const url = `${base}/projects/${task.projectNumber}/tasks?status=Not%20Started&sort=dueDate:asc&panel=Task:${task.id}`
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: /^Change status/ }).waitFor()
    const contextSection = () => dialog.locator('section[aria-label="recordContext.title"], section[aria-label="Record context"]')
    assert.equal(await contextSection().count(), 0, 'flag-off hides stale context while task fields remain visible')

    // Reloaded TaskPanel sees the flag turn on; hook order remains stable across its loading/loaded render path.
    me.flags.screen_patterns = true
    await page.reload()
    const section = contextSection()
    await section.getByText('You', { exact: true }).waitFor()
    await section.getByText('Diane Review', { exact: true }).waitFor()
    assert.equal(await section.getByText('You', { exact: true }).count(), 1)
    assert.equal(await section.getByRole('button', { name: /^(Start|Review|Complete|Revise|Resolve input|Resume)$/ }).count(), 0, 'context grants no mutation command')
    assert.equal(await section.getByRole('button').count(), 2, 'Task and Decision inputs are navigable panel buttons')
    await section.getByRole('button', { name: /P-MOCK-T002/ }).click()
    await page.waitForFunction((id) => new URL(location.href).searchParams.get('panel') === `Task:${id}`, inputTask)
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    await dialog.getByRole('button', { name: /^Change status/ }).waitFor()
    await contextSection().getByRole('button', { name: /P-MOCK-D001/ }).click()
    await page.waitForFunction((id) => new URL(location.href).searchParams.get('panel') === `Decision:${id}`, inputDecision)
    await page.goto(url, { waitUntil: 'domcontentloaded' })
    await dialog.getByRole('button', { name: /^Change status/ }).waitFor()
    const originUrl = page.url()
    const originScrollTop = await page.locator('main#content').evaluate((el) => { el.scrollTop = 160; return el.scrollTop })
    assert.equal(originScrollTop, 160, `origin task list is a real scroll container (${await page.locator('main#content').evaluate(el => JSON.stringify({ scrollHeight: el.scrollHeight, clientHeight: el.clientHeight, children: el.children.length, text: el.textContent?.slice(0, 80) }))})`)
    await contextSection().getByRole('link', { name: /P-MOCK-H001/ }).click()
    await page.waitForURL(new RegExp(`/projects/${task.projectNumber}/handoffs\\?panel=Handoff:${inputHandoff}$`))
    await page.getByRole('dialog').getByRole('heading', { name: /Discipline handoffs/i }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click()
    await page.waitForURL(originUrl)
    await page.waitForFunction((expected) => document.querySelector('main#content')?.scrollTop === expected, originScrollTop)
    await contextSection().getByText('You', { exact: true }).waitFor()
    await contextSection().locator('a[href*="WorkConstraint:"]').click()
    await page.waitForURL(new RegExp(`/projects/${task.projectNumber}/readiness\\?panel=WorkConstraint:${inputConstraint}$`))
    await page.getByRole('dialog').getByRole('heading', { name: /Inspect readiness/i }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close' }).click()
    await page.waitForURL(originUrl)
    await page.waitForFunction((expected) => document.querySelector('main#content')?.scrollTop === expected, originScrollTop)

    // Missing reviewer stays unassigned and a null needed-by is explicit.
    taskDetail.context = { ...context, nextActors: [], actorState: 'Unassigned', neededBy: null }
    await page.reload()
    await section.locator('dt').filter({ hasText: /^Next actor$/ }).locator('..').getByText('Unassigned', { exact: true }).waitFor()
    await section.locator(':scope > div.mt-1').getByText(/No date/).waitFor()
    assert.equal(await section.getByText('Diane Review', { exact: true }).count(), 0)

    // Inactive, unknown and external actors remain labelled rather than substituted.
    taskDetail.context = { ...context, nextActors: [{ id: reviewerDiane, name: 'Diane Review', state: 'Inactive', kind: 'User' }], actorState: 'Inactive' }
    await page.reload()
    await section.getByText(/Diane Review \(Inactive\)/).waitFor()
    taskDetail.context = { ...context, nextActors: [{ id: reviewerDiane, name: 'Diane Review', state: 'Inactive', kind: 'ExternalParty' }, { id: ownerAlex, name: 'Vendor party', state: 'External', kind: 'ExternalParty' }, { id: null, name: null, state: 'Unknown', kind: 'ExternalParty' }], actorState: 'Unknown' }
    await page.reload()
    await section.getByText(/Diane Review \(Inactive · No account\)/).waitFor()
    await section.getByText(/Vendor party \(No account\)/).waitFor()
    await section.getByText(/Unknown/).waitFor()
    assert.equal(await section.getByText('You', { exact: true }).count(), 1, 'external actor with viewer id is not treated as self')

    // Terminal context removes the actor field and keeps the action explicit.
    taskDetail.context = { ...context, terminal: true, nextActors: [], nextAction: 'none' }
    await page.reload()
    await section.getByText('No next action', { exact: true }).waitFor()
    assert.equal(await section.getByText('Next actor', { exact: true }).count(), 0)

    assert.match(page.url(), /panel=Task:/)
    await dialog.getByRole('button', { name: 'Close' }).click()
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
    assert.doesNotMatch(page.url(), /panel=Task:/)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked reads')
    assert.deepEqual(ui.mock.apiErrors, [], 'no mocked API errors')
    assert.deepEqual(errors, [], errors.map(error => error.stack ?? error.message).join('\n'))
    console.log('PASS: TaskPanel context flag gate, actors, no-date, terminal state, stored inputs and close URL')
  } finally {
    await ui.browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
