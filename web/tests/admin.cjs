// 040-S15-Admin: the Administration screens go through the typed client. Reference lists are read, edited, added, deactivated (the usage is read
// first) and reordered on their contract paths with the row version in If-Match; holidays are read for the year, added with no office as null and
// removed; settings save their value on the key's path; users are read with their filters, saved with If-Match, and have roles added and removed;
// the organisation activity reads page, size and the sign-in filter. Every write carries X-Hub-Source: UI. Against the mocked platform API;
// backend rules are tested separately.
const assert = require('node:assert/strict')
const { startPreview, newMockPage } = require('./platform-fixtures.cjs')
const { officeRow, holidayRow, settingRow, adminUser } = require('./admin.fixture.cjs')
const { referenceData, referenceOffice } = require('./me.fixture.cjs')

const port = Number(process.env.ADMIN_TEST_PORT ?? 7386)
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const halifax = id(901), moncton = id(902), orgDay = id(911), officeDay = id(912), alex = id(921), sam = id(922), jordan = id(923)

;(async () => {
  const preview = await startPreview(port)
  const ui = await newMockPage(preview.base)
  const { page } = ui
  try {
    const writes = [], reads = []
    const send = (request) => ({ method: request.method(), path: new URL(request.url()).pathname, query: Object.fromEntries(new URL(request.url()).searchParams), body: request.postData() ? request.postDataJSON() : null, ifMatch: request.headers()['if-match'] ?? null, source: request.headers()['x-hub-source'] })
    const bump = (list, rowId, change) => list.map(x => x.id === rowId ? { ...x, ...change, rowVersion: x.rowVersion + 1 } : x)

    let offices = [officeRow({ id: halifax, name: 'Halifax', code: 'HFX', sortOrder: 0 }), officeRow({ id: moncton, name: 'Moncton', code: 'MON', sortOrder: 1 })]
    let holidays = [holidayRow({ id: orgDay, date: '2026-07-01', name: 'Canada Day' }), holidayRow({ id: officeDay, date: '2026-08-03', name: 'Natal Day', officeId: halifax, office: 'Halifax' })]
    let users = [adminUser({ id: alex, displayName: 'Alex Chen', email: 'alex@hub.test', supervisorId: sam }), adminUser({ id: sam, displayName: 'Sam Roy', roles: [{ role: 'Supervisor', source: 'Manual' }] }), adminUser({ id: jordan, displayName: 'Jordan Lee', roles: [{ role: 'Admin', source: 'Manual' }] })]
    let settings = [
      settingRow({ key: 'task_due_soon_days', kind: 'Int', group: 'thresholds', default: 5 }),
      settingRow({ key: 'flag.task_pickup', kind: 'Bool', group: 'flags', default: false, packet: '049' }),
    ]

    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ offices: offices.map(o => referenceOffice(o.id, o.name, { code: o.code, isActive: o.isActive })) }) }))
    await page.route(/\/api\/v1\/admin\/offices(\/.*)?$/, route => {
      const request = route.request(), w = send(request)
      if (w.method === 'GET' && !w.path.endsWith('/usage')) { reads.push(w.path); return route.fulfill({ json: [...offices].sort((a, b) => a.sortOrder - b.sortOrder) }) } // the server lists by sort order
      if (w.method === 'GET') { reads.push(w.path); return route.fulfill({ json: { projects: 3 } }) }
      writes.push(w)
      if (w.method === 'POST') {
        const row = officeRow({ id: id(903), name: w.body.name, code: w.body.code, timeZone: w.body.timeZone, sortOrder: offices.length, rowVersion: 0 })
        offices = [...offices, row]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/admin/offices/${row.id}` }, json: row })
      }
      const rowId = w.path.split('/')[5]
      offices = bump(offices, rowId, w.body)
      return route.fulfill({ json: offices.find(o => o.id === rowId) })
    })
    await page.route(/\/api\/v1\/admin\/holidays(\/.*|\?.*)?$/, route => {
      const request = route.request(), w = send(request)
      if (w.method === 'GET') { reads.push(`${w.path}?${new URLSearchParams(w.query)}`); return route.fulfill({ json: holidays }) }
      writes.push(w)
      if (w.method === 'POST') {
        holidays = [...holidays, holidayRow({ id: id(913), date: w.body.date, name: w.body.name })]
        return route.fulfill({ status: 201, headers: { location: `/api/v1/admin/holidays/${id(913)}` }, json: { id: id(913) } })
      }
      holidays = holidays.filter(h => h.id !== w.path.split('/')[5])
      return route.fulfill({ status: 204 })
    })
    await page.route(/\/api\/v1\/admin\/settings(\/.*)?$/, route => {
      const request = route.request(), w = send(request)
      if (w.method === 'GET') { reads.push(w.path); return route.fulfill({ json: settings }) }
      writes.push(w)
      const key = decodeURIComponent(w.path.split('/')[5])
      settings = settings.map(s => s.key === key ? { ...s, value: w.body.value } : s)
      return route.fulfill({ json: { key, value: w.body.value } })
    })
    await page.route(/\/api\/v1\/admin\/users(\/.*|\?.*)?$/, route => {
      const request = route.request(), w = send(request)
      if (w.method === 'GET') { reads.push(`${w.path}?${new URLSearchParams(w.query)}`); return route.fulfill({ json: users }) }
      writes.push(w)
      const parts = w.path.split('/'), userId = parts[5]
      if (w.method === 'PATCH') { users = bump(users, userId, w.body); return route.fulfill({ json: { id: userId, rowVersion: users.find(u => u.id === userId).rowVersion } }) }
      return route.fulfill({ status: 204 })
    })
    await page.route(/\/api\/v1\/admin\/activity(\?.*)?$/, route => {
      reads.push(`${new URL(route.request().url()).pathname}?${new URL(route.request().url()).searchParams}`)
      return route.fulfill({ json: { items: [], page: 1, pageSize: 50, totalCount: 0 } })
    })

    const row = (name) => page.locator('tr').filter({ has: page.getByRole('cell', { name, exact: true }) }) // rows by their name cell: another row's time zone can read the same
    // Reference list: read from its contract path, edited with If-Match, added, deactivated after reading the usage, reordered.
    await page.goto(`${preview.base}/admin/reference/offices`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('row', { name: /Moncton/ }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes('/api/v1/admin/offices'), 'the list is read from its contract path')

    await page.getByRole('row', { name: /Moncton/ }).getByRole('button', { name: 'Edit' }).click()
    await page.getByLabel('Name', { exact: true }).fill('Moncton North')
    await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('row', { name: /Moncton North/ }).waitFor({ state: 'visible' })
    assert.equal(writes[0].method, 'PATCH'); assert.equal(writes[0].path, `/api/v1/admin/offices/${moncton}`)
    assert.equal(writes[0].ifMatch, '"1"'); assert.equal(writes[0].source, 'UI'); assert.equal(writes[0].body.name, 'Moncton North')

    await page.getByRole('button', { name: 'Add offices' }).click()
    await page.getByLabel('Name', { exact: true }).fill('Fredericton')
    await page.getByLabel('Code', { exact: true }).fill('FRE')
    await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('row', { name: /Fredericton/ }).waitFor({ state: 'visible' })
    assert.equal(writes[1].method, 'POST'); assert.equal(writes[1].path, '/api/v1/admin/offices'); assert.equal(writes[1].ifMatch, null)
    assert.deepEqual([writes[1].body.name, writes[1].body.code, 'timeZone' in writes[1].body, writes[1].source], ['Fredericton', 'FRE', false, 'UI']) // the time zone is left to the server's default

    await row('Halifax').getByRole('button', { name: 'Deactivate' }).click()
    await page.getByText('Used by 3 projects.').waitFor({ state: 'visible' })
    assert.ok(reads.includes(`/api/v1/admin/offices/${halifax}/usage`), 'the usage is read before deactivating')
    await page.getByRole('dialog').getByRole('button', { name: 'Deactivate' }).click()
    await row('Halifax').getByRole('button', { name: 'Activate' }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[2], { method: 'PATCH', path: `/api/v1/admin/offices/${halifax}`, query: {}, body: { id: halifax, rowVersion: 1, isActive: false }, ifMatch: '"1"', source: 'UI' })

    await row('Halifax').getByRole('button', { name: 'Move down' }).click()
    await page.waitForFunction(() => document.querySelectorAll('tbody tr')[0]?.textContent?.includes('Moncton North'))
    assert.deepEqual(writes.slice(3, 5).map(w => [w.method, w.path, w.body, w.ifMatch]), [
      ['PATCH', `/api/v1/admin/offices/${halifax}`, { sortOrder: 1 }, '"2"'],
      ['PATCH', `/api/v1/admin/offices/${moncton}`, { sortOrder: 0 }, '"2"'],
    ])

    // Holidays: the year's list on its contract path (no office), add with the organisation's calendar as null, remove.
    await page.goto(`${preview.base}/admin/holidays`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('row', { name: /Natal Day/ }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.some(r => r.startsWith('/api/v1/admin/holidays?year=')), 'the list is read with its year')
    assert.ok(!reads.some(r => r.includes('officeId')), 'no office filter is sent for all calendars')
    await page.getByLabel('Date').fill('2026-11-11')
    await page.getByLabel('Name', { exact: true }).fill('Remembrance Day')
    await page.getByRole('button', { name: 'Add', exact: true }).click()
    await page.getByText('Saved').first().waitFor({ state: 'visible' })
    assert.deepEqual(writes[5], { method: 'POST', path: '/api/v1/admin/holidays', query: {}, body: { officeId: null, date: '2026-11-11', name: 'Remembrance Day' }, ifMatch: null, source: 'UI' })
    await page.getByRole('button', { name: 'Remove Natal Day' }).click()
    await page.getByRole('row', { name: /Natal Day/ }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[6], { method: 'DELETE', path: `/api/v1/admin/holidays/${officeDay}`, query: {}, body: null, ifMatch: null, source: 'UI' })

    // Settings: a switch and a number save their value on the key's path.
    await page.goto(`${preview.base}/admin/settings`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('switch').first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes('/api/v1/admin/settings'), 'the settings are read from their contract path')
    await page.getByRole('switch').first().click()
    await page.waitForFunction(() => document.querySelector('[role=switch]')?.getAttribute('aria-checked') === 'true')
    assert.deepEqual(writes[7], { method: 'PUT', path: '/api/v1/admin/settings/flag.task_pickup', query: {}, body: { value: true }, ifMatch: null, source: 'UI' })
    const days = page.locator('#set-task_due_soon_days')
    await days.fill('7')
    await days.press('Enter')
    await page.waitForFunction(() => document.querySelector('#set-task_due_soon_days')?.value === '7' && !document.querySelector('[role=status]'))
    assert.deepEqual(writes[8], { method: 'PUT', path: '/api/v1/admin/settings/task_due_soon_days', query: {}, body: { value: 7 }, ifMatch: null, source: 'UI' })

    // Users: the list and its filters, a save with If-Match, and a role added then removed.
    await page.goto(`${preview.base}/admin/users`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('row', { name: /Alex Chen/ }).first().waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes('/api/v1/admin/users?'), 'the list is read with no filter')
    assert.ok(reads.includes('/api/v1/admin/users?inactive=true'), 'the supervisor lookup reads inactive people too')
    await page.getByLabel('Search', { exact: true }).fill('alex')
    await page.waitForResponse(r => r.url().includes('/admin/users?q=alex'))
    await page.getByRole('row', { name: /Alex Chen/ }).getByRole('button', { name: 'Edit' }).click()
    await page.getByLabel('Weekly capacity (h)').fill('36')
    await page.getByRole('dialog').getByRole('button', { name: 'Save', exact: true }).click()
    await page.getByRole('dialog').waitFor({ state: 'detached' })
    const patched = writes[9]
    assert.equal(patched.method, 'PATCH'); assert.equal(patched.path, `/api/v1/admin/users/${alex}`); assert.equal(patched.ifMatch, '"1"'); assert.equal(patched.source, 'UI')
    assert.deepEqual(patched.body, { supervisorId: sam, officeId: null, homeDisciplineId: null, weeklyCapacityHours: 36, isTemplateEditor: false, isActive: true })
    await page.getByRole('row', { name: /Alex Chen/ }).getByRole('button', { name: 'Edit' }).click()
    await page.getByLabel('Add role').selectOption({ label: 'Supervisor' })
    await page.getByRole('dialog').getByRole('button', { name: 'Add', exact: true }).click()
    await page.getByRole('dialog').getByRole('button', { name: /^Remove Supervisor/ }).waitFor({ state: 'visible' })
    assert.deepEqual(writes[10], { method: 'POST', path: `/api/v1/admin/users/${alex}/roles`, query: {}, body: { role: 'Supervisor' }, ifMatch: null, source: 'UI' })
    await page.getByRole('dialog').getByRole('button', { name: /^Remove Supervisor/ }).click()
    await page.getByRole('dialog').getByRole('button', { name: /^Remove Supervisor/ }).waitFor({ state: 'detached' })
    assert.deepEqual(writes[11], { method: 'DELETE', path: `/api/v1/admin/users/${alex}/roles/Supervisor`, query: {}, body: null, ifMatch: null, source: 'UI' })

    // Organisation activity: page, size and the sign-in filter.
    await page.goto(`${preview.base}/admin/activity`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Hide sign-ins').waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(reads.includes('/api/v1/admin/activity?page=1&pageSize=50&excludeAction=SignedIn'), 'the activity read names page, size and the sign-in filter')

    assert.equal(writes.length, 12)
    assert.deepEqual(ui.mock.unknownGets, [], 'no unmocked read')
    assert.deepEqual(ui.mock.apiErrors, [], 'no API error')
    console.log(JSON.stringify({ result: 'PASS', writes: writes.length, reads: reads.length }))
  } finally {
    await ui.browser.close()
    await new Promise(resolve => { preview.child.once('exit', resolve); preview.child.kill() })
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
