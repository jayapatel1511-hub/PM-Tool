// 040-S15-Milestones: the Milestones tab and the milestone panel go through the typed client. The register reads the milestones from their contract
// path with the filters under the contract's names (ShowCompleted, Type); New milestone and Edit send the milestone body (the edit with the row
// version in If-Match); Change date asks for a dry run first, then sends the date with the reason and the row version; Mark complete asks again
// when deliverables are still open; Reopen and Cancel send the reason and the row version. Every write carries X-Hub-Source: UI. Against the
// mocked platform API; backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project } = require('./platform-fixtures.cjs')
const { milestoneRow, milestoneDetail, datePreview, milestoneCreated, milestoneVersion, milestoneCompleted, milestoneCancelled, dateChanged } = require('./milestones.fixture.cjs')

const port = Number(process.env.MILESTONES_TEST_PORT ?? 7396)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const m1 = id(901), m2 = id(902), m3 = id(903), newMilestone = id(904), phase = id(905), held = id(906)

// The project lets this caller manage its milestones.
Object.assign(project.permissions, { manageMilestones: { ok: true, reason: null } })

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let milestones = [
      milestoneRow({ id: m1, projectId: project.id, key: 'M-001', name: 'Issue for review', date: '2026-10-20', originalDate: '2026-10-20', deliverableTotal: 1, deliverableIssued: 0 }),
      milestoneRow({ id: m2, projectId: project.id, key: 'M-002', name: 'Site visit', milestoneType: 'Field Work', date: '2026-11-10', originalDate: '2026-11-10' }),
      milestoneRow({ id: m3, projectId: project.id, key: 'M-003', name: 'Kickoff', milestoneType: 'Kickoff', date: '2026-09-01', originalDate: '2026-09-01', isComplete: true, completedDate: '2026-09-01', rowVersion: 3 }),
    ]
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const change = (rowId, patch) => { milestones = milestones.map(x => x.id === rowId ? { ...x, ...patch, rowVersion: x.rowVersion + 1 } : x) }
    const version = (rowId) => milestones.find(x => x.id === rowId).rowVersion
    const dry = datePreview({ key: 'M-001', name: 'Issue for review', oldDate: '2026-10-20', newDate: '2026-10-27', slipDays: 7, delta: 7,
      deliverables: [{ id: id(931), key: 'D-001', name: 'Grading plan', oldDue: '2026-10-15', newDue: '2026-10-22' }], tasks: [] })

    await page.route(`**/api/v1/projects/${project.id}/milestones**`, route => {
      const w = send(route.request())
      if (w.method === 'GET') { reads.push({ path: w.path, query: w.query }); return route.fulfill({ json: w.path.endsWith('/export') ? [] : milestones.filter(x => w.query.ShowCompleted === 'true' || (!x.isComplete && !x.isCancelled)) }) }
      writes.push(w)
      milestones = [...milestones, milestoneRow({ id: newMilestone, projectId: project.id, key: 'M-004', name: w.body.name, milestoneType: w.body.milestoneType, date: w.body.date })]
      return route.fulfill({ status: 201, headers: { location: `/api/v1/milestones/${newMilestone}` }, json: milestoneCreated(newMilestone, 'M-004') })
    })
    await page.route('**/api/v1/milestones/**', route => {
      const w = send(route.request()), parts = w.path.split('/'), rowId = parts[4], row = milestones.find(x => x.id === rowId)
      if (w.method === 'GET' && parts.length === 5) { reads.push({ path: w.path }); return route.fulfill({ json: milestoneDetail({ milestone: row, permissions: { manage: { ok: true, reason: null } } }) }) }
      if (w.method === 'GET') return route.fulfill({ json: [] }) // history and the other reads of the panel
      writes.push(w)
      const command = parts[5]
      if (command === 'change-date') {
        if (w.body.dryRun) return route.fulfill({ json: dry })
        change(rowId, { date: w.body.newDate, slipDays: 7 })
        return route.fulfill({ json: dateChanged(rowId, version(rowId), dry) })
      }
      if (command === 'complete') {
        if (rowId === m1 && !w.body.confirm) return route.fulfill({ status: 409, contentType: 'application/problem+json', json: { code: 'confirm_open_deliverables', detail: 'Deliverables are still open.', deliverables: [{ id: held, key: 'D-001', name: 'Grading plan', status: 'In Progress' }] } })
        change(rowId, { isComplete: true, completedDate: w.body.completedDate })
        return route.fulfill({ json: milestoneCompleted(rowId, version(rowId), rowId === m1 ? { id: phase, name: 'Detailed Design' } : null) })
      }
      if (command === 'reopen') { change(rowId, { isComplete: false, completedDate: null }); return route.fulfill({ json: milestoneVersion(rowId, version(rowId)) }) }
      if (command === 'cancel') {
        change(rowId, { isCancelled: true, cancelledReason: w.body.reason })
        return route.fulfill({ json: milestoneCancelled(rowId, version(rowId), rowId === m2 ? [{ id: held, key: 'D-002', name: 'Site notes', rowVersion: 1 }] : []) })
      }
      change(rowId, w.body) // PATCH /api/v1/milestones/{id}
      return route.fulfill({ json: milestoneVersion(rowId, version(rowId)) })
    })

    const menu = async (key, item) => { await page.getByRole('button', { name: `Actions for ${key}` }).click(); await page.getByRole('menuitem', { name: item, exact: true }).click() }
    const lastRead = () => reads.filter(r => r.path === `/api/v1/projects/${project.id}/milestones`).at(-1)

    await page.goto(`${preview.base}/projects/${project.projectNumber}/milestones`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Actions for M-001' }).waitFor({ state: 'visible', timeout: 20000 })
    assert.deepEqual(lastRead().query, {}, 'an unfiltered register sends no filter')

    // The filters go out under the contract's names.
    await page.getByRole('button', { name: /^Filters/ }).click()
    await page.locator('#ms-type').selectOption('Kickoff')
    await page.waitForTimeout(300)
    assert.deepEqual(lastRead().query, { Type: 'Kickoff' })
    await page.getByRole('checkbox', { name: 'Show completed' }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(lastRead().query, { ShowCompleted: 'true', Type: 'Kickoff' })
    await page.locator('#ms-type').selectOption('')
    await page.waitForTimeout(300)
    assert.deepEqual(lastRead().query, { ShowCompleted: 'true' })
    await page.keyboard.press('Escape')
    await page.getByRole('button', { name: 'Actions for M-003' }).waitFor({ state: 'visible' })

    // New milestone: the name, type, date and client-facing flag; the discipline, phase and description go as null.
    await page.getByRole('button', { name: 'New milestone' }).first().click()
    const form = page.getByRole('dialog')
    await form.locator('#m-name').fill('Design freeze')
    await form.locator('#m-date').fill('2026-12-01')
    await form.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('button', { name: 'Actions for M-004' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/projects/${project.id}/milestones`, query: {}, body: { name: 'Design freeze', milestoneType: 'Design Submission', date: '2026-12-01', description: null, projectDisciplineId: null, completesPhaseId: null, isClientFacing: true }, ifMatch: null, source: 'UI' })

    // Edit: the row version in If-Match; a dated milestone keeps its date.
    await menu('M-001', 'Edit')
    await page.locator('#m-name').fill('Issue for client review')
    await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('button', { name: 'Issue for client review' }).first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[1], { method: 'PATCH', path: `/api/v1/milestones/${m1}`, query: {}, body: { name: 'Issue for client review', milestoneType: 'Design Submission', description: '', projectDisciplineId: null, completesPhaseId: null, isClientFacing: true }, ifMatch: '"1"', source: 'UI' })

    // Change date: a dry run first (with the cascade the person chose), then the date with the reason and the row version.
    await menu('M-002', 'Change date')
    const moving = page.getByRole('dialog')
    await moving.getByRole('checkbox', { name: 'Shift deliverables targeting this milestone by the same number of days' }).click()
    await moving.getByRole('checkbox', { name: 'Also shift their tasks' }).click()
    await moving.locator('#md-date').fill('2026-11-17')
    await moving.getByText('Grading plan').waitFor({ state: 'visible' })
    const dryRun = writes.filter(w => w.path === `/api/v1/milestones/${m2}/change-date`).at(-1)
    assert.deepEqual(dryRun, { method: 'POST', path: `/api/v1/milestones/${m2}/change-date`, query: {}, body: { newDate: '2026-11-17', cascadeDeliverables: true, includeTasks: true, dryRun: true, reason: null, rowVersion: null }, ifMatch: null, source: 'UI' })
    await moving.locator('#confirm-reason').fill('Client asked for a week')
    await moving.getByRole('button', { name: 'Change date', exact: true }).click()
    await page.getByRole('dialog').waitFor({ state: 'detached' })
    assert.deepEqual(writes.at(-1), { method: 'POST', path: `/api/v1/milestones/${m2}/change-date`, query: {}, body: { newDate: '2026-11-17', reason: 'Client asked for a week', cascadeDeliverables: true, includeTasks: true, dryRun: null, rowVersion: 1 }, ifMatch: null, source: 'UI' })

    // Mark complete: the first answer lists the deliverables still open; the second, sent with confirm, completes the milestone (and suggests its phase).
    const completeWrites = () => writes.filter(w => w.path === `/api/v1/milestones/${m1}/complete`)
    await menu('M-001', 'Mark complete')
    await page.getByRole('dialog').getByRole('button', { name: 'Mark complete', exact: true }).click()
    await page.getByText('Review the list and confirm again.').waitFor({ state: 'visible' })
    await page.getByRole('dialog').getByRole('button', { name: 'Mark complete', exact: true }).click()
    await page.getByRole('dialog').waitFor({ state: 'detached' })
    const [asked, confirmed] = completeWrites()
    assert.deepEqual(asked.body, { completedDate: asked.body.completedDate, confirm: false, rowVersion: 2 })
    assert.deepEqual(confirmed.body, { completedDate: asked.body.completedDate, confirm: true, rowVersion: 2 })
    assert.equal(asked.ifMatch, null); assert.equal(confirmed.source, 'UI')

    // Reopen: the reason and the row version.
    await menu('M-003', 'Reopen')
    await page.locator('#confirm-reason').fill('Closed by mistake')
    await page.getByRole('dialog').getByRole('button', { name: 'Confirm', exact: true }).click()
    await page.getByRole('dialog').waitFor({ state: 'detached' })
    assert.deepEqual(writes.at(-1), { method: 'POST', path: `/api/v1/milestones/${m3}/reopen`, query: {}, body: { reason: 'Closed by mistake', rowVersion: 3 }, ifMatch: null, source: 'UI' })

    // Cancel: the reason and the row version; the answer lists the deliverables still aimed at the milestone.
    await menu('M-002', 'Cancel milestone')
    await page.locator('#confirm-reason').fill('Visit dropped')
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel milestone', exact: true }).click()
    await page.getByRole('dialog').waitFor({ state: 'detached' })
    assert.deepEqual(writes.at(-1), { method: 'POST', path: `/api/v1/milestones/${m2}/cancel`, query: {}, body: { reason: 'Visit dropped', rowVersion: 2 }, ifMatch: null, source: 'UI' })

    // The panel reads the milestone from its contract path and shows what the detail carries.
    await page.getByRole('button', { name: 'Kickoff', exact: true }).first().click()
    await page.getByRole('heading', { name: 'Kickoff' }).waitFor({ state: 'visible' })
    assert.ok(reads.some(r => r.path === `/api/v1/milestones/${m3}`), 'the milestone is read from its contract path')

    assert.equal(writes.length, 8)
    assert.ok(writes.every(w => w.source === 'UI'), 'every write names its source')
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
