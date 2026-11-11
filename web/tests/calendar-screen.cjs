// S15 actual-SPA synthetic contract proof. Provider cases own real authorization, persistence and canonical date effects.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./calendar-screen.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the coordinator-owned local preview')
;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], unknown = [], writes = [], reads = [], previews = [], observations = [], screenReplies = []
  let actor = f.actorFor(), staleOnce = true, refuse = false
  const scope = f.ids.project, url = `/calendar?projects=${scope}&date=2026-11-02`
  const allowed = ['from', 'to', 'projectIds', 'types', 'personId', 'disciplineId', 'showAway', 'undatedPage', 'undatedPageSize']
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request(), uri = new URL(request.url()), path = uri.pathname.replace('/api/v1/', ''), query = uri.searchParams
      if (request.method() === 'POST' && path === 'me/sign-in') return route.fallback()
      const sourceMatch = /^(tasks|deliverables|milestones)\/([^/]+)(\/change-date)?$/.exec(path)
      if (sourceMatch && request.method() !== 'GET') {
        const type = sourceMatch[1] === 'tasks' ? 'Task' : sourceMatch[1] === 'deliverables' ? 'Deliverable' : 'Milestone'
        const source = f.source(type, sourceMatch[2]), body = request.postDataJSON()
        assert.equal(request.method(), type === 'Milestone' ? 'POST' : 'PATCH')
        assert.equal(sourceMatch[3] ?? '', type === 'Milestone' ? '/change-date' : '')
        assert.ok(Object.keys(body).every(key => (type === 'Milestone' ? ['newDate', 'reason', 'cascadeDeliverables', 'includeTasks', 'dryRun', 'rowVersion'] : ['dueDate', 'reason']).includes(key)))
        assert.ok(!Object.hasOwn(body, 'startDate'), 'Calendar due markers never shift start dates')
        const version = type === 'Milestone' ? body.rowVersion : Number(request.headers()['if-match']?.replaceAll('"', ''))
        writes.push({ type, id: source.id, body: structuredClone(body), version })
        if (type === 'Milestone') { assert.equal(body.cascadeDeliverables, false); assert.equal(body.includeTasks, false); assert.equal(body.dryRun, false) }
        if (source.needsReason) assert.ok(body.reason?.trim())
        if (staleOnce) {
          staleOnce = false; source.rowVersion++
          return route.fulfill({ status: 409, json: { status: 409, code: 'concurrency_conflict', detail: 'Synthetic original-version conflict.', currentRowVersion: source.rowVersion } })
        }
        assert.equal(version, source.rowVersion, 'A deliberate new intent uses only its newly observed version')
        const start = source.startDate, changed = f.commit(type, source.id, type === 'Milestone' ? body.newDate : body.dueDate)
        assert.equal(changed.startDate, start)
        return route.fulfill({ json: { id: changed.id, rowVersion: changed.rowVersion, warnings: [] } })
      }
      if (path === 'views' && request.method() === 'POST') {
        assert.equal(actor.flags.work_screens, true)
        const body = request.postDataJSON(); assert.equal(body.listType, 'calendar'); assert.equal(body.projectId, scope)
        assert.ok(['Project', 'Personal'].includes(body.scope))
        assert.ok(Object.keys(body.params).every(key => ['mode', 'date', 'hide', 'projects', 'ws', 'showAway', 'personId', 'disciplineId'].includes(key)))
        if (body.scope === 'Project') { assert.equal(body.params.projects, scope); assert.ok(!Object.hasOwn(body.params, 'ws')) }
        return route.fulfill({ status: 201, json: f.saveView(body) })
      }
      if (request.method() !== 'GET') { unknown.push(request.method() + ' ' + path); return route.fulfill({ status: 405, json: {} }) }
      reads.push({ path, query: Object.fromEntries(query) })
      if (path === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: actor })
      if (path === 'reference') return route.fulfill({ json: f.reference })
      if (path === 'projects') {
        const normalized = Object.fromEntries([...query].map(([key, value]) => [key.toLowerCase(), value]))
        assert.ok(Object.keys(normalized).every(key => ['ids', 'includearchived', 'pagesize'].includes(key)))
        assert.equal(normalized.ids, scope); assert.equal(normalized.includearchived, 'true'); assert.equal(normalized.pagesize, '200')
        return route.fulfill({ json: f.projects })
      }
      if (path === 'calendar/screen') {
        assert.equal(actor.flags.work_screens, true); assert.ok([...query.keys()].every(key => allowed.includes(key)))
        assert.equal(query.get('projectIds'), scope); assert.ok(query.get('showAway') === null || ['true', 'false'].includes(query.get('showAway')))
        const types = query.get('types')
        if (types !== null && types !== '' && types !== 'none') assert.ok(types.split(',').every(type => ['Deadline', 'Meeting', 'Site Work', 'Internal Task'].includes(type)), 'Only finite business types or standalone none')
        assert.ok(!query.get('personId') || [f.ids.viewer, f.ids.other].includes(query.get('personId')))
        assert.ok(!query.get('disciplineId') || [f.ids.civil, f.ids.electrical].includes(query.get('disciplineId')))
        if (refuse) return route.fulfill({ status: 404, json: { detail: 'Synthetic current Calendar refusal.' } })
        const reply = f.read(query)
        screenReplies.push({ query: Object.fromEntries(query), reply: structuredClone(reply) })
        if (types === 'none') { assert.equal(reply.range.entries.length, 0); assert.equal(reply.undated.totalCount, 0); assert.equal(reply.undated.items.length, 0) }
        return route.fulfill({ json: reply })
      }
      const match = /^calendar\/screen\/(Task|Deliverable|Milestone)\/([^/]+)\/date-preview$/.exec(path)
      if (match) {
        assert.equal(actor.flags.work_screens, true)
        assert.ok([...query.keys()].every(key => ['dueDate', 'rowVersion', 'projectIds'].includes(key))); assert.equal(query.get('projectIds'), scope)
        const source = f.source(match[1], match[2]); assert.match(query.get('dueDate'), /^\d{4}-\d{2}-\d{2}$/)
        const version = Number(query.get('rowVersion')); previews.push({ type: match[1], id: match[2], version, date: query.get('dueDate') })
        if (version !== source.rowVersion) return route.fulfill({ status: 409, json: { status: 409, code: 'concurrency_conflict', detail: 'Synthetic original-version conflict.' } })
        return route.fulfill({ json: f.preview(match[1], match[2], query.get('dueDate')) })
      }
      if (path === 'calendar') { assert.equal(actor.flags.work_screens, false); return route.fulfill({ json: f.legacyRange(query) }) }
      if (path === 'views' && query.get('listType') === 'calendar') {
        assert.equal(actor.flags.work_screens, true); assert.ok([...query.keys()].every(key => ['listType', 'projectId'].includes(key)))
        assert.equal(query.get('projectId'), scope); return route.fulfill({ json: f.viewList(f.views, true) })
      }
      if (['config', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'].includes(path)) return route.fallback()
      unknown.push('GET ' + path); return route.fulfill({ status: 501, json: { detail: 'Unexpected finite Calendar GET' } })
    })
    const layers = [['Project deadlines', 'Deadline'], ['Meetings', 'Meeting'], ['Site work', 'Site Work'], ['Internal tasks', 'Internal Task']]
    // §41 R1: the layers, Time away, Person and Discipline open from Filters (a popover on desktop); set filters show as chips.
    const filters = page.locator('[data-calendar-filters]')
    const openFilters = async () => { if (!(await filters.isVisible())) { await page.getByRole('button', { name: /^Filters/ }).click(); await filters.waitFor({ state: 'visible' }) } }
    const closeFilters = async () => { if (await filters.isVisible()) { await page.keyboard.press('Escape'); await filters.waitFor({ state: 'hidden' }) } }
    const waitForClear = async () => {
      await page.waitForURL(uri => !['showAway', 'personId', 'disciplineId', 'hide'].some(key => uri.searchParams.has(key)))
      await openFilters()
      await page.getByRole('button', { name: 'Time away', exact: true, pressed: false }).waitFor({ state: 'visible' })
      await openFilters()
      await page.waitForFunction(() => document.querySelector('#calendar-person')?.value === '' && document.querySelector('#calendar-discipline')?.value === '')
      await openFilters()
      for (const [label] of layers) await page.getByRole('button', { name: label, exact: true, pressed: true }).waitFor({ state: 'visible' })
      await page.waitForFunction(() => document.querySelectorAll('[data-calendar-away]').length === 0)
      await page.getByText('Synthetic ownerless milestone', { exact: true }).waitFor({ state: 'visible' })
      await closeFilters()
    }
    await page.goto(base + url)
    await openFilters()
    await page.getByRole('button', { name: 'Time away', exact: true }).click()
    await page.waitForURL(uri => uri.searchParams.get('showAway') === 'true')
    await openFilters()
    await page.getByRole('button', { name: 'Time away', exact: true, pressed: true }).waitFor({ state: 'visible' })
    await page.locator('[data-calendar-away]').getByText('Synthetic Calendar PM · Away', { exact: true }).waitFor({ state: 'visible' })
    await page.locator('[data-calendar-away]').getByText('Synthetic Calendar PM · Away 4 h', { exact: true }).waitFor({ state: 'visible' })
    await page.locator('[data-calendar-away]').getByText(/Unavailable.*Unverified/).waitFor({ state: 'visible' })
    const beforeLayersOff = screenReplies.length
    const noBusinessResponse = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/v1/calendar/screen'
      && new URL(response.url()).searchParams.get('types') === 'none' && response.status() === 200)
    for (const [label, type] of layers) {
      await openFilters()
      await page.getByRole('button', { name: label, exact: true }).click()
      await page.waitForURL(uri => (uri.searchParams.get('hide') ?? '').split(',').includes(type) && uri.searchParams.get('showAway') === 'true')
      await page.getByRole('button', { name: label, exact: true, pressed: false }).waitFor({ state: 'visible' })
    }
    await noBusinessResponse
    await page.getByText('Synthetic due marker', { exact: true }).waitFor({ state: 'hidden' })
    await page.getByText('Synthetic visible meeting', { exact: true }).waitFor({ state: 'hidden' })
    await page.getByRole('heading', { name: 'Undated (0)', exact: true }).waitFor({ state: 'visible' })
    await page.locator('[data-calendar-away]').getByText('Synthetic Calendar PM · Away 4 h', { exact: true }).waitFor({ state: 'visible' })
    const awayOnly = screenReplies.slice(beforeLayersOff).filter(entry => entry.query.types === 'none')
    assert.ok(awayOnly.length > 0); assert.ok(awayOnly.every(entry => entry.query.showAway === 'true' && entry.reply.range.entries.length === 0 && entry.reply.undated.totalCount === 0 && entry.reply.away.days.length > 0))
    for (const [label, type] of layers) {
      await openFilters()
      await page.getByRole('button', { name: label, exact: true }).click()
      await page.waitForURL(uri => !(uri.searchParams.get('hide') ?? '').split(',').includes(type) && uri.searchParams.get('showAway') === 'true')
      await page.getByRole('button', { name: label, exact: true, pressed: true }).waitFor({ state: 'visible' })
    }
    await page.getByText('Synthetic due marker', { exact: true }).waitFor({ state: 'visible' })
    observations.push('actual all-business-layers-off sends standalone types=none through qs, suppresses entries/tray, and keeps nonempty permitted Away')
    const selfResponse = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/v1/calendar/screen'
      && new URL(response.url()).searchParams.get('personId') === f.ids.viewer && new URL(response.url()).searchParams.get('showAway') === 'true'
      && !new URL(response.url()).searchParams.has('disciplineId') && response.status() === 200)
    await openFilters()
    await page.getByLabel('Person', { exact: true }).selectOption(f.ids.viewer)
    await page.waitForURL(uri => uri.searchParams.get('personId') === f.ids.viewer && uri.searchParams.get('showAway') === 'true')
    await openFilters()
    await page.waitForFunction(id => document.querySelector('#calendar-person')?.value === id, f.ids.viewer)
    await selfResponse
    await page.getByRole('button', { name: /S-CAL-T001/ }).first().waitFor({ state: 'visible' })
    await page.getByText('Synthetic ownerless milestone', { exact: true }).waitFor({ state: 'hidden' })
    const disciplineResponse = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/v1/calendar/screen'
      && new URL(response.url()).searchParams.get('personId') === f.ids.viewer && new URL(response.url()).searchParams.get('disciplineId') === f.ids.civil
      && new URL(response.url()).searchParams.get('showAway') === 'true' && response.status() === 200)
    await openFilters()
    await page.getByLabel('Discipline', { exact: true }).selectOption(f.ids.civil)
    await page.waitForURL(uri => uri.searchParams.get('disciplineId') === f.ids.civil && uri.searchParams.get('personId') === f.ids.viewer && uri.searchParams.get('showAway') === 'true')
    await openFilters()
    await page.waitForFunction(id => document.querySelector('#calendar-discipline')?.value === id, f.ids.civil)
    await disciplineResponse
    await page.getByText('Synthetic visible meeting', { exact: true }).waitFor({ state: 'hidden' })
    await closeFilters()
    await page.getByRole('button', { name: 'Clear filters', exact: true }).click()
    await waitForClear()
    assert.equal(new URL(page.url()).searchParams.get('date'), '2026-11-02'); assert.equal(new URL(page.url()).searchParams.get('projects'), scope)
    await openFilters()
    await page.getByRole('button', { name: 'Time away', exact: true }).click()
    await page.waitForURL(uri => uri.searchParams.get('showAway') === 'true' && !uri.searchParams.has('personId') && !uri.searchParams.has('disciplineId'))
    await openFilters()
    await page.getByRole('button', { name: 'Time away', exact: true, pressed: true }).waitFor({ state: 'visible' })
    await page.locator('[data-calendar-away]').getByText('Synthetic Calendar PM · Away 4 h', { exact: true }).waitFor({ state: 'visible' })
    const peerResponse = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/v1/calendar/screen'
      && new URL(response.url()).searchParams.get('personId') === f.ids.other && new URL(response.url()).searchParams.get('showAway') === 'true'
      && !new URL(response.url()).searchParams.has('disciplineId') && response.status() === 200)
    await openFilters()
    await page.getByLabel('Person', { exact: true }).selectOption(f.ids.other)
    await page.waitForURL(uri => uri.searchParams.get('personId') === f.ids.other && uri.searchParams.get('showAway') === 'true' && !uri.searchParams.has('disciplineId'))
    await openFilters()
    await page.waitForFunction(id => document.querySelector('#calendar-person')?.value === id, f.ids.other)
    await peerResponse
    await openFilters()
    await page.getByRole('button', { name: 'Time away', exact: true, pressed: true }).waitFor({ state: 'visible' })
    const peer = screenReplies.filter(entry => entry.query.personId === f.ids.other && entry.query.showAway === 'true' && !entry.query.disciplineId).at(-1)
    assert.ok(peer); assert.equal(peer.reply.away.state, 'Unavailable'); assert.equal(peer.reply.away.days.length, 0)
    await page.getByText('Time away is unavailable for this people scope.', { exact: true }).waitFor({ state: 'visible' })
    await page.getByText('Synthetic drawing deadline', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await page.locator('[data-calendar-away]').count(), 0)
    await closeFilters()
    await page.getByRole('button', { name: 'Clear filters', exact: true }).click()
    await waitForClear()
    await page.getByText('Synthetic due marker', { exact: true }).waitFor({ state: 'visible' })
    observations.push('real layer toggle and source-specific person/work-discipline filters preserve ownerless exclusion; Clear retains date/scope')

    const task = page.locator(`#calendar-source-Task-${f.ids.task}`), target = page.locator('[data-calendar-drop-day="2026-11-05"]').first()
    await task.dragTo(target)
    const dialog = page.getByRole('dialog', { name: 'Move S-CAL-T001', exact: true })
    await dialog.getByText(/Synthetic successor would become Date Inconsistent/).waitFor({ state: 'visible' })
    assert.equal(writes.length, 0); assert.equal(await dialog.getByRole('button', { name: 'Confirm date', exact: true }).isEnabled(), false)
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    await task.waitFor({ state: 'visible' }); await page.waitForFunction(id => document.activeElement?.id === id, `calendar-source-Task-${f.ids.task}`)
    assert.equal(writes.length, 0)
    await task.dragTo(target)
    const originalReason = 'Keep this original Calendar intent ' + 'x'.repeat(4100)
    await dialog.getByLabel('Reason', { exact: true }).fill(originalReason)
    await dialog.getByRole('button', { name: 'Confirm date', exact: true }).click()
    await dialog.getByText('Synthetic original-version conflict.', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await dialog.getByLabel('Reason', { exact: true }).inputValue(), originalReason)
    assert.equal(writes[0].body.reason, originalReason)
    assert.equal(await dialog.locator('[data-source-version]').getAttribute('data-source-version'), '7')
    assert.equal(await dialog.getByRole('button', { name: 'Confirm date', exact: true }).isEnabled(), false)
    assert.equal(writes.length, 1); assert.equal(writes[0].version, 7); assert.equal(writes[0].body.dueDate, '2026-11-05')
    await dialog.getByRole('button', { name: 'Reload and start again', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    await page.getByRole('button', { name: 'Move S-CAL-T001', exact: true }).click()
    await dialog.getByLabel('New due date', { exact: true }).fill('2026-11-06')
    await dialog.getByLabel('Reason', { exact: true }).fill('Deliberate Calendar intent after reload')
    await dialog.locator('[data-calendar-date-preview]').waitFor({ state: 'visible' })
    await dialog.getByRole('button', { name: 'Confirm date', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    assert.equal(writes.length, 2); assert.equal(writes[1].version, 8); assert.equal(f.source('Task', f.ids.task).startDate, '2026-11-01')
    observations.push('actual desktop drag previews without writes; Cancel restores exact focus;409 retains original date/reason/version; explicit reload creates a deliberate due-only intent')

    await page.getByRole('button', { name: 'Move S-CAL-T002', exact: true }).click()
    const undated = page.getByRole('dialog', { name: 'Move S-CAL-T002', exact: true })
    const undatedPreview = page.waitForResponse(response => response.request().method() === 'GET'
      && new URL(response.url()).pathname === `/api/v1/calendar/screen/Task/${f.ids.undatedTask}/date-preview`
      && new URL(response.url()).searchParams.get('dueDate') === '2026-11-07'
      && new URL(response.url()).searchParams.get('rowVersion') === '2' && response.status() === 200)
    await undated.getByLabel('New due date', { exact: true }).fill('2026-11-07')
    await undatedPreview
    await undated.locator('[data-calendar-date-preview]').waitFor({ state: 'attached' })
    assert.ok(previews.some(row => row.type === 'Task' && row.id === f.ids.undatedTask && row.version === 2 && row.date === '2026-11-07'))
    assert.equal(await undated.locator('[data-source-version]').getAttribute('data-source-version'), '2')
    assert.equal(await undated.getByLabel('New due date', { exact: true }).inputValue(), '2026-11-07')
    assert.equal(writes.length, 2); assert.equal(await undated.getByRole('button', { name: 'Confirm date', exact: true }).isEnabled(), true)
    await undated.getByRole('button', { name: 'Confirm date', exact: true }).click(); await undated.waitFor({ state: 'hidden' })
    assert.equal(writes.length, 3); assert.equal(writes[2].version, 2); assert.equal(writes[2].body.dueDate, '2026-11-07')
    assert.equal(f.source('Task', f.ids.undatedTask).startDate, null); assert.equal(f.source('Task', f.ids.undatedTask).dueDate, '2026-11-07')
    assert.equal(await page.getByRole('button', { name: 'Move S-CAL-D002', exact: true }).count(), 0)
    await page.getByRole('button', { name: 'Move S-CAL-D001', exact: true }).click()
    const drawing = page.getByRole('dialog', { name: 'Move S-CAL-D001', exact: true })
    await drawing.getByLabel('New due date', { exact: true }).fill('2026-11-02')
    await drawing.getByText(/Synthetic child would fall after/).waitFor({ state: 'visible' }); await drawing.getByRole('button', { name: 'Cancel', exact: true }).click()
    await page.getByRole('button', { name: 'Move S-CAL-M001', exact: true }).click()
    const milestone = page.getByRole('dialog', { name: 'Move S-CAL-M001', exact: true })
    await milestone.getByLabel('New due date', { exact: true }).fill('2026-11-02')
    await milestone.getByText(/S-CAL-D001.*2026-11-03/).waitFor({ state: 'visible' }); assert.equal(await milestone.getByRole('checkbox').count(), 0)
    await milestone.getByRole('button', { name: 'Cancel', exact: true }).click()
    assert.equal(writes.length, 3); assert.ok(previews.some(row => row.type === 'Milestone'))

    await openFilters()
    await page.getByRole('button', { name: 'Time away', exact: true }).click()
    await page.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    const view = page.getByRole('dialog', { name: 'Save this view', exact: true })
    await view.getByLabel('Name', { exact: true }).fill('Synthetic Calendar review')
    await view.getByRole('checkbox', { name: 'Share with everyone on this project', exact: true }).check()
    await view.getByRole('button', { name: 'Save', exact: true }).click(); await view.waitFor({ state: 'hidden' })
    assert.equal(f.views.length, 1); assert.equal(f.views[0].scope, 'Project'); assert.equal(f.views[0].params.projects, scope)
    const beforeOff = reads.length; actor = f.actorFor(false)
    await page.goto(base + url + '&showAway=true&personId=' + f.ids.viewer)
    await page.getByText('Synthetic due marker', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await page.locator('[data-calendar-undated]').count(), 0)
    assert.ok(reads.slice(beforeOff).every(row => !row.path.startsWith('calendar/screen')))
    actor = f.actorFor(true); await page.goto(base + url)
    await page.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Synthetic Calendar review', exact: true }).click()
    await page.locator('[data-calendar-away]').getByText('Synthetic Calendar PM · Away 4 h', { exact: true }).waitFor({ state: 'visible' })
    observations.push('single explicit-project shared definition is canonical; off hides additions and preserves stored definition for a real apply after on')

    await page.setViewportSize({ width: 375, height: 900 })
    await page.waitForFunction(id => document.getElementById(id)?.draggable === false, `calendar-source-Task-${f.ids.task}`)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Phone page has no global horizontal overflow')
    assert.notEqual(await task.getAttribute('draggable'), 'true')
    await page.getByRole('button', { name: 'Move S-CAL-T001', exact: true }).click()
    await dialog.getByLabel('New due date', { exact: true }).waitFor({ state: 'visible' })
    if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: process.env.TUESDAY_EVIDENCE_DIR + '/calendar-screen-phone.png', fullPage: true })
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click()
    await page.setViewportSize({ width: 1440, height: 1000 })
    if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: process.env.TUESDAY_EVIDENCE_DIR + '/calendar-screen-desktop.png', fullPage: true })
    const viewedAt = Date.now()
    await page.getByRole('button', { name: 'Month', exact: true }).click()
    await page.waitForFunction(start => Date.now() >= start + 15050, viewedAt, { timeout: 20000 })
    refuse = true
    const denied = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/v1/calendar/screen' && response.status() === 404)
    await page.getByRole('button', { name: 'Week', exact: true }).click(); await denied
    await page.getByRole('alert').getByText('This page or item does not exist, or you cannot view it.', { exact: true }).waitFor({ state: 'visible' })
    assert.equal(await page.locator('[data-calendar-away]').count(), 0); assert.equal(await page.getByRole('button', { name: 'Move S-CAL-T001', exact: true }).count(), 0)
    assert.deepEqual(errors, []); assert.deepEqual(unknown, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'calendar-screen', evidence: 'synthetic actual-SPA; provider owns persistence and authority', writes: writes.length, previews: previews.length, observations }))
  } catch (error) {
    console.error(JSON.stringify({ status: 'FAIL', suite: 'calendar-screen', url: page.url(), errors, unknown, writes, previews: previews.slice(-8), reads: reads.slice(-12),
      screenStates: screenReplies.slice(-6).map(entry => ({ query: entry.query, away: entry.reply.away.state, entries: entry.reply.range.entries.length })), observations }))
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
