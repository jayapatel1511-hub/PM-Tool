// 040-S15-Tasks: the task list, panel and commands go through the typed client. The list is read from its contract path with the filters under their own names; a new
// task sends every contract field (the unused ones null); the panel reads the task from its contract path, a status change sends the contract body, a rename and a
// due-date change carry the row version in If-Match, blocking and clearing the block, watching and unwatching, and deleting with its preview and its undo each go to
// their contract path. Every write carries X-Hub-Source: UI. Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project, me, task: baseTask } = require('./platform-fixtures.cjs')
const { taskRow, taskPage, taskDetail, taskPermissions, taskTransition, taskCreated, taskEdited, taskTransitioned, taskVersion, taskDeleted, taskDeletePreview } = require('./tasks.fixture.cjs')

const port = Number(process.env.TASKS_TEST_PORT ?? 7329)
const taskId = baseTask.id, newId = '00000000-0000-0000-0000-000000000901'
const civil = project.disciplines[0].id

// The caller may create tasks in Civil.
Object.assign(project, { disciplines: [{ ...project.disciplines[0], code: 'CIV', colour: '#2563eb', isActive: true, rowVersion: 1 }] })
Object.assign(project.permissions, { createTaskIn: [civil], createDeliverableIn: [] })

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let task = taskRow({ id: taskId, projectId: project.id, key: 'T-001', name: 'Synthetic task', projectDisciplineId: civil, assigneeId: me.id, assigneeName: me.displayName, rowVersion: 1 })
    let watchers = []
    let deleted = false
    const where = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams) })
    const sent = (request) => ({ ...where(request), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const detail = () => taskDetail({ task, description: 'Synthetic detail', watchers,
      permissions: taskPermissions({ transitions: [taskTransition('In Progress'), taskTransition('Cancelled', { needsReason: true })] }) })
    const bump = (patch = {}) => { task = { ...task, ...patch, rowVersion: task.rowVersion + 1 } }
    const waitWrite = async (n) => { for (let i = 0; i < 100 && writes.length < n; i++) await page.waitForTimeout(100); assert.ok(writes.length >= n, `write ${n} was sent`) }

    await page.route(`**/api/v1/projects/${project.id}/tasks**`, route => {
      const request = route.request()
      if (request.method() === 'POST') { writes.push(sent(request)); return route.fulfill({ status: 201, headers: { location: `/api/v1/tasks/${newId}` }, json: taskCreated(newId, 'T-002', ['Synthetic due date is after its deliverable']) }) }
      reads.push(where(request))
      return route.fulfill({ json: taskPage(deleted ? [] : [task]) })
    })
    await page.route(`**/api/v1/tasks/${taskId}`, route => {
      const request = route.request()
      if (request.method() === 'GET') { reads.push(where(request)); return route.fulfill({ json: detail() }) }
      writes.push(sent(request))
      if (request.method() === 'DELETE') { deleted = true; return route.fulfill({ json: taskDeleted(0) }) }
      const body = request.postDataJSON()
      bump(body)
      return route.fulfill({ json: taskEdited(taskId, task.rowVersion) })
    })
    await page.route(`**/api/v1/tasks/${taskId}/*`, route => {
      const request = route.request(), tail = new URL(request.url()).pathname.split('/').at(-1)
      if (request.method() === 'GET' && tail === 'delete-preview') { reads.push(where(request)); return route.fulfill({ json: taskDeletePreview({ suggestCancel: true }) }) }
      if (request.method() === 'GET') return route.fallback()
      writes.push(sent(request))
      if (tail === 'transition') { bump({ status: 'In Progress', lane: 'In Progress' }); return route.fulfill({ json: taskTransitioned(taskId, 'In Progress', task.rowVersion) }) }
      if (tail === 'block') { const body = request.postDataJSON(); bump({ manualBlockType: body.type, manualBlockReason: body.reason, manualBlockSetAt: '2026-10-03T12:00:00Z' }); return route.fulfill({ json: taskVersion(taskId, task.rowVersion) }) }
      if (tail === 'unblock') { bump({ manualBlockType: null, manualBlockReason: null, manualBlockSetAt: null }); return route.fulfill({ json: taskVersion(taskId, task.rowVersion) }) }
      if (tail === 'restore') { deleted = false; return route.fulfill({ json: taskVersion(taskId, task.rowVersion) }) }
      return route.fulfill({ status: 404, json: { title: 'Not found', status: 404 } })
    })
    await page.route(`**/api/v1/items/Task/${taskId}/watchers**`, route => {
      const request = route.request()
      writes.push(sent(request))
      watchers = request.method() === 'POST' ? [{ id: me.id, displayName: me.displayName, source: 'Manual' }] : []
      return route.fulfill({ status: 204, body: '' })
    })

    // The list: the contract path, the page size asked for, and no filter when none is set; a filtered link sends its filters under their own names.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/tasks`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'T-001' }).first().waitFor({ state: 'visible', timeout: 20000 })
    const list = () => reads.filter(r => r.path === `/api/v1/projects/${project.id}/tasks`).at(-1)
    assert.deepEqual(list().query, { pageSize: '200', page: '1' }, 'an unfiltered list sends only its paging')
    await page.goto(`${preview.base}/projects/${project.projectNumber}/tasks?status=Not Started,In Progress&priority=High&q=synthetic&overdue=true`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'T-001' }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(list().query, { status: 'Not Started,In Progress', priority: 'High', q: 'synthetic', overdue: 'true', pageSize: '200', page: '1' })

    // New task: every contract field is sent, the ones not filled null; the answer's warning is shown and the dialog closes.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/tasks`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'New task' }).first().click()
    const form = page.getByRole('dialog')
    await form.locator('#t-name').fill('Synthetic new task')
    await form.getByRole('button', { name: 'Create task' }).click()
    await waitWrite(1)
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/projects/${project.id}/tasks`, query: {}, source: 'UI', ifMatch: null, body: {
      name: 'Synthetic new task', description: null, projectDisciplineId: civil, deliverableId: null, milestoneId: null, assigneeId: null, reviewerId: null, requiresReview: false,
      priority: 'Medium', startDate: null, dueDate: null, estimatedHours: null, dependsOn: null } })
    await page.getByText('Synthetic due date is after its deliverable').first().waitFor({ state: 'visible' })

    // The panel reads the task from its contract path; Start sends the contract body with the version in the body and every other field null.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/tasks?panel=Task:${taskId}`, { waitUntil: 'domcontentloaded' })
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: /^Change status of T-001/ }).waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.some(r => r.path === `/api/v1/tasks/${taskId}`), 'the task is read from its contract path')
    await dialog.getByRole('button', { name: /^Change status of T-001/ }).click()
    await page.getByRole('menuitem', { name: 'In Progress' }).click()
    await waitWrite(2)
    assert.deepEqual(writes[1], { method: 'POST', path: `/api/v1/tasks/${taskId}/transition`, query: {}, source: 'UI', ifMatch: null, body: { toStatus: 'In Progress', rowVersion: 1, reason: null, comment: null, reviewerId: null } })

    // A rename carries the version just saved in If-Match.
    await dialog.getByRole('button', { name: 'Synthetic task', exact: true }).first().click()
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true })
    await name.fill('Renamed task')
    await name.press('Enter')
    await waitWrite(3)
    assert.deepEqual(writes[2], { method: 'PATCH', path: `/api/v1/tasks/${taskId}`, query: {}, source: 'UI', ifMatch: '"2"', body: { name: 'Renamed task' } })

    // Block, then clear the block: the type, the reason and the version in the body, then the version in If-Match.
    await dialog.getByRole('button', { name: 'Set block' }).click()
    const block = page.getByRole('dialog', { name: /^Block T-001/ })
    await block.locator('#blk-reason').fill('Survey data is late')
    await block.getByRole('button', { name: 'Set block' }).click()
    await waitWrite(4)
    assert.deepEqual(writes[3], { method: 'POST', path: `/api/v1/tasks/${taskId}/block`, query: {}, source: 'UI', ifMatch: null, body: { type: 'Client', reason: 'Survey data is late', rowVersion: 3 } })
    await dialog.getByRole('button', { name: 'Clear block' }).click()
    await waitWrite(5)
    assert.deepEqual(writes[4], { method: 'POST', path: `/api/v1/tasks/${taskId}/unblock`, query: {}, source: 'UI', ifMatch: '"4"', body: {} })

    // Watch and stop watching: the person in the body, then in the path; no content comes back either time.
    await dialog.getByRole('button', { name: 'Watch', exact: true }).click()
    await waitWrite(6)
    assert.deepEqual(writes[5], { method: 'POST', path: `/api/v1/items/Task/${taskId}/watchers`, query: {}, source: 'UI', ifMatch: null, body: { userId: me.id } })
    await dialog.getByRole('button', { name: 'Stop watching' }).click()
    await waitWrite(7)
    assert.deepEqual(writes[6], { method: 'DELETE', path: `/api/v1/items/Task/${taskId}/watchers/${me.id}`, query: {}, source: 'UI', ifMatch: null, body: null })

    // Delete reads its preview from the contract path, deletes by id and offers the undo that restores.
    await dialog.getByRole('button', { name: 'Delete task' }).click()
    const confirm = page.getByRole('dialog', { name: /^Delete T-001/ })
    await confirm.getByText('Cancel it instead').or(confirm.getByRole('button', { name: /Cancel/ })).first().waitFor({ state: 'visible' })
    assert.ok(reads.some(r => r.path === `/api/v1/tasks/${taskId}/delete-preview`), 'the preview is read from its contract path')
    await confirm.getByRole('button', { name: 'Delete', exact: true }).click()
    await waitWrite(8)
    assert.deepEqual(writes[7], { method: 'DELETE', path: `/api/v1/tasks/${taskId}`, query: {}, source: 'UI', ifMatch: null, body: null })
    await page.getByRole('button', { name: 'Restore', exact: true }).click()
    await waitWrite(9)
    assert.deepEqual(writes[8], { method: 'POST', path: `/api/v1/tasks/${taskId}/restore`, query: {}, source: 'UI', ifMatch: null, body: {} })
    assert.equal(writes.length, 9)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
