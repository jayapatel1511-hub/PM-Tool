// FR-SCR-01 / AC-SCR-01: legacy relation opens, normal Action duplicate and source restoration.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail, panelIds } = require('./platform-fixtures.cjs')
const { taskRow, taskPage } = require('./tasks.fixture.cjs')
const { deliverableRow, deliverableDetail } = require('./deliverables.fixture.cjs')
const { actionRow, actionDetail } = require('./meetings.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const storedLink = { id: '00000000-0000-0000-0000-000000000900', targetType: 'Task', targetId: task.id, relation: 'related', key: task.key, name: task.name, status: task.status, date: null, person: null }
const action = actionRow({ id: panelIds.action, projectId: project.id, projectNumber: project.projectNumber, key: 'A-001', meetingId: '00000000-0000-0000-0000-000000000031', text: 'Confirm the crossing with the drawing owner',
  relatedTaskId: task.id, taskKey: task.key })
const actionData = actionDetail({ action, task: { id: task.id, key: task.key, name: task.name, status: task.status }, links: [storedLink],
  permissions: { edit: { ok: false, reason: null }, transitions: [], convert: false, comment: true } })
const delivery = deliverableDetail({ deliverable: deliverableRow({ id: panelIds.deliverable, projectId: project.id, key: 'D-001', name: 'Crossing drawing package',
  tasks: [{ id: task.id, key: task.key, name: task.name, status: task.status, dueDate: null, deliverableId: panelIds.deliverable, assignee: 'Alex Chen' }] }),
  project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status }, description: 'Recorded drawing package and member task',
  permissions: { edit: { ok: false, reason: null }, comment: false } })
