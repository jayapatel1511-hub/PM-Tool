// 040-S15-Dependencies: the task panel's links go through the typed client. The list and the candidates are read from their contract
// paths (the candidate search sends its term and direction, nothing empty), Add sends both link ends with the unused one null, the
// created answer's warnings are shown, Remove sends a bodiless DELETE, and the chain dialog lists both sides. Every write carries
// X-Hub-Source: UI. Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, task, taskDetail } = require('./platform-fixtures.cjs')
const { dependencyEdge, dependencyTask, taskDependencies } = require('./dependencies.fixture.cjs')

const port = Number(process.env.DEPENDENCIES_TEST_PORT ?? 7371)
const other = { id: '00000000-0000-0000-0000-000000000042', key: 'T-002', name: 'Second task' }
const loop = { id: '00000000-0000-0000-0000-000000000043', key: 'T-003', name: 'Third task' }
const edgeId = '00000000-0000-0000-0000-0000000000d2', addedId = '00000000-0000-0000-0000-0000000000d3'

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let links = [dependencyEdge({ dependencyId: edgeId, task: dependencyTask({ ...other, assigneeName: 'Pat Lee', dueDate: '2026-10-20' }), lagDays: 2, type: 'StartToStart' })]
    const row = (r) => ({ ...taskDetail.task, ...r })
    await page.route(`**/api/v1/tasks/${task.id}/dependencies`, async route => {
      const request = route.request()
      if (request.method() === 'POST') {
        writes.push({ method: 'POST', path: new URL(request.url()).pathname, body: request.postDataJSON(), source: request.headers()['x-hub-source'] })
        links = [...links, dependencyEdge({ dependencyId: addedId, task: dependencyTask({ ...loop }) })]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/dependencies/${addedId}` }, json: { id: addedId, warnings: ['Synthetic successor is already complete'] } })
      }
      reads.push(new URL(request.url()).pathname)
      return route.fulfill({ json: taskDependencies(links) })
    })
    await page.route(`**/api/v1/dependencies/${edgeId}`, route => {
      const request = route.request()
      writes.push({ method: request.method(), path: new URL(request.url()).pathname, body: request.postData(), source: request.headers()['x-hub-source'] })
      links = links.filter(x => x.dependencyId !== edgeId)
      return route.fulfill({ status: 204 })
    })
    await page.route(`**/api/v1/tasks/${task.id}/dependency-candidates**`, route => {
      reads.push(new URL(route.request().url()).pathname + new URL(route.request().url()).search)
      return route.fulfill({ json: [
        { ...other, status: 'Not Started', dueDate: null, disabled: true, reason: 'Already linked' },
        { ...loop, status: 'In Progress', dueDate: '2026-10-25', disabled: false, reason: null },
      ] })
    })
    await page.route(`**/api/v1/tasks/${task.id}/chain`, route => {
      reads.push(new URL(route.request().url()).pathname)
      return route.fulfill({ json: { task: taskDetail.task, depth: 10, predecessors: [{ level: 1, parentId: task.id, task: row({ ...other, id: other.id }) }], successors: [] } })
    })
    await page.route(`**/api/v1/items/Task/${other.id}/**`, route => route.fulfill({ json: {} }))

    const dialog = page.getByRole('dialog')
    await page.goto(`${preview.base}/my-work?panel=Task:${task.id}`, { waitUntil: 'domcontentloaded' })
    await dialog.getByRole('button', { name: /^T-002 / }).waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes(`/api/v1/tasks/${task.id}/dependencies`), 'the list is read from its contract path')
    assert.ok(await dialog.getByText('SS +2d').first().isVisible(), 'the type and lag of the link are shown')

    // Add: the search sends its direction, the dialog marks a taken task, and the save names both ends with the unused one null.
    await dialog.getByRole('button', { name: 'Add', exact: true }).first().click()
    const add = page.getByRole('dialog', { name: `What does ${task.key} depend on?` })
    await add.getByRole('option', { name: /T-003/ }).waitFor({ state: 'visible' })
    assert.ok(reads.some(x => x === `/api/v1/tasks/${task.id}/dependency-candidates?direction=predecessor`), `the first search sends only its direction: ${reads.filter(x => x.includes('candidates'))}`)
    assert.equal(await add.getByRole('option', { name: /T-002/ }).isDisabled(), true, 'a taken task cannot be chosen')
    await add.getByRole('searchbox').fill('Third')
    await page.waitForResponse(res => res.url().includes('dependency-candidates') && res.url().includes('q=Third'))
    await add.getByRole('option', { name: /T-003/ }).click()
    await add.getByLabel('Notes').fill('Needs the survey')
    await add.getByRole('button', { name: 'Add', exact: true }).click()
    await dialog.getByRole('button', { name: /^T-003 / }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/tasks/${task.id}/dependencies`, source: 'UI',
      body: { predecessorTaskId: loop.id, successorTaskId: null, note: 'Needs the survey', lagDays: 0, type: 'FinishToStart' } })
    await page.getByText('Synthetic successor is already complete').waitFor({ state: 'visible' })

    // Remove: no body, the same source header, and the list is read again without the link.
    await dialog.getByRole('button', { name: `Remove the dependency on ${other.key}` }).click()
    await dialog.getByRole('button', { name: /^T-002 / }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[1], { method: 'DELETE', path: `/api/v1/dependencies/${edgeId}`, body: null, source: 'UI' })

    // Chain: both sides come from the contract path.
    await dialog.getByRole('button', { name: 'Show chain' }).click()
    const chain = page.getByRole('dialog', { name: `Dependency chain of ${task.key}` })
    await chain.getByRole('button', { name: /^T-002 / }).waitFor({ state: 'visible' })
    assert.ok(reads.includes(`/api/v1/tasks/${task.id}/chain`))
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
