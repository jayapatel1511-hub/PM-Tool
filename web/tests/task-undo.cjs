// S12 Undo for single changes (FR-SCR-20, 053-D9): with flag.work_screens on, a panel rename and a status change each offer Undo
// for 10 seconds; Undo sends the same command with the earlier value, against the version the change made (G-07), marked "Undo of".
// With the flag off no Undo is offered. Against the mocked platform API; TaskBulkUndoTests owns the server reversal rules.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project, me, task: baseTask } = require('./platform-fixtures.cjs')
const { taskRow, taskDetail, taskPermissions, taskTransition, taskEdited, taskTransitioned, taskPage } = require('./tasks.fixture.cjs')

const port = Number(process.env.TASK_UNDO_TEST_PORT ?? 7331)
const taskId = baseTask.id, civil = project.disciplines[0].id

async function run(flag) {
  me.flags.work_screens = flag
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base), { page } = ui
  const writes = []
  try {
    let task = taskRow({ id: taskId, projectId: project.id, key: 'T-001', name: 'Synthetic task', projectDisciplineId: civil, status: 'Not Started', rowVersion: 1 })
    const sent = request => ({ method: request.method(), path: new URL(request.url()).pathname, body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null })
    const bump = (patch = {}) => { task = { ...task, ...patch, rowVersion: task.rowVersion + 1 } }
    const waitWrite = async n => { for (let i = 0; i < 100 && writes.length < n; i++) await page.waitForTimeout(100); assert.ok(writes.length >= n, `write ${n} was sent`) }
    await page.route(`**/api/v1/projects/${project.id}/tasks**`, route => route.fulfill({ json: taskPage([task]) }))
    await page.route(`**/api/v1/tasks/${taskId}`, route => {
      const request = route.request()
      if (request.method() === 'GET') return route.fulfill({ json: taskDetail({ task, permissions: taskPermissions({ transitions: [taskTransition(task.status === 'Not Started' ? 'In Progress' : 'Not Started')] }) }) })
      writes.push(sent(request)); const { reason, ...body } = request.postDataJSON(); bump(body)
      return route.fulfill({ json: taskEdited(taskId, task.rowVersion) })
    })
    await page.route(`**/api/v1/tasks/${taskId}/transition`, route => {
      const request = route.request(); writes.push(sent(request)); bump({ status: request.postDataJSON().toStatus })
      return route.fulfill({ json: taskTransitioned(taskId, task.status, task.rowVersion) })
    })

    await page.goto(`${preview.base}/projects/${project.projectNumber}/tasks?panel=Task:${taskId}`, { waitUntil: 'domcontentloaded' })
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: 'Synthetic task', exact: true }).first().click()
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true })
    await name.fill('Renamed task'); await name.press('Enter')
    await waitWrite(1)
    const undo = page.locator('[data-sonner-toast]').getByRole('button', { name: 'Undo', exact: true })
    if (!flag) {
      await page.waitForTimeout(500)
      assert.equal(await undo.count(), 0, 'no Undo with the flag off')
      return { flag, writes: writes.length }
    }
    await undo.click(); await waitWrite(2)
    assert.deepEqual(writes[1], { method: 'PATCH', path: `/api/v1/tasks/${taskId}`, ifMatch: '"2"', body: { name: 'Synthetic task', reason: 'Undo of: Name on T-001' } })
    await page.locator('[data-sonner-toast]').getByText('Undone: Name', { exact: true }).waitFor()

    await dialog.getByRole('button', { name: /^Change status of T-001/ }).click()
    await page.getByRole('menuitem', { name: 'In Progress' }).click()
    await waitWrite(3)
    await undo.first().click(); await waitWrite(4)
    assert.deepEqual(writes[3], { method: 'POST', path: `/api/v1/tasks/${taskId}/transition`, ifMatch: null,
      body: { toStatus: 'Not Started', rowVersion: 4, reason: 'Undo of: → In Progress on T-001', comment: null, reviewerId: null } })
    assert.deepEqual(ui.mock.apiErrors.filter(error => !error.startsWith('GET ')), [], 'no write error (the panel activity reads are not mocked here)')
    return { flag, writes: writes.length }
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
}

;(async () => {
  const results = [await run(true), await run(false)]
  console.log(JSON.stringify({ result: 'PASS', suite: 'task-undo', results }))
})().catch(error => { console.error(error); process.exitCode = 1 })