const dependencyTargetId = '00000000-0000-0000-0000-000000000902'
const dependencyTarget = deliverableDetail({ deliverable: deliverableRow({ id: dependencyTargetId, projectId: project.id, key: 'D-002', name: 'Dependency destination', tasks: [] }), project: delivery.project })
delivery.explicitPredecessors = [
  ...Array.from({ length: 25 }, (_, i) => ({ dependencyId: `00000000-0000-0000-0004-${String(i + 1).padStart(12, '0')}`, id: `00000000-0000-0000-0005-${String(i + 1).padStart(12, '0')}`,
    key: `D-${i + 100}`, name: `Scrollable dependency ${i + 1}`, type: 'FinishToStart', lagDays: 0, note: null, status: 'Not Started', dueDate: null, issuedDate: null, projectDisciplineId: project.disciplines[0].id, satisfied: false })),
  { dependencyId: '00000000-0000-0000-0000-000000000903', id: dependencyTargetId, key: 'D-002', name: 'Dependency destination', type: 'FinishToStart', lagDays: 0,
    note: null, status: 'Not Started', dueDate: null, issuedDate: null, projectDisciplineId: project.disciplines[0].id, satisfied: false },
]

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui, errors = []
  page.setDefaultTimeout(12000)
  page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (m.type() === 'error') errors.push(m.text()) })
  try {
    await page.setViewportSize({ width: 1440, height: 400 })
    const rows = Array.from({ length: 70 }, (_, i) => taskRow({ ...task, id: `00000000-0000-0000-0003-${String(i + 1).padStart(12, '0')}`, key: `T-${i + 1}`, name: `Scrollable crossing task ${i + 1}` }))
    await page.route(`**/api/v1/projects/${project.id}/tasks**`, r => r.fulfill({ json: taskPage(rows) }))
    await page.route(`**/api/v1/projects/${project.projectNumber}/tasks**`, r => r.fulfill({ json: taskPage(rows) }))
    await page.route(`**/api/v1/actions/${action.id}`, r => r.fulfill({ json: actionData }))
    await page.route(`**/api/v1/deliverables/${panelIds.deliverable}`, r => r.fulfill({ json: delivery }))
    await page.route(`**/api/v1/deliverables/${dependencyTargetId}`, r => r.fulfill({ json: dependencyTarget }))
    await page.route(`**/api/v1/tasks/${task.id}`, r => r.fulfill({ json: taskDetail }))
    const urlFor = kind => `${base}/projects/${project.projectNumber}/tasks?discipline=${project.disciplines[0].id}&q=crossing&sort=dueDate:asc&panel=${kind}:${kind === 'Action' ? action.id : panelIds.deliverable}`
    const dialog = () => page.getByRole('dialog')
    const taskAnchor = () => dialog().getByRole('link', { name: `${task.key} ${task.name}`, exact: true })
    me.flags = { ...me.flags, screen_patterns: false }
    const offDelivery = `${urlFor('Deliverable')}&deliverableTab=${encodeURIComponent(`${panelIds.deliverable}:deps`)}`
    await page.goto(offDelivery); await dialog().getByRole('tab', { name: /^Tasks/ }).waitFor()
    assert.equal(await dialog().getByRole('tab', { name: /^Tasks/ }).getAttribute('aria-selected'), 'true', 'disabled pattern ignores the new tab URL state')
    await dialog().getByRole('tab', { name: 'Dependencies', exact: true }).click()
    assert.equal(page.url(), offDelivery, 'disabled pattern keeps local tab state and does not write a URL')
    await page.reload(); await dialog().getByRole('tab', { name: /^Tasks/ }).waitFor()
    assert.equal(await dialog().getByRole('tab', { name: /^Tasks/ }).getAttribute('aria-selected'), 'true')
    await page.goto(urlFor('Action')); await dialog().getByRole('heading', { name: action.text, exact: true }).waitFor()
    assert.equal(await taskAnchor().count(), 1); assert.equal(await dialog().getByRole('button', { name: `${task.key} ${task.name}`, exact: true }).count(), 1, 'flag-off retains the legacy two representations')

    me.flags.screen_patterns = true
    await page.goto(urlFor('Action')); await taskAnchor().waitFor()
    assert.equal(await taskAnchor().count(), 1); assert.equal(await dialog().getByRole('button', { name: `${task.key} ${task.name}`, exact: true }).count(), 0, 'normal Action duplicate appears once')
    await taskAnchor().locator('..').getByText('Related', { exact: true }).waitFor()
    assert.equal(await taskAnchor().getAttribute('href'), `/projects/${project.projectNumber}/tasks?panel=Task%3A${task.id}`, 'stored relation keeps its ordinary exact-record href')

    // Cross-route anchor restores source URL and both real scroll containers.
    const origin = page.url()
    await page.locator('main#content').evaluate(e => { e.scrollTop = 160 })
    await taskAnchor().scrollIntoViewIfNeeded()
    const before = await page.evaluate(() => ({ main: document.querySelector('main#content').scrollTop, panel: document.querySelector('[data-slot="sheet-content"]').scrollTop }))
    assert.ok(before.main > 0); assert.ok(before.panel > 0, 'source Action panel genuinely overflows')
    await taskAnchor().click(); await page.waitForURL(u => u.searchParams.get('panel') === `Task:${task.id}`)
    await dialog().getByRole('heading', { name: task.name, exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    await dialog().getByRole('heading', { name: action.text, exact: true }).waitFor()
    await page.waitForFunction(p => document.querySelector('main#content')?.scrollTop === p.main && document.querySelector('[data-slot="sheet-content"]')?.scrollTop === p.panel, before)
    assert.equal(new URL(page.url()).searchParams.get('q'), 'crossing'); assert.equal(new URL(page.url()).searchParams.get('sort'), 'dueDate:asc')

    // Existing member-task button restores its enclosing Deliverable panel too.
    await page.goto(urlFor('Deliverable')); await dialog().getByRole('heading', { name: 'Crossing drawing package', exact: true }).waitFor()
    const member = dialog().getByRole('button', { name: task.name, exact: true })
    await member.scrollIntoViewIfNeeded(); await page.locator('main#content').evaluate(e => { e.scrollTop = 160 })
    const deliveryOrigin = page.url(), deliveryScroll = await page.evaluate(() => ({ main: document.querySelector('main#content').scrollTop, panel: document.querySelector('[data-slot="sheet-content"]').scrollTop }))
    assert.ok(deliveryScroll.panel > 0)
    await member.click(); await page.waitForURL(u => u.searchParams.get('panel') === `Task:${task.id}`)
    await dialog().getByRole('heading', { name: task.name, exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(deliveryOrigin)
    await dialog().getByRole('heading', { name: 'Crossing drawing package', exact: true }).waitFor()
    await page.waitForFunction(p => document.querySelector('main#content')?.scrollTop === p.main && document.querySelector('[data-slot="sheet-content"]')?.scrollTop === p.panel, deliveryScroll)

    // The source Dependencies tab must survive the keyed panel remount, alongside
    // its URL filters and genuinely nonzero main/panel scroll. The target must not inherit it.
    await dialog().getByRole('tab', { name: 'Dependencies', exact: true }).click()
    await page.waitForURL(u => u.searchParams.get('deliverableTab') === `${panelIds.deliverable}:deps`)
    await page.reload(); await dialog().getByRole('tab', { name: 'Dependencies', exact: true }).waitFor()
    assert.equal(await dialog().getByRole('tab', { name: 'Dependencies', exact: true }).getAttribute('aria-selected'), 'true', 'copied/reloaded tab URL is reconstructible')
    const dependency = dialog().getByRole('button', { name: 'D-002 Dependency destination', exact: true })
    await dependency.scrollIntoViewIfNeeded(); await page.locator('main#content').evaluate(e => { e.scrollTop = 160 })
    const dependencyOrigin = page.url(), dependencyScroll = await page.evaluate(() => ({ main: document.querySelector('main#content').scrollTop, panel: document.querySelector('[data-slot="sheet-content"]').scrollTop }))
    assert.ok(dependencyScroll.main > 0); assert.ok(dependencyScroll.panel > 0, 'Dependencies source has real overflowing scroll')
    await dependency.click(); await page.waitForURL(u => u.searchParams.get('panel') === `Deliverable:${dependencyTargetId}`)
    await dialog().getByRole('heading', { name: 'Dependency destination', exact: true }).waitFor()
    assert.equal(await dialog().getByRole('tab', { name: /^Tasks/ }).getAttribute('aria-selected'), 'true', 'source tab state is scoped to its source record')
    await dialog().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(dependencyOrigin)
    await dialog().getByRole('heading', { name: 'Crossing drawing package', exact: true }).waitFor()
    assert.equal(await dialog().getByRole('tab', { name: 'Dependencies', exact: true }).getAttribute('aria-selected'), 'true', 'return restores Dependencies, not the default Tasks tab')
    await dependency.waitFor()
    await page.waitForFunction(p => document.querySelector('main#content')?.scrollTop === p.main && document.querySelector('[data-slot="sheet-content"]')?.scrollTop === p.panel, dependencyScroll)
    assert.equal(new URL(page.url()).searchParams.get('q'), 'crossing'); assert.equal(new URL(page.url()).searchParams.get('sort'), 'dueDate:asc')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: Action relation dedup/provenance and nested return URL/tab/main/panel scroll, including Dependencies → Deliverable → close')
  } catch (e) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw e } finally { await ui.browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
