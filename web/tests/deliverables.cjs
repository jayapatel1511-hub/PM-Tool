// 040-S15-Deliverables: the register, the panel and their writes go through the typed client. The register is read from its contract
// path (the filter's 'false' text is kept, tasks are asked for), Create sends every contract field with the unused ones null and shows
// the answer's warnings, a panel edit carries If-Match and shows its warnings, a status change and an issue send their contract
// bodies (the issue asks again after the open-tasks conflict), and a bulk change sends ids, operation and params. Every write carries
// X-Hub-Source: UI. Against the mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me, project, panelIds } = require('./platform-fixtures.cjs')
const { deliverableRow, deliverableState, deliverableList, deliverableDetail, deliverableCreated } = require('./deliverables.fixture.cjs')
const { referenceData, referenceDeliverableType } = require('./me.fixture.cjs')

const port = Number(process.env.DELIVERABLES_TEST_PORT ?? 7387)
const civil = project.disciplines[0]
const typeId = '00000000-0000-0000-0000-000000000030', createdId = '00000000-0000-0000-0000-0000000000e1', secondId = '00000000-0000-0000-0000-0000000000e2'
const first = panelIds.deliverable
const lead = { ...civil, code: 'CIV', colour: '#467762', isActive: true, leadUserId: me.id, leadName: 'Alex Chen' }
const live = { ...project, disciplines: [lead], permissions: { ...project.permissions, createDeliverableIn: [civil.id], createTaskIn: [civil.id], manageMilestones: true } }
const source = (request) => request.headers()['x-hub-source']
const where = (request) => new URL(request.url()).pathname + new URL(request.url()).search

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let issueRefused = false
    const row = (r) => deliverableRow({ projectId: project.id, projectDisciplineId: civil.id, ownerId: me.id, ownerName: 'Alex Chen', ...r })
    const rows = [
      row({ id: first, key: 'D-001', name: 'Synthetic deliverable', status: 'In Progress', dueDate: '2026-10-20', rowVersion: 3, state: deliverableState({ taskTotal: 2, taskComplete: 1, taskOpen: 1 }) }),
      row({ id: secondId, key: 'D-002', name: 'Second deliverable', status: 'Not Started', dueDate: '2026-10-25' }),
    ]
    const detail = () => deliverableDetail({ deliverable: rows[0], description: 'Synthetic detail', project: { id: project.id, projectNumber: project.projectNumber, name: project.name, status: project.status },
      permissions: { edit: { ok: true, reason: null }, issue: true, transitions: [{ to: 'In Review', allowed: true, needsReason: false }], comment: true } })

    await page.route(`**/api/v1/projects/${project.projectNumber}`, route => route.fulfill({ json: live }))
    await page.route(`**/api/v1/projects/${project.id}`, route => route.fulfill({ json: live }))
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ deliverableTypes: [referenceDeliverableType(typeId, 'Drawing Package')] }) }))
    await page.route(/\/api\/v1\/projects\/[^/]+\/deliverables(\?.*)?$/, route => {
      const request = route.request()
      if (request.method() === 'POST') {
        writes.push({ method: 'POST', path: new URL(request.url()).pathname, body: request.postDataJSON(), source: source(request) })
        return route.fulfill({ status: 201, headers: { location: `/api/v1/deliverables/${createdId}` }, json: deliverableCreated({ id: createdId, key: 'D-003', warnings: ['Synthetic due date is after its milestone'] }) })
      }
      reads.push(where(request))
      return route.fulfill({ json: deliverableList(rows) })
    })
    await page.route(/\/api\/v1\/projects\/[^/]+\/deliverables\/bulk$/, route => {
      const request = route.request()
      writes.push({ method: 'POST', path: new URL(request.url()).pathname, body: request.postDataJSON(), source: source(request) })
      return route.fulfill({ json: { updated: 2, skipped: [] } })
    })
    await page.route(`**/api/v1/deliverables/${first}`, route => {
      const request = route.request()
      if (request.method() === 'PATCH') {
        writes.push({ method: 'PATCH', path: new URL(request.url()).pathname, body: request.postDataJSON(), ifMatch: request.headers()['if-match'], source: source(request) })
        rows[0].name = request.postDataJSON().name ?? rows[0].name; rows[0].rowVersion++
        return route.fulfill({ json: { id: first, rowVersion: rows[0].rowVersion, warnings: ['Synthetic edit warning'] } })
      }
      reads.push(where(request))
      return route.fulfill({ json: detail() })
    })
    await page.route(`**/api/v1/deliverables/${first}/transition`, route => {
      const request = route.request()
      writes.push({ method: 'POST', path: new URL(request.url()).pathname, body: request.postDataJSON(), source: source(request) })
      return route.fulfill({ json: { id: first, status: 'In Review', rowVersion: rows[0].rowVersion + 1 } })
    })
    await page.route(`**/api/v1/deliverables/${first}/issue`, route => {
      const request = route.request()
      writes.push({ method: 'POST', path: new URL(request.url()).pathname, body: request.postDataJSON(), source: source(request) })
      if (!issueRefused) { issueRefused = true; return route.fulfill({ status: 409, json: { code: 'confirm_open_tasks', title: 'Conflict', status: 409, detail: 'One task is still open.', tasks: [{ id: panelIds.milestone, key: 'T-001', name: 'Open task', status: 'In Progress' }] } }) }
      return route.fulfill({ json: { id: first, status: 'Issued', rowVersion: rows[0].rowVersion + 1, openTasks: [] } })
    })
    await page.route(/\/api\/v1\/items\/Deliverable\/[^/]+\/(history|activity)/, route => route.fulfill({ json: { items: [], page: 1, pageSize: 100, totalCount: 0 } }))

    // Register: the contract path with the tasks asked for and the filter's text sent as it is, then Create.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/deliverables?requiresReview=false`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Synthetic deliverable', exact: true }).first().waitFor({ state: 'visible', timeout: 20000 })
    const list = reads.find(x => x.startsWith(`/api/v1/projects/${project.id}/deliverables?`))
    assert.ok(list, `the register is read from its contract path: ${reads}`)
    assert.deepEqual([...new URL(list, 'http://x').searchParams].sort(), [['IncludeTasks', 'true'], ['RequiresReview', 'false']], 'tasks are asked for and a false filter is not dropped')

    await page.getByRole('button', { name: 'New deliverable' }).first().click()
    const create = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'New deliverable', exact: true }) })
    await create.locator('#d-name').fill('Synthetic new deliverable')
    await create.locator('#d-type').selectOption(typeId)
    await create.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByText('Synthetic due date is after its milestone').first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/projects/${project.id}/deliverables`, source: 'UI', body: {
      name: 'Synthetic new deliverable', projectDisciplineId: civil.id, deliverableTypeId: typeId, description: null, priority: 'Medium', revision: null, requiresReview: true,
      ownerId: me.id, reviewerId: null, milestoneId: null, startDate: null, dueDate: null } })

    // Panel: the detail from its contract path; a rename carries the row version and shows the answer's warning.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/deliverables?panel=Deliverable:${first}`, { waitUntil: 'domcontentloaded' })
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: 'Synthetic deliverable', exact: true }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes(`/api/v1/deliverables/${first}`), 'the detail is read from its contract path')
    await dialog.getByRole('button', { name: 'Synthetic deliverable', exact: true }).first().click()
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true })
    await name.fill('Renamed deliverable')
    await name.press('Enter')
    await page.getByText('Synthetic edit warning').first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[1], { method: 'PATCH', path: `/api/v1/deliverables/${first}`, source: 'UI', ifMatch: '"3"', body: { name: 'Renamed deliverable' } })

    // A status change sends the contract body: no comment is null, the empty reason is the text the dialog gives, the row version is the one just saved.
    await dialog.getByRole('button', { name: 'Change status' }).click()
    await page.getByRole('menuitem', { name: 'In Review' }).click()
    await page.getByRole('dialog', { name: 'Move D-001 to In Review?' }).getByRole('button', { name: 'Confirm' }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[2], { method: 'POST', path: `/api/v1/deliverables/${first}/transition`, source: 'UI', body: { toStatus: 'In Review', reason: '', comment: null, rowVersion: 4 } })

    // Issue: the open-tasks conflict is shown, and the second send confirms it with every contract field present.
    await dialog.getByRole('button', { name: 'Issue deliverable' }).first().click()
    const issue = page.getByRole('dialog', { name: /^Issue D-001 / })
    await issue.getByRole('button', { name: 'Issue deliverable' }).click()
    await issue.getByText('T-001').waitFor({ state: 'visible' })
    await issue.getByRole('button', { name: 'Issue deliverable' }).click()
    await page.waitForTimeout(300)
    const [refused, confirmed] = [writes[3], writes[4]]
    assert.equal(refused.body.confirmOpenTasks, false); assert.equal(confirmed.body.confirmOpenTasks, true)
    assert.deepEqual(Object.keys(confirmed.body).sort(), ['confirmOpenTasks', 'issuedDate', 'issuedTo', 'note', 'revision', 'rowVersion', 'transmittalUrl'], 'every field of the contract body is sent')
    assert.equal(confirmed.path, `/api/v1/deliverables/${first}/issue`); assert.equal(confirmed.source, 'UI')

    // Bulk: the selection's ids, the operation and its params, with no reason for a plain change.
    await page.goto(`${preview.base}/projects/${project.projectNumber}/deliverables`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Renamed deliverable', exact: true }).first().waitFor({ state: 'visible', timeout: 20000 })
    await page.getByRole('checkbox', { name: 'Select all' }).first().check()
    await page.getByRole('button', { name: 'Shift due dates' }).click()
    const bulk = page.getByRole('dialog')
    await bulk.getByLabel('Days (negative moves earlier)').fill('3')
    await bulk.getByLabel('Reason').fill('Synthetic client delay')
    await bulk.getByRole('button', { name: 'Confirm' }).click()
    await page.waitForTimeout(300)
    const shifted = writes.at(-1)
    assert.deepEqual(shifted, { method: 'POST', path: `/api/v1/projects/${project.id}/deliverables/bulk`, source: 'UI',
      body: { ids: [first, secondId], operation: 'shiftDueDates', params: { days: 3 }, reason: 'Synthetic client delay' } })
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors.filter(x => !x.includes(' 409')), [], 'no API error but the refused issue')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
