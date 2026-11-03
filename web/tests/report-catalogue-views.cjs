// S23-A synthetic browser acceptance. The real eligibility/persistence boundary has API coverage.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const civil = project.disciplines[0].disciplineId
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const param = (key, label, type, def = null) => ({ key, label, type, default: def, options: null })
const definitions = [
  { code: 'team-capacity', title: 'Team Capacity', question: 'Who has room in the coming weeks?', description: 'Weekly capacity and visible planned hours.', itemType: null, columns: [],
    params: [param('disciplineId', 'Discipline', 'discipline'), param('from', 'From', 'date'), param('weeks', 'Weeks', 'number', '12'), param('includeMyDrafts', 'Include my private drafts', 'bool', 'true')] },
  { code: 'workload-employee', title: 'Workload by Employee', question: 'Who has room in the coming weeks?', description: 'Estimated weekly work in your permitted scope.', itemType: null, columns: [], params: [param('disciplineId', 'Discipline', 'discipline')] },
  { code: 'project-activity-log', title: 'Project Activity Log', question: 'What changed on the project?', description: 'Current permitted activity records.', itemType: null, columns: [],
    params: [param('projectId', 'Project', 'project'), param('from', 'From', 'date'), param('to', 'To', 'date')] },
  { code: 'overdue-tasks', title: 'Overdue Tasks', question: 'What is late?', description: 'Tasks past their due dates.', itemType: 'Task', columns: [], params: [param('minDaysOverdue', 'Minimum days overdue', 'number', '1')] },
]

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const errors = [], runs = [], writes = [], exports = [], expectedRefusals = []
  let stored = [], viewer = 'PM', missingCapture = false, blockRun = false, releaseRun, runBlocked, failRun = false
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', e => errors.push(e)); page.on('console', m => {
      if (m.type() !== 'error') return
      if (m.location().url.includes('/api/v1/reports/team-capacity') && /403/.test(m.text())) return // deliberate refused current run below
      errors.push(new Error(m.text()))
    })
    await page.addInitScript(() => { window.__reportCopies = []; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async value => { window.__reportCopies.push(value) } } }) })
    me.flags = { ...me.flags, capacity_screens: true, capacity_reports: true, small_ux: true, visual_system: false, screen_patterns: false, home_overview: false, live_events: false }
    me.settings.today = '2026-09-14'
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')] }) }))
    await page.route('**/api/v1/reports**', async route => {
      const url = new URL(route.request().url()), parts = url.pathname.split('/'), code = parts[4]
      if (!code) return route.fulfill({ json: definitions.filter(def => me.flags.capacity_reports || def.code !== 'team-capacity').map(def => {
        const result = { ...def }; if (!me.flags.capacity_screens) delete result.question; return result
      }) })
      const def = definitions.find(item => item.code === code); assert.ok(def, code)
      const query = Object.fromEntries(url.searchParams)
      for (const key of ['catalogue', 'view', 'viewProject', 'panel']) if (me.flags.capacity_screens) assert.equal(query[key], undefined, `${key} is UI state, not a report parameter`)
      if (parts.at(-1) === 'export') { exports.push({ code, query }); return route.fulfill({ contentType: 'text/csv', body: 'Measure,Hours\nCapacity,40\n' }) }
      runs.push({ code, query, viewer })
      if (failRun && code === 'team-capacity') { expectedRefusals.push(code); return route.fulfill({ status: 403, json: { title: 'Synthetic report refusal', detail: 'Synthetic report refusal', status: 403 } }) }
      if (blockRun) await new Promise(resolve => { releaseRun = resolve; runBlocked() })
      const captured = { ...query }
      if (code === 'team-capacity') { captured.from = query.from ?? '2026-09-14'; captured.weeks = query.weeks ?? '12'; captured.includeMyDrafts = query.includeMyDrafts ?? 'true' }
      if (code === 'overdue-tasks') captured.minDaysOverdue = query.minDaysOverdue ?? '1'
      if (code === 'project-activity-log') { if (!query.from && !query.to) captured.history = 'all'; else delete captured.history }
      return route.fulfill({ json: { code, title: def.title, itemType: def.itemType, columns: [{ path: 'measure', header: 'Measure', type: 'text' }, { path: 'hours', header: 'Hours', type: 'number' }],
        rows: [{ measure: viewer === 'Member' ? 'Recipient visible capacity' : 'Owner visible capacity', hours: viewer === 'Member' ? 40 : 72 }], total: 1, totalIsLowerBound: false, truncated: false, listLink: null, parameters: [],
        ...(me.flags.capacity_screens && !missingCapture ? { savedParameters: captured } : {}) } })
    })
    await page.route('**/api/v1/views**', async route => {
      const request = route.request(), url = new URL(request.url()), key = url.searchParams.get('listType'), container = url.searchParams.get('projectId') ?? null
      if (request.method() === 'GET') return route.fulfill({ json: viewList(stored.filter(row => row.listType === key && row.projectId === container && (row.scope === 'Project' || row.viewer === viewer))
        .map(row => ({ ...row, canEdit: row.scope === 'Personal' ? row.viewer === viewer : viewer === 'PM' })), viewer === 'PM' && !!container) })
      const body = request.postDataJSON(); writes.push({ method: request.method(), body, headers: request.headers() })
      if (request.method() === 'POST') {
        const next = savedView({ id: id(7100 + stored.length), name: body.name, scope: body.scope, projectId: body.projectId, params: body.params, isDefault: body.isDefault, canEdit: viewer === 'PM' || body.scope === 'Personal' })
        stored.push({ ...next, listType: body.listType, viewer }); return route.fulfill({ status: 201, json: viewSaved(next.id) })
      }
      const target = stored.find(row => row.id === url.pathname.split('/').at(-1)); assert.ok(target)
      if (body.params) { target.params = body.params; target.dropped = [] } target.rowVersion++
      return route.fulfill({ json: viewSaved(target.id, target.rowVersion) })
    })
    const catalogue = page.getByRole('navigation', { name: 'Report catalogue', exact: true })
    const copy = page.getByRole('button', { name: 'Copy link', exact: true })
    const viewSection = page.getByRole('region', { name: 'Views', exact: true })
    const watchResponse = predicate => {
      const pending = page.waitForResponse(predicate)
      // Preserve the awaited rejection while preventing a pending listener from masking an earlier failed action during cleanup.
      void pending.catch(() => {})
      return pending
    }
    const waitCapture = async () => copy.waitFor()
    const nextRun = (code, expected, status = 200) => watchResponse(response => {
      const url = new URL(response.url())
      return response.request().method() === 'GET' && url.pathname === `/api/v1/reports/${code}` && response.status() === status
        && [...url.searchParams].length === Object.keys(expected).length
        && Object.entries(expected).every(([key, value]) => url.searchParams.get(key) === value)
    })
    const finishRun = async (pending, capture = true) => {
      const response = await pending; await response.finished()
      if (capture) { assert.ok((await response.json()).savedParameters, 'the expected successful run supplies captured parameters'); await waitCapture() }
    }
    const copyLink = async () => {
      const previous = await page.evaluate(() => window.__reportCopies.length); await copy.click()
      await page.waitForFunction(n => window.__reportCopies.length === n + 1, previous)
      return new URL(await page.evaluate(() => window.__reportCopies.at(-1)))
    }

    await page.goto(`${base}/reports`); await catalogue.getByLabel('Search reports', { exact: true }).waitFor()
    await catalogue.getByLabel('Search reports', { exact: true }).fill('room')
    await page.waitForURL(url => url.searchParams.get('catalogue') === 'room')
    await page.waitForFunction(() => document.querySelectorAll('nav[aria-label="Report catalogue"] [data-report-code]').length === 2)
    assert.equal(await catalogue.getByRole('link').count(), 2)
    assert.equal(await catalogue.getByLabel('Search reports', { exact: true }).evaluate(el => document.activeElement === el), true)
    const initialRun = nextRun('team-capacity', {})
    await catalogue.getByRole('link', { name: /^Team Capacity/ }).click()
    await page.waitForURL(url => url.pathname === '/reports/team-capacity' && url.searchParams.get('catalogue') === 'room'); await finishRun(initialRun)
    await catalogue.locator('[data-report-code="team-capacity"][aria-current="page"]').waitFor()
    assert.equal(await catalogue.getByRole('link').count(), 2, 'catalogue stays beside the open report')
    const civilRun = nextRun('team-capacity', { disciplineId: civil })
    await page.getByLabel('Discipline', { exact: true }).selectOption(civil)
    await page.waitForURL(url => url.searchParams.get('disciplineId') === civil); await finishRun(civilRun)
    assert.equal(runs.at(-1).query.disciplineId, civil)
    const viewsBeforeContainer = runs.length
    const containerRead = watchResponse(response => {
      const url = new URL(response.url())
      return response.request().method() === 'GET' && url.pathname === '/api/v1/views' && url.searchParams.get('listType') === 'report:team-capacity' && url.searchParams.get('projectId') === project.id
    })
    await page.getByLabel('Views for project', { exact: true }).click()
    await page.getByRole('radio', { name: /P-MOCK.*Synthetic live project/ }).click()
    await page.waitForURL(url => url.searchParams.get('viewProject') === project.id)
    await (await containerRead).finished(); await waitCapture()
    await viewSection.getByRole('button', { name: 'Views', exact: true }).waitFor()
    assert.equal(runs.length, viewsBeforeContainer, 'sharing container does not trigger a new data query or become Team Capacity projectId')
    await viewSection.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Save this view', exact: true })
    await dialog.getByLabel('Name', { exact: true }).fill('Civil twelve weeks')
    await dialog.getByRole('checkbox', { name: 'Share with everyone on this project', exact: true }).check()
    await dialog.getByRole('button', { name: 'Save', exact: true }).click()
    await page.waitForURL(url => url.searchParams.has('view')); await waitCapture()
    assert.equal(writes[0].body.listType, 'report:team-capacity'); assert.equal(writes[0].body.projectId, project.id); assert.equal(writes[0].body.scope, 'Project')
    assert.deepEqual(writes[0].body.params, { disciplineId: civil, from: '2026-09-14', weeks: '12', includeMyDrafts: 'true' })
    const copied = await copyLink()
    assert.equal(copied.pathname, '/reports/team-capacity'); assert.deepEqual(Object.fromEntries(copied.searchParams), writes[0].body.params)
    for (const key of ['catalogue', 'viewProject', 'view', 'panel']) assert.equal(copied.searchParams.has(key), false)
    viewer = 'Member'; const recipientRun = nextRun('team-capacity', writes[0].body.params)
    await page.goto(copied.toString()); await finishRun(recipientRun)
    await page.getByRole('cell', { name: 'Recipient visible capacity', exact: true }).waitFor()
    assert.deepEqual(runs.at(-1).query, writes[0].body.params, 'recipient gets the same parameters under their own read')
    assert.equal(runs.at(-1).viewer, 'Member')

    // Existing project sharing rules are reflected by canShare/canEdit, not invented by Reports.
    const memberRun = nextRun('team-capacity', { disciplineId: civil })
    await page.goto(`${base}/reports/team-capacity?viewProject=${project.id}&disciplineId=${civil}`); await finishRun(memberRun)
    await viewSection.getByRole('button', { name: 'Views', exact: true }).click()
    const appliedRun = nextRun('team-capacity', writes[0].body.params)
    await page.getByRole('menuitem', { name: 'Civil twelve weeks', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('weeks') === '12' && url.searchParams.has('view')); await finishRun(appliedRun)
    await viewSection.getByRole('button', { name: 'Civil twelve weeks', exact: true }).click()
    assert.equal(await page.getByRole('menuitem', { name: /Update/ }).count(), 0)
    await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    assert.equal(await dialog.getByRole('checkbox', { name: 'Share with everyone on this project', exact: true }).count(), 0)
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click()

    // A cleaned invalid default must not silently apply its broader remaining parameters.
    viewer = 'PM'
    stored = [{ ...savedView({ id: id(7199), name: 'Unavailable saved scope', params: { weeks: '12' }, dropped: ['disciplineId'], isDefault: true }), listType: 'report:team-capacity', viewer }]
    const invalidDefaultRun = nextRun('team-capacity', {})
    await page.goto(`${base}/reports/team-capacity`); await finishRun(invalidDefaultRun)
    await viewSection.getByRole('button', { name: 'Views', exact: true }).click()
    const unavailable = page.getByRole('menuitem', { name: /^Unavailable saved scope/ }); await unavailable.waitFor()
    assert.equal(await unavailable.getAttribute('aria-disabled'), 'true'); assert.equal(new URL(page.url()).searchParams.has('view'), false)
    const firstRepair = watchResponse(response => response.request().method() === 'PATCH' && response.url().includes(`/views/${id(7199)}`))
    await page.getByRole('menuitem', { name: 'Update "Unavailable saved scope" with current filters', exact: true }).click(); await firstRepair
    assert.equal(writes.at(-1).body.params.weeks, '12'); assert.equal(writes.at(-1).body.params.disciplineId, undefined)
    assert.equal(new URL(page.url()).searchParams.has('view'), false, 'repair is deliberate and does not auto-apply a different scope')
    stored = [{ ...savedView({ id: id(7199), name: 'Unavailable saved scope', params: { weeks: '12' }, dropped: ['disciplineId'], isDefault: true }), listType: 'report:team-capacity', viewer }]
    const explicitRun = nextRun('team-capacity', { weeks: '8' })
    await page.goto(`${base}/reports/team-capacity?view=${id(7199)}&weeks=8`); await finishRun(explicitRun)
    await viewSection.getByRole('button', { name: 'Unavailable saved scope', exact: true }).click()
    assert.equal(await page.getByRole('menuitem', { name: 'Copy as personal view', exact: true }).getAttribute('aria-disabled'), 'true')
    const secondRepair = watchResponse(response => response.request().method() === 'PATCH' && response.url().includes(`/views/${id(7199)}`))
    await page.getByRole('menuitem', { name: 'Update "Unavailable saved scope" with current filters', exact: true }).click()
    await secondRepair
    await page.waitForFunction(() => !document.querySelector('[role="menu"]'))
    assert.equal(writes.at(-1).method, 'PATCH'); assert.equal(writes.at(-1).body.params.weeks, '8'); assert.equal(writes.at(-1).headers['if-match'], '"1"')

    stored = []; missingCapture = true
    const incompleteRun = nextRun('team-capacity', { weeks: '12' })
    await page.goto(`${base}/reports/team-capacity?catalogue=room&weeks=12`)
    await finishRun(incompleteRun, false)
    await page.getByText('Run the report successfully before saving or copying its parameters.', { exact: true }).waitFor()
    assert.equal(await copy.count(), 0)
    await viewSection.getByRole('button', { name: 'Views', exact: true }).click()
    assert.equal(await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).getAttribute('aria-disabled'), 'true')
    await page.keyboard.press('Escape')
    missingCapture = false; blockRun = true
    const pending = new Promise(resolve => { runBlocked = resolve })
    const blockedRun = nextRun('team-capacity', { weeks: '12' })
    await page.reload({ waitUntil: 'domcontentloaded' }); await pending
    assert.equal(await copy.count(), 0)
    await viewSection.getByRole('button', { name: 'Views', exact: true }).click()
    assert.equal(await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).getAttribute('aria-disabled'), 'true')
    await page.keyboard.press('Escape')
    blockRun = false; releaseRun(); await finishRun(blockedRun)

    // Read/apply/default is available before required params exist; capture actions are independent.
    stored = [{ ...savedView({ id: id(7200), name: 'Default project log', params: { projectId: project.id, history: 'all' }, isDefault: true }), listType: 'report:project-activity-log', viewer }]
    const bootstrappedRun = nextRun('project-activity-log', { projectId: project.id, history: 'all' })
    await page.goto(`${base}/reports/project-activity-log`)
    await page.waitForURL(url => url.searchParams.get('projectId') === project.id && url.searchParams.get('view') === id(7200)); await finishRun(bootstrappedRun)
    assert.equal(runs.at(-1).code, 'project-activity-log'); assert.equal(runs.at(-1).query.history, 'all')
    stored = [{ ...savedView({ id: id(7201), name: 'Corrected capacity parameters', params: { weeks: '12', from: '2026-09-14' } }), listType: 'report:team-capacity', viewer }]
    failRun = true; const refusedRun = nextRun('team-capacity', { weeks: '8' }, 403)
    await page.goto(`${base}/reports/team-capacity?weeks=8`); await finishRun(refusedRun, false)
    await page.getByRole('alert').filter({ hasText: 'Synthetic report refusal' }).waitFor()
    assert.equal(await copy.count(), 0); assert.ok(expectedRefusals.length > 0)
    await viewSection.getByRole('button', { name: 'Views', exact: true }).click()
    assert.equal(await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).getAttribute('aria-disabled'), 'true')
    failRun = false; const correctedRun = nextRun('team-capacity', { weeks: '12', from: '2026-09-14' })
    await page.getByRole('menuitem', { name: 'Corrected capacity parameters', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('weeks') === '12' && url.searchParams.get('view') === id(7201)); await finishRun(correctedRun)
    stored = []

    const datedRun = nextRun('project-activity-log', { projectId: project.id, from: '2026-08-15', to: '2026-09-14' })
    await page.goto(`${base}/reports/project-activity-log?projectId=${project.id}&catalogue=changed`); await finishRun(datedRun)
    const dated = await copyLink(); assert.equal(dated.searchParams.get('from'), '2026-08-15'); assert.equal(dated.searchParams.get('to'), '2026-09-14'); assert.equal(dated.searchParams.has('history'), false)
    const historyRun = nextRun('project-activity-log', { projectId: project.id, history: 'all' })
    await page.getByRole('checkbox', { name: 'All history', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('history') === 'all'); await finishRun(historyRun)
    await page.waitForFunction(() => [...document.querySelectorAll('button[role="checkbox"]')].find(node => node.closest('label')?.textContent.includes('All history'))?.getAttribute('aria-checked') === 'true')
    const history = await copyLink(); assert.equal(history.searchParams.get('history'), 'all'); assert.equal(history.searchParams.has('from'), false); assert.equal(history.searchParams.has('to'), false)
    assert.equal(history.searchParams.get('projectId'), project.id)
    const exported = watchResponse(response => response.url().includes('/reports/project-activity-log/export'))
    await page.getByRole('button', { name: 'Export', exact: true }).click(); await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click(); await exported
    await page.waitForFunction(() => !document.querySelector('[role="menu"]'))
    assert.equal(exports.at(-1).query.history, 'all'); assert.equal(exports.at(-1).query.catalogue, undefined)

    await page.setViewportSize({ width: 390, height: 844 })
    const phoneRun = nextRun('team-capacity', {})
    await page.goto(`${base}/reports/team-capacity?catalogue=room`); await finishRun(phoneRun)
    const details = page.locator('details.report-catalogue')
    await details.locator('summary').click(); await catalogue.getByLabel('Search reports', { exact: true }).waitFor()
    assert.equal(await catalogue.getByLabel('Search reports', { exact: true }).inputValue(), 'room')
    const workloadRun = nextRun('workload-employee', {})
    await catalogue.getByRole('link', { name: /^Workload by Employee/ }).click()
    await page.waitForURL(url => url.pathname === '/reports/workload-employee' && url.searchParams.get('catalogue') === 'room'); await finishRun(workloadRun)
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
    me.flags.capacity_reports = false
    await page.goto(`${base}/reports?catalogue=room`); await catalogue.getByLabel('Search reports', { exact: true }).waitFor()
    assert.equal(await catalogue.getByRole('link', { name: /^Team Capacity/ }).count(), 0)
    me.flags.capacity_reports = true; me.flags.capacity_screens = false; await page.setViewportSize({ width: 1440, height: 1000 })
    await page.goto(`${base}/reports`); await page.getByRole('link', { name: 'Team Capacity', exact: true }).waitFor()
    assert.equal(await catalogue.count(), 0); assert.equal(await page.getByLabel('Search reports', { exact: true }).count(), 0)
    const legacyRun = nextRun('team-capacity', { weeks: '12' })
    await page.goto(`${base}/reports/team-capacity?weeks=12`); await finishRun(legacyRun, false); await page.getByRole('cell', { name: 'Owner visible capacity', exact: true }).waitFor()
    assert.equal(await catalogue.count(), 0); assert.equal(await copy.count(), 0); assert.equal(await viewSection.count(), 0)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [], errors.map(e => e.stack ?? e.message).join('\n'))
    console.log('PASS: searchable adjacent catalogue, report-specific saved/copied applied parameters, project container, recipient scope, incomplete/default/settlement guards, history, phone and independent off state')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw error }
  finally { if (releaseRun) releaseRun(); await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
