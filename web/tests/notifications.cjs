// 040-S15-Notifications: the notification centre, the Following tab, the Preferences page and the follow levels go through the typed client. The
// centre reads its page from the contract path with the filters under the contract's names (unread, type, projectId, page, pageSize) and marks
// notifications read with the ids or all; the Following tab reads the feed (projectId, importantOnly, page, pageSize) and marks one project read with
// no body; Preferences saves a channel per event, resets it, saves each digest control as its own partial body and previews the weekly summary; the
// follow level selects PUT the level or DELETE the follow; the density menu PUTs the choice. Every write carries X-Hub-Source: UI. Against the mocked
// platform API (the pulse and the counts come from the shared mock); backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, project } = require('./platform-fixtures.cjs')
const { markedRead, notificationRow, notificationPage, preferenceEvent, preferences, followingProject, followingItem, followingFeed, followRead } = require('./notifications.fixture.cjs')

const port = Number(process.env.NOTIFICATIONS_TEST_PORT ?? 7397)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const n1 = id(801), n2 = id(802), n3 = id(803)

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, source: request.headers()['x-hub-source'] ?? null })
    let notifications = [
      notificationRow({ id: n1, title: 'Alex Chen assigned you P-MOCK-T0001 Grading plan', projectId: project.id, projectNumber: project.projectNumber, itemType: 'Task', itemKey: 'P-MOCK-T0001', updatedAt: '2026-10-03T10:00:00Z' }),
      notificationRow({ id: n2, title: 'Sam Supervisor mentioned you on P-MOCK-T0002', eventType: 'Mention', projectId: project.id, projectNumber: project.projectNumber, updatedAt: '2026-10-03T09:00:00Z' }),
      notificationRow({ id: n3, title: 'Review outcome on P-MOCK-D0001', eventType: 'ReviewOutcome', readAt: '2026-10-03T09:30:00Z', updatedAt: '2026-10-02T09:00:00Z' }),
    ]
    const entry = { id: id(851), occurredAt: '2026-10-03T08:00:00Z', actorId: id(5), actorName: 'Sam Supervisor', actorType: 'User', projectId: project.id, itemType: 'Task', itemId: id(852), itemKey: 'P-MOCK-T0002',
      itemName: 'Culvert sizing', action: 'Updated', categories: [], changes: [], summary: 'Changed the due date', reason: null, source: 'Web', correlationId: null, snapshot: null, snapshotLabels: null }
    const feed = followingFeed([followingProject({ projectId: project.id, projectNumber: project.projectNumber, name: project.name, unread: 1 })], [followingItem({ entry, projectNumber: project.projectNumber, projectName: project.name })])
    let prefs = preferences({
      events: [preferenceEvent({ code: 'TaskAssigned', direct: true }), preferenceEvent({ code: 'Mention', app: false, custom: true })],
      digest: { sections: ['overdue', 'updates'], weeklySummaryEnabled: false, managesProjects: true },
      follows: [{ projectId: project.id, projectNumber: project.projectNumber, name: project.name, status: 'Active', level: 'AllActivity', source: 'Manual' }],
    })
    const summary = { subject: 'Hub weekly summary: 1 project', body: 'Hi Alex, here is the week for your project.', projects: [], requiredProjectIds: [] }

    await page.route(/\/api\/v1\/me\/(notifications|following|preferences|weekly-summary)/, route => {
      const w = send(route.request())
      if (w.path.endsWith('/pulse') || w.path.endsWith('/unread-count')) return route.fallback() // the shared mock answers the bell's polling
      if (w.method !== 'GET') writes.push(w); else reads.push({ path: w.path, query: w.query })
      if (w.path === '/api/v1/me/notifications' && w.method === 'GET') {
        const rows = notifications.filter(n => (w.query.unread !== 'true' || !n.readAt) && (!w.query.type || n.eventType === w.query.type) && (!w.query.projectId || n.projectId === w.query.projectId))
        return route.fulfill({ json: notificationPage(rows, { page: Number(w.query.page ?? 1), pageSize: Number(w.query.pageSize ?? 50) }) })
      }
      if (w.path === '/api/v1/me/notifications/read') {
        const open = notifications.filter(n => !n.readAt && (w.body.all === true || (w.body.ids ?? []).includes(n.id)))
        notifications = notifications.map(n => open.includes(n) ? { ...n, readAt: '2026-10-03T11:00:00Z' } : n)
        return route.fulfill({ json: markedRead(open.length) })
      }
      if (w.path === '/api/v1/me/following' && w.method === 'GET') return route.fulfill({ json: feed })
      if (w.path === `/api/v1/me/following/${project.id}/read`) return route.fulfill({ status: 204 })
      if (w.path === '/api/v1/me/preferences' && w.method === 'GET') return route.fulfill({ json: prefs })
      if (w.path === '/api/v1/me/preferences/digest') {
        const { enabled, time, sectionsOff, weeklySummary, holdDigestWhenAway } = w.body
        prefs = { ...prefs, digest: { ...prefs.digest, digestEnabled: enabled, ...(time ? { time } : {}), ...(sectionsOff ? { sectionsOff } : {}), ...(weeklySummary != null ? { weeklySummaryEnabled: weeklySummary } : {}), ...(holdDigestWhenAway != null ? { holdDigestWhenAway } : {}) } }
        return route.fulfill({ status: 204 })
      }
      if (w.path.startsWith('/api/v1/me/preferences/events/')) {
        const code = w.path.split('/').at(-1)
        prefs = { ...prefs, events: prefs.events.map(e => e.code !== code ? e : w.method === 'DELETE' ? preferenceEvent({ code, direct: e.direct }) : { ...e, ...w.body, custom: true }) }
        return route.fulfill({ status: 204 })
      }
      if (w.path === '/api/v1/me/weekly-summary') return route.fulfill({ json: summary })
      return route.fulfill({ status: 404, json: {} })
    })
    await page.route('**/api/v1/projects/*/follow', route => {
      const w = send(route.request())
      writes.push(w)
      if (w.method === 'DELETE') { prefs = { ...prefs, follows: [] }; return route.fulfill({ status: 204 }) }
      prefs = { ...prefs, follows: prefs.follows.map(f => ({ ...f, level: w.body.level })) }
      return route.fulfill({ json: followRead(w.body.level) })
    })
    await page.route('**/api/v1/me/preferences/density', route => { writes.push(send(route.request())); return route.fulfill({ status: 204 }) })

    // The centre: the page comes from the contract path; the filters go out under the contract's names.
    await page.goto(`${preview.base}/notifications`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Alex Chen assigned you P-MOCK-T0001 Grading plan').waitFor({ state: 'visible', timeout: 20000 })
    const lastList = () => reads.filter(r => r.path === '/api/v1/me/notifications').at(-1)
    assert.deepEqual(lastList().query, { page: '1', pageSize: '50' }, 'an unfiltered centre asks for the first page of 50')
    await page.locator('#notif-type').selectOption('Mention')
    await page.getByText('Sam Supervisor mentioned you on P-MOCK-T0002').waitFor({ state: 'visible' })
    await page.getByText('Review outcome on P-MOCK-D0001').waitFor({ state: 'detached' })
    assert.deepEqual(lastList().query, { type: 'Mention', page: '1', pageSize: '50' })
    await page.locator('#notif-type').selectOption('')
    await page.locator('#notif-project').selectOption(project.id)
    await page.getByRole('button', { name: 'Unread only' }).click()
    await page.waitForFunction(() => location.search.includes('unread=true'))
    await page.waitForTimeout(300)
    assert.deepEqual(lastList().query, { unread: 'true', projectId: project.id, page: '1', pageSize: '50' })

    // Marking one read sends its id; marking all sends all.
    await page.getByRole('button', { name: 'Mark read: Alex Chen assigned you P-MOCK-T0001 Grading plan' }).click()
    await page.waitForFunction(() => !document.body.innerText.includes('Alex Chen assigned you P-MOCK-T0001 Grading plan'))
    assert.deepEqual(writes[0], { method: 'POST', path: '/api/v1/me/notifications/read', query: {}, body: { ids: [n1] }, source: 'UI' })
    await page.getByRole('button', { name: 'Mark all read' }).click()
    await page.waitForFunction(() => !document.body.innerText.includes('Sam Supervisor mentioned you'))
    assert.deepEqual(writes[1], { method: 'POST', path: '/api/v1/me/notifications/read', query: {}, body: { all: true }, source: 'UI' })

    // The Following tab: the feed comes from its contract path with the filters under their names; one project marks read with no body.
    await page.goto(`${preview.base}/notifications?tab=following`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Culvert sizing').waitFor({ state: 'visible' })
    const lastFeed = () => reads.filter(r => r.path === '/api/v1/me/following').at(-1)
    assert.deepEqual(lastFeed().query, { page: '1', pageSize: '200' })
    await page.getByRole('button', { name: 'Important only' }).click()
    await page.waitForFunction(() => location.search.includes('important=true'))
    await page.waitForTimeout(300)
    assert.deepEqual(lastFeed().query, { importantOnly: 'true', page: '1', pageSize: '200' })
    await page.getByRole('button', { name: `Mark ${project.projectNumber} as read` }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[2], { method: 'POST', path: `/api/v1/me/following/${project.id}/read`, query: {}, body: null, source: 'UI' })

    // Preferences: a channel per event is saved with the other channel as it stood, and reset deletes the choice.
    await page.goto(`${preview.base}/preferences`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('checkbox', { name: 'Task assigned to you In-app' }).waitFor({ state: 'visible' })
    assert.ok(reads.some(r => r.path === '/api/v1/me/preferences'), 'the preferences are read from their contract path')
    await page.getByRole('checkbox', { name: 'Task assigned to you In-app' }).click()
    await page.getByRole('button', { name: 'Reset Task assigned to you to the default' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[3], { method: 'PUT', path: '/api/v1/me/preferences/events/TaskAssigned', query: {}, body: { app: false, email: true }, source: 'UI' })
    await page.getByRole('checkbox', { name: '@mention Email' }).click()
    await page.waitForFunction(() => document.querySelector('[aria-label="@mention Email"]')?.getAttribute('data-state') === 'unchecked')
    assert.deepEqual(writes[4], { method: 'PUT', path: '/api/v1/me/preferences/events/Mention', query: {}, body: { app: false, email: false }, source: 'UI' })
    await page.getByRole('button', { name: 'Reset @mention to the default' }).click()
    await page.getByRole('button', { name: 'Reset @mention to the default' }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[5], { method: 'DELETE', path: '/api/v1/me/preferences/events/Mention', query: {}, body: null, source: 'UI' })

    // The digest: each control saves its own partial body.
    await page.getByRole('switch', { name: "Hold my digest on days I'm away" }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[6], { method: 'PUT', path: '/api/v1/me/preferences/digest', query: {}, body: { enabled: true, time: '08:00', holdDigestWhenAway: true }, source: 'UI' })
    await page.locator('#digest-time').fill('07:30'); await page.locator('#digest-time').blur()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[7], { method: 'PUT', path: '/api/v1/me/preferences/digest', query: {}, body: { enabled: true, time: '07:30' }, source: 'UI' })
    await page.getByRole('checkbox', { name: 'Project updates' }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[8], { method: 'PUT', path: '/api/v1/me/preferences/digest', query: {}, body: { enabled: true, time: '07:30', sectionsOff: ['updates'] }, source: 'UI' })
    await page.getByRole('switch', { name: 'Weekly summary of my projects' }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[9], { method: 'PUT', path: '/api/v1/me/preferences/digest', query: {}, body: { enabled: true, time: '07:30', weeklySummary: true }, source: 'UI' })
    await page.getByRole('button', { name: 'Preview' }).click()
    await page.getByRole('heading', { name: summary.subject }).waitFor({ state: 'visible' })
    assert.ok(reads.some(r => r.path === '/api/v1/me/weekly-summary'), 'the weekly summary is read from its contract path')
    await page.keyboard.press('Escape')

    // The follow level: a level is a PUT with the level; "Not following" is a DELETE.
    const level = page.getByLabel(`Follow level for ${project.projectNumber} ${project.name}`)
    await level.selectOption('Muted')
    await page.waitForTimeout(300)
    assert.deepEqual(writes[10], { method: 'PUT', path: `/api/v1/projects/${project.id}/follow`, query: {}, body: { level: 'Muted' }, source: 'UI' })
    await level.selectOption('')
    await page.waitForTimeout(300)
    assert.deepEqual(writes[11], { method: 'DELETE', path: `/api/v1/projects/${project.id}/follow`, query: {}, body: null, source: 'UI' })

    // Density: the menu choice goes out as the dense-rows flag.
    await page.getByRole('button', { name: 'Account' }).click()
    await page.getByRole('menuitemradio', { name: 'Comfortable' }).click()
    await page.waitForTimeout(300)
    assert.deepEqual(writes[12], { method: 'PUT', path: '/api/v1/me/preferences/density', query: {}, body: { denseRows: false }, source: 'UI' })
    assert.equal(writes.length, 13)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
