// FR-SCR-01: legacy explicit work relationships retain their exact source and scroll.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail, panelIds, links, comments } = require('./platform-fixtures.cjs')
const { taskRow, taskPage, taskDetail: taskDetailOf } = require('./tasks.fixture.cjs')
const { dependencyTask, dependencyEdge, taskDependencies } = require('./dependencies.fixture.cjs')
const { handoffRow, handoffPage, handoffOptions, handoffDetail, handoffHistoryRow } = require('./handoffs.fixture.cjs')
const { coordinationOptions, coordinationTask } = require('./reviews.fixture.cjs')
const { readinessWindow, windowConstraint, readinessAssessment, readinessDetail, readinessConstraintRow, linkedRecord } = require('./readiness.fixture.cjs')
const { commitmentList } = require('./weekly-commitments.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local preview')
const uid = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`

;(async () => {
  const ui = await newMockPage(base)
  const { page, mock } = ui
  const errors = []
  page.on('pageerror', error => errors.push(error))
  page.on('console', message => { if (message.type() === 'error') errors.push(new Error(message.text())) })
  try {
    page.setDefaultTimeout(12000)
    const rows = [task, ...Array.from({ length: 65 }, (_, i) => taskRow({ ...task, id: uid(100 + i), key: `P-MOCK-T${i + 2}`, name: `Scrollable work ${i + 1}` }))]
    await page.route(`**/api/v1/projects/${project.id}/tasks*`, route => route.fulfill({ json: taskPage(rows) }))
    await page.route('**/api/v1/projects/P-MOCK/tasks*', route => route.fulfill({ json: taskPage(rows) }))
    taskDetail.relatedDeliverables = [{ id: panelIds.deliverable, key: 'D-001', name: 'Synthetic deliverable', status: 'In Review' }]
    const predecessor = taskRow({ ...task, id: uid(65), key: 'T-PRE', name: 'Related predecessor' })
    await page.route(`**/api/v1/tasks/${task.id}/dependencies`, route => route.fulfill({ json: taskDependencies([dependencyEdge({ dependencyId: uid(66), task: dependencyTask(predecessor) })]) }))
    await page.route(`**/api/v1/tasks/${predecessor.id}`, route => route.fulfill({ json: taskDetailOf({ task: predecessor }) }))
    await page.route(`**/api/v1/tasks/${predecessor.id}/dependencies`, route => route.fulfill({ json: taskDependencies() }))
    await page.route(`**/api/v1/items/Task/${predecessor.id}/links`, route => route.fulfill({ json: links }))
    await page.route(`**/api/v1/items/Task/${predecessor.id}/comments`, route => route.fulfill({ json: comments }))
    const handoffId = uid(61)
    const handoff = handoffRow({ id: handoffId, key: 'H-001', title: 'Return to this handoff', sourceDeliverableId: panelIds.deliverable, sourceKey: 'D-001', targetTaskId: task.id, targetKey: task.key })
    await page.route(`**/api/v1/projects/${project.id}/handoffs**`, route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith('/options')) return route.fulfill({ json: handoffOptions() })
      if (path.endsWith(`/${handoffId}`)) return route.fulfill({ json: handoffDetail({ row: handoff, history: Array.from({ length: 25 }, (_, i) => handoffHistoryRow({ id: uid(300 + i), fromStatus: 'Draft', toStatus: 'Submitted', reason: `Recorded history ${i + 1}` })) }) })
      return route.fulfill({ json: handoffPage(Array.from({ length: 60 }, (_, i) => handoffRow({ ...handoff, id: i ? uid(500 + i) : handoffId, key: `H-${String(i + 1).padStart(3, '0')}` }))) })
    })
    const constraints = Array.from({ length: 25 }, (_, i) => readinessConstraintRow({ id: uid(700 + i), targetId: task.id, key: `CT-${i + 1}`, description: `Constraint ${i + 1}`, linkedType: 'Issue', linkedId: panelIds.issue,
      linked: linkedRecord({ type: 'Issue', id: panelIds.issue, key: 'I-001', title: 'Synthetic issue', status: 'Open' }) }))
    const constraint = constraints[20]
    const options = coordinationOptions({ actorId: me.id, canWrite: false, tasks: [coordinationTask({ id: task.id, key: task.key, name: task.name, projectDisciplineId: project.disciplines[0].id })] })
    await page.route(`**/api/v1/projects/${project.id}/changes/options`, route => route.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/options`, route => route.fulfill({ json: options }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/window*`, route => route.fulfill({ json: readinessWindow({ constraints: constraints.map(c => windowConstraint(c)), constraintsTotal: constraints.length }) }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/constraints/${constraint.id}*`, route => route.fulfill({ json: { ...constraint, title: constraint.description } }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/Task/${task.id}`, route => route.fulfill({ json: readinessDetail({ assessment: readinessAssessment({ id: uid(62), targetId: task.id }) }) }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/Task/${task.id}/constraints`, route => route.fulfill({ json: constraints }))
    await page.route(`**/api/v1/projects/${project.id}/readiness/Task/${task.id}/submission-prerequisites`, route => route.fulfill({ json: [] }))
    await page.route(`**/api/v1/projects/${project.id}/design-basis*`, route => route.fulfill({ json: { items: [], page: 1, pageSize: 200, totalCount: 0 } }))
    await page.route(`**/api/v1/projects/${project.id}/weekly-commitments**`, route => route.fulfill({ json: commitmentList([]) }))
    await page.route(`**/api/v1/projects/${project.id}/issues*`, route => route.fulfill({ json: [] }))

    const dialog = () => page.getByRole('dialog').last()
    const returnFrom = async (link, destination, { mainScroll = true } = {}) => {
      const origin = page.url()
      await link.scrollIntoViewIfNeeded()
      await link.evaluate((el, scrollMain) => {
        if (scrollMain) document.getElementById('content').scrollTop = 120
        // Capture actual positions at the click, after browser scrolling, not a requested offset.
        el.addEventListener('click', () => {
          const main = document.getElementById('content')
          const surface = el.closest('[data-slot="dialog-body"], [data-slot="dialog-content"], [data-slot="sheet-content"]')
          window.__workOriginScroll = { main: main.scrollTop, panel: surface?.scrollTop ?? 0, slot: surface?.dataset.slot }
        }, { once: true, capture: true })
      }, mainScroll)
      await link.click()
      await page.waitForFunction(({ kind, id }) => new URL(location.href).searchParams.get('panel') === `${kind}:${id}`, destination)
      await dialog().getByRole('heading', { name: destination.title, exact: true }).waitFor()
      const scroll = await page.evaluate(() => window.__workOriginScroll)
      if (mainScroll) assert.ok(scroll.main > 0, 'source main list really scrolls')
      await dialog().getByRole('button', { name: 'Close', exact: true }).click()
      await page.waitForURL(origin)
      await page.waitForFunction(expected => {
        const main = document.getElementById('content')
        const surfaces = Array.from(document.querySelectorAll(`[data-slot="${expected.slot}"]`))
        const surface = surfaces.reverse().find(el => el.getBoundingClientRect().height > 0 && !el.closest('[data-state="closed"], [aria-hidden="true"]'))
        return main?.scrollTop === expected.main && surface?.scrollTop === expected.panel
      }, scroll)
      return scroll
    }

    const taskUrl = `${base}/projects/${project.projectNumber}/tasks?status=Not%20Started&sort=dueDate:asc&panel=Task:${task.id}`
    me.flags.screen_patterns = false
    await page.goto(taskUrl, { waitUntil: 'domcontentloaded' })
    await dialog().getByRole('button', { name: 'D-001 Synthetic deliverable', exact: true }).click()
    await dialog().getByRole('heading', { name: 'Synthetic deliverable', exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
    assert.equal(new URL(page.url()).searchParams.has('panel'), false, 'disabled behavior still closes to the list')
    assert.equal(new URL(page.url()).searchParams.get('sort'), 'dueDate:asc')
    await page.goto(`${base}/projects/${project.projectNumber}/handoffs?status=Draft&panel=Handoff:${handoffId}`, { waitUntil: 'domcontentloaded' })
    await dialog().getByRole('link', { name: task.key, exact: true }).click()
    await dialog().getByRole('heading', { name: task.name, exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
    assert.equal(new URL(page.url()).pathname, `/projects/${project.projectNumber}/tasks`, 'disabled cross-route close retains existing destination-list behavior')
    const genericReadinessUrl = `${base}/projects/${project.projectNumber}/readiness?from=2026-11-01&to=2026-11-14&constraintsPage=1&sort=neededBy%3Aasc`
    await page.goto(genericReadinessUrl, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Inspect readiness', exact: true }).click()
    await dialog().getByRole('combobox', { name: 'Linked work' }).selectOption(`Task:${task.id}`)
    await dialog().getByText(constraint.description, { exact: true }).waitFor()
    assert.equal(page.url(), genericReadinessUrl, 'disabled generic inspector keeps its existing local-only state')
    await dialog().getByRole('button', { name: 'Close', exact: true }).click()
    assert.equal(page.url(), genericReadinessUrl)

    me.flags.screen_patterns = true
    await page.goto(taskUrl, { waitUntil: 'domcontentloaded' })
    await returnFrom(dialog().getByRole('button', { name: 'D-001 Synthetic deliverable', exact: true }), { kind: 'Deliverable', id: panelIds.deliverable, title: 'Synthetic deliverable' })
    await dialog().getByRole('heading', { name: task.name, exact: true }).waitFor()
    const dependencyScroll = await returnFrom(dialog().getByRole('button', { name: /T-PRE Related predecessor/ }), { kind: 'Task', id: predecessor.id, title: predecessor.name })
    assert.ok(dependencyScroll.panel > 0, 'stored dependency uses a real nonzero task-panel scroll')

    const handoffUrl = `${base}/projects/${project.projectNumber}/handoffs?status=Draft&sort=neededBy:asc&direction=incoming&panel=Handoff:${handoffId}`
    for (const [key, target] of [['D-001', { kind: 'Deliverable', id: panelIds.deliverable, title: 'Synthetic deliverable' }], [task.key, { kind: 'Task', id: task.id, title: task.name }]]) {
      await page.goto(handoffUrl, { waitUntil: 'domcontentloaded' })
      const link = dialog().getByRole('link', { name: key, exact: true })
      await link.waitFor()
      const href = await link.getAttribute('href')
      const current = page.url()
      // A modified click must stay browser-native; block its default in a test listener to avoid opening a test tab.
      const prevented = await link.evaluate(el => {
        let preventedByApp
        // React handles the event at its root before it reaches window. Observe the
        // final app decision there, then suppress the browser's new-tab default.
        window.addEventListener('click', event => { preventedByApp = event.defaultPrevented; event.preventDefault() }, { once: true })
        el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ctrlKey: true }))
        return preventedByApp
      })
      assert.equal(prevented, false, 'modified click is not intercepted by the app')
      assert.equal(page.url(), current)
      assert.ok(href.includes(`${target.kind}:${target.id}`), 'href retains exact stable entity route')
      await returnFrom(link, target)
      await dialog().getByRole('heading', { name: /H-001 · Return to this handoff/ }).waitFor()
    }

    const readinessUrl = `${base}/projects/${project.projectNumber}/readiness?from=2026-11-01&to=2026-11-14&constraintsPage=1&panel=WorkConstraint:${constraint.id}`
    await page.goto(readinessUrl, { waitUntil: 'domcontentloaded' })
    const focused = dialog().locator('li.ring-2')
    const scroll = await returnFrom(focused.getByRole('link', { name: /Issue I-001 · Synthetic issue/ }), { kind: 'Issue', id: panelIds.issue, title: 'Synthetic issue' }, { mainScroll: false })
    assert.ok(scroll.panel > 0, 'late constraint uses a real nonzero inspector scroll')
    await dialog().getByRole('heading', { name: /Inspect readiness/ }).waitFor()
    await focused.getByText(constraint.key, { exact: true }).waitFor()

    // Use the normal button and choose work, not a preconstructed WorkConstraint URL.
    // Both the inspector and its selected work must survive the linked-record round trip.
    await page.goto(genericReadinessUrl, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Inspect readiness', exact: true }).click()
    await page.waitForFunction(() => new URL(location.href).searchParams.get('inspect') === '1')
    await dialog().getByRole('combobox', { name: 'Linked work' }).selectOption(`Task:${task.id}`)
    await page.waitForFunction(id => new URL(location.href).searchParams.get('inspectWork') === `Task:${id}`, task.id)
    const genericConstraint = dialog().getByText(constraint.description, { exact: true }).locator('..')
    const genericScroll = await returnFrom(genericConstraint.getByRole('link', { name: /Issue I-001 · Synthetic issue/ }), { kind: 'Issue', id: panelIds.issue, title: 'Synthetic issue' })
    assert.ok(genericScroll.panel > 0, 'normal inspector really scrolled to its late constraint')
    await dialog().getByRole('heading', { name: 'Inspect readiness', exact: true }).waitFor()
    assert.equal(await dialog().getByRole('combobox', { name: 'Linked work' }).inputValue(), `Task:${task.id}`, 'normal inspector restores selected work')
    assert.equal(new URL(page.url()).searchParams.has('panel'), false, 'generic inspector does not impersonate an entity panel')
    await page.reload({ waitUntil: 'domcontentloaded' })
    await dialog().getByText(constraint.description, { exact: true }).waitFor()
    assert.equal(await dialog().getByRole('combobox', { name: 'Linked work' }).inputValue(), `Task:${task.id}`, 'selected inspector is reconstructible after reload too')
    await dialog().getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(genericReadinessUrl)
    assert.equal(new URL(page.url()).searchParams.has('inspectWork'), false, 'close removes only inspector state, retaining list filters/sort')

    assert.deepEqual(mock.unknownGets, [], `unmocked GETs: ${mock.unknownGets.join('; ')}`)
    assert.deepEqual(mock.apiErrors, [])
    assert.deepEqual(errors, [], errors.map(e => e.stack ?? e.message).join('\n'))
    console.log('PASS: work/delivery related-record return paths')
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
