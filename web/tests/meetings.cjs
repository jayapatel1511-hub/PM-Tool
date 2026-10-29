// 040-S15-Meetings: the Meetings tab and the action panel go through the typed client. The register reads the meetings and the actions from their
// contract paths, with the filters under the contract's names (Q, Status, OwnerType, Indicator); Record meeting and Edit send the meeting body (the
// edit with the row version in If-Match); Add action sends the owner as the discipline with the other owners null and no links; the panel reads the
// action from its contract path, a move sends the new status with the version in If-Match, Change owner sends the owner body, and Convert sends the
// assignee, due date and discipline with the version. Every write carries X-Hub-Source: UI. Against the mocked platform API; backend rules are
// tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project, task } = require('./platform-fixtures.cjs')
const { meetingRow, meetingRead, actionRow, actionDetail, actionCreated, actionMutation, actionMoved, actionConverted } = require('./meetings.fixture.cjs')
const { calendarEvent, calendarRange } = require('./calendar.fixture.cjs')

const port = Number(process.env.MEETINGS_TEST_PORT ?? 7395)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = project.projectDisciplines[0].id
const m1 = id(901), m2 = id(902), newMeeting = id(903), a1 = id(911), a2 = id(912), newAction = id(913), event = id(921)

// The project lets this caller run coordination and raise register items, and has one active discipline to own an action.
Object.assign(project.permissions, { runCoordination: { ok: true }, raiseRegister: { ok: true } })
project.disciplines = [{ ...project.disciplines[0], isActive: true, leadName: null }]

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    let meetings = [
      meetingRow({ id: m1, projectId: project.id, title: 'Client Progress Meeting #4', meetingDate: '2026-10-01', meetingType: 'Client', open: 2, total: 2 }),
      meetingRow({ id: m2, projectId: project.id, title: 'Weekly Coordination', meetingDate: '2026-09-24', calendarEventTitle: 'Coordination call' }),
    ]
    let actions = [
      actionRow({ id: a1, projectId: project.id, key: 'P-MOCK-A01', meetingId: m1, meetingTitle: 'Client Progress Meeting #4', meetingDate: '2026-10-01', text: 'Send the revised grading plan', dueDate: '2026-10-15', ownerUserId: id(2) }),
      actionRow({ id: a2, projectId: project.id, key: 'P-MOCK-A02', meetingId: m1, meetingTitle: 'Client Progress Meeting #4', meetingDate: '2026-10-01', text: 'Confirm catch basin spacing', dueDate: '2026-10-20', ownerUserId: id(2) }),
    ]
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const change = (list, rowId, patch) => list.map(x => x.id === rowId ? { ...x, ...patch, rowVersion: x.rowVersion + 1 } : x)
    const detail = (a) => actionDetail({ action: a, permissions: { edit: { ok: true, reason: null }, comment: true, convert: !a.converted && a.status !== 'Complete',
      transitions: a.converted ? [] : [{ to: 'In Progress', ok: true, reason: null }, { to: 'Complete', ok: true, reason: null }] },
    task: a.converted ? { id: task.id, key: 'P-MOCK-T0002', name: a.text, status: 'Not Started' } : null })

    await page.route('**/api/v1/calendar**', route => route.fulfill({ json: calendarRange('2026-10-03', '2026-10-09', [calendarEvent(event, project.id, { title: 'Site walk', start: '2026-10-06T09:00', end: '2026-10-06T10:00' })]) }))
    await page.route(`**/api/v1/projects/${project.id}/meetings**`, route => {
      const w = send(route.request())
      if (w.method === 'GET') { reads.push({ path: w.path }); return route.fulfill({ json: meetings }) }
      writes.push(w)
      const row = meetingRow({ id: newMeeting, projectId: project.id, title: w.body.title, meetingDate: w.body.meetingDate, meetingType: w.body.meetingType, notesLink: w.body.notesLink, calendarEventId: w.body.calendarEventId })
      meetings = [row, ...meetings]
      return route.fulfill({ status: 201, headers: { location: `/api/v1/meetings/${newMeeting}` }, json: meetingRead(row) })
    })
    await page.route(`**/api/v1/projects/${project.id}/actions**`, route => {
      const w = send(route.request())
      reads.push({ path: w.path, query: w.query })
      return route.fulfill({ json: actions })
    })
    await page.route('**/api/v1/meetings/**', route => {
      const w = send(route.request()), parts = w.path.split('/')
      writes.push(w)
      if (parts[5] === 'actions') { // POST /api/v1/meetings/{id}/actions
        actions = [...actions, actionRow({ id: newAction, projectId: project.id, key: 'P-MOCK-A03', meetingId: parts[4], text: w.body.text, dueDate: w.body.dueDate, ownerType: w.body.ownerType, ownerDisciplineId: w.body.ownerDisciplineId, ownerName: 'Civil' })]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/actions/${newAction}` }, json: actionCreated(newAction, 'P-MOCK-A03') })
      }
      meetings = change(meetings, parts[4], w.body)
      return route.fulfill({ json: meetingRead(meetings.find(x => x.id === parts[4])) })
    })
    await page.route('**/api/v1/actions/**', route => {
      const w = send(route.request()), parts = w.path.split('/'), actionId = parts[4], current = actions.find(x => x.id === actionId)
      if (w.method === 'GET') { reads.push({ path: w.path }); return route.fulfill({ json: detail(current) }) }
      writes.push(w)
      if (parts[5] === 'transition') { actions = change(actions, actionId, { status: w.body.toStatus }); return route.fulfill({ json: actionMoved(actionId, w.body.toStatus, current.rowVersion + 1) }) }
      if (parts[5] === 'convert') { actions = change(actions, actionId, { converted: true, status: 'In Progress', relatedTaskId: task.id, taskKey: 'P-MOCK-T0002' }); return route.fulfill({ json: actionConverted(actionId, 'In Progress', current.rowVersion + 1, task.id, 'P-MOCK-T0002') }) }
      actions = change(actions, actionId, { ownerType: w.body.ownerType, ownerUserId: w.body.ownerUserId, ownerDisciplineId: w.body.ownerDisciplineId, ownerName: 'Civil' })
      return route.fulfill({ json: actionMutation(actionId, current.rowVersion + 1) })
    })

    await page.goto(`${preview.base}/projects/${project.projectNumber}/meetings`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Client Progress Meeting #4' }).waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.some(r => r.path === `/api/v1/projects/${project.id}/meetings`), 'the meetings are read from their contract path')
    const lastActions = () => reads.filter(r => r.path === `/api/v1/projects/${project.id}/actions`).at(-1)
    assert.deepEqual(lastActions().query, {}, 'an unfiltered register sends no filter')

    // The filters go out under the contract's names.
    await page.locator('#action-search').fill('grading')
    await page.getByRole('button', { name: /^Filters/ }).click()
    await page.locator('#action-status').selectOption('Open,In Progress')
    await page.waitForFunction(() => location.search.includes('status='))
    await page.locator('#action-owner-type').selectOption('Discipline')
    await page.waitForFunction(() => location.search.includes('ownerType='))
    await page.keyboard.press('Escape')
    await page.getByRole('button', { name: 'Overdue', exact: true }).click()
    await page.waitForFunction(() => location.search.includes('indicator=overdue'))
    await page.waitForTimeout(300)
    assert.deepEqual(lastActions().query, { Q: 'grading', Status: 'Open,In Progress', OwnerType: 'Discipline', Indicator: 'overdue' })
    await page.getByRole('button', { name: 'Clear filters' }).click()
    await page.waitForFunction(() => !location.search.includes('indicator'))
    await page.waitForTimeout(300)
    assert.deepEqual(lastActions().query, {})

    // Record meeting: the title, date, type, notes link and the calendar event the person picked.
    await page.getByRole('button', { name: 'Record meeting' }).first().click()
    const form = page.getByRole('dialog')
    await form.locator('#m-title').fill('Site visit')
    await form.locator('#m-date').fill('2026-10-06')
    await form.locator('#m-type').selectOption('Site')
    await form.locator('#m-link').fill('https://example.test/minutes')
    await form.locator('#m-event').selectOption(event)
    await form.getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('heading', { name: 'Site visit' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[0], { method: 'POST', path: `/api/v1/projects/${project.id}/meetings`, query: {}, body: { title: 'Site visit', meetingDate: '2026-10-06', meetingType: 'Site', notesLink: 'https://example.test/minutes', calendarEventId: event }, ifMatch: null, source: 'UI' })

    // Edit: the same body with the row version in If-Match.
    await page.locator(`section[aria-labelledby="mt-${m1}"]`).getByRole('button', { name: 'Edit' }).click()
    await page.locator('#m-title').fill('Client Progress Meeting #4 (revised)')
    await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('heading', { name: 'Client Progress Meeting #4 (revised)' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[1], { method: 'PATCH', path: `/api/v1/meetings/${m1}`, query: {}, body: { title: 'Client Progress Meeting #4 (revised)', meetingDate: '2026-10-01', meetingType: 'Client', notesLink: null, calendarEventId: null }, ifMatch: '"1"', source: 'UI' })

    // Add action: owned by the discipline; the person, party and links go as null.
    await page.locator(`section[aria-labelledby="mt-${m1}"]`).getByRole('button', { name: 'Add action' }).click()
    const adding = page.getByRole('dialog')
    await adding.locator('#a-text').fill('Order the survey')
    await adding.getByRole('radio', { name: 'Discipline' }).check()
    await adding.getByRole('combobox', { name: 'Discipline' }).selectOption(civil)
    await adding.locator('#a-due').fill('2026-10-12')
    await adding.getByRole('button', { name: 'Add action', exact: true }).click()
    await page.getByRole('button', { name: 'Order the survey' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[2], { method: 'POST', path: `/api/v1/meetings/${m1}/actions`, query: {}, body: { text: 'Order the survey', ownerType: 'Discipline', ownerUserId: null, ownerDisciplineId: civil, ownerExternalPartyId: null, dueDate: '2026-10-12', relatedTaskId: null, relatedDecisionId: null, links: null }, ifMatch: null, source: 'UI' })

    // The panel: the action is read from its contract path; Start moves it with the version in If-Match.
    await page.getByRole('button', { name: 'Send the revised grading plan' }).click()
    await page.getByRole('button', { name: 'Start' }).waitFor({ state: 'visible' })
    assert.ok(reads.some(r => r.path === `/api/v1/actions/${a1}`), 'the action is read from its contract path')
    await page.getByRole('button', { name: 'Start' }).click()
    await page.getByRole('button', { name: 'Complete' }).waitFor({ state: 'visible' })
    await page.waitForFunction(() => document.body.innerText.includes('In Progress'))
    assert.deepEqual(writes[3], { method: 'POST', path: `/api/v1/actions/${a1}/transition`, query: {}, body: { toStatus: 'In Progress', reason: null, rowVersion: null }, ifMatch: '"1"', source: 'UI' })

    // Change owner: the owner body with the new version.
    await page.getByRole('button', { name: 'Change owner' }).click()
    const owner = page.getByRole('radiogroup', { name: 'Owner type' }).last()
    await owner.getByRole('radio', { name: 'Discipline' }).check()
    await page.getByRole('combobox', { name: 'Discipline' }).last().selectOption(civil)
    await page.getByRole('button', { name: 'Save', exact: true }).last().click()
    await page.getByRole('button', { name: 'Change owner' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[4], { method: 'PATCH', path: `/api/v1/actions/${a1}`, query: {}, body: { ownerType: 'Discipline', ownerUserId: null, ownerDisciplineId: civil, ownerExternalPartyId: null }, ifMatch: '"2"', source: 'UI' })

    // Convert to task: the discipline picked, the assignee and the due date as the action had them, the version in If-Match.
    await page.getByRole('button', { name: 'Convert to task' }).first().click()
    const convert = page.getByRole('dialog')
    await convert.locator('#c-disc').selectOption(civil)
    await convert.getByRole('button', { name: 'Convert to task' }).click()
    await page.waitForFunction(() => document.body.innerText.includes('Synthetic task'))
    assert.deepEqual(writes[5], { method: 'POST', path: `/api/v1/actions/${a1}/convert`, query: {}, body: { assigneeId: null, deliverableId: null, dueDate: '2026-10-15', projectDisciplineId: civil, rowVersion: null }, ifMatch: '"3"', source: 'UI' })
    assert.equal(writes.length, 6)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
