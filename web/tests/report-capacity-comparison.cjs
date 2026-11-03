const assert = require('node:assert/strict')
const { newMockPage, me, project, links, comments } = require('./platform-fixtures.cjs')
const { taskRow, taskDetail, taskPermissions, taskPermission } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const id = index => `00000000-0000-0000-0088-${String(index).padStart(12, '0')}`
const dates = ['2026-11-02', '2026-11-09', '2026-11-16', '2026-11-23', '2026-11-30']
const initial = { from: dates[0], weeks: '5', includeMyDrafts: 'false', unit: 'hours', period: 'weeks' }
const column = (path, header) => ({ path, header, type: path === 'measure' ? 'text' : 'number' })
const param = (key, label, type, value = null, options = null) => ({ key, label, type, default: value, options })
const definition = { code: 'team-capacity', title: 'Team Capacity', description: 'Effective availability and canonical planning bands.', question: 'Who has room in the coming weeks?', columns: [], itemType: null, params: [
  param('supervisorId', 'Supervisor', 'person'), param('disciplineId', 'Discipline', 'discipline'), param('from', 'From', 'date'), param('weeks', 'Weeks', 'number', '5'), param('includeMyDrafts', 'Include my private drafts', 'bool', 'true'),
  param('unit', 'Report unit', 'select', 'hours', ['hours', 'fte'].map(value => ({ value, label: value === 'fte' ? 'FTE' : 'Hours' }))),
  param('period', 'Period', 'select', 'weeks', ['weeks', 'months'].map(value => ({ value, label: value === 'months' ? 'Months' : 'Weeks' }))),
] }
const task = taskRow({ id: id(9000), projectId: project.id, key: 'T-9000', name: 'Synthetic chart task input', assigneeId: me.id, assigneeName: me.displayName })
const uiOnly = ['chartPeriod', 'chartPage', 'taskInput', 'inputPage', 'inputPageSize', 'catalogue', 'viewProject', 'view', 'panel']
const canonical = query => ({ from: dates[0], weeks: '5', includeMyDrafts: 'true', unit: 'hours', period: 'weeks', ...query })

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const runs = [], metrics = [], exports = [], saves = [], errors = [], geometry = []
  let mode = 'complete', standard = 40
  const watch = predicate => { const result = page.waitForResponse(predicate); void result.catch(() => {}); return result }
  const finished = async result => assert.equal(await (await result).finished(), null)
  const reportRead = query => watch(response => { const url = new URL(response.url()); return url.pathname === '/api/v1/reports/team-capacity' && Object.entries(query).every(([key, value]) => url.searchParams.get(key) === value) })
  const section = () => page.locator('[data-report-capacity-comparison]')
  const dataRow = key => section().locator(`[data-report-comparison-row="${key}"]`)
  const group = key => section().locator(`[data-report-chart-group="${key}"]`)
  const firstKey = `discipline:${id(1)}`
  const openControls = async () => { const disclosure = page.locator('details.report-controls'); if (await disclosure.count() && await disclosure.getAttribute('open') === null) await disclosure.locator('summary').click(); await page.getByRole('form', { name: 'Report parameters', exact: true }).waitFor() }
  const goto = async (params = initial, focus = {}) => { const read = reportRead(params); await page.goto(`${base}/reports/team-capacity?${new URLSearchParams({ ...params, ...focus })}`); await finished(read); await page.locator(me.flags.capacity_screens ? 'table[data-report-table="team-capacity"] tbody tr' : 'main#content table tbody tr').first().waitFor() }
  const make = parameters => {
    const periods = []
    dates.forEach(day => { const thursday = new Date(`${day}T00:00:00Z`); thursday.setUTCDate(thursday.getUTCDate() + 3); const key = parameters.period === 'months' ? thursday.toISOString().slice(0, 7) : day
      let period = periods.find(period => period.key === key); if (!period) { period = { key, label: key, weeks: [], weekCount: 0, denominatorHours: null }; periods.push(period) } period.weeks.push(day); period.weekCount++; period.denominatorHours = standard > 0 ? period.weekCount * standard : null
    })
    const count = mode === 'empty' ? 0 : mode === 'capped' ? Math.floor(2000 / periods.length) : 14
    const capacityRows = Array.from({ length: count }, (_, index) => ({ key: index === 13 && mode !== 'capped' ? 'discipline:none' : `discipline:${id(index + 1)}`, disciplineId: index === 13 && mode !== 'capped' ? null : id(index + 1), discipline: index < 2 ? 'Civil' : index === 13 && mode !== 'capped' ? 'Not set' : `Synthetic discipline ${String(index).padStart(3, '0')}`,
      source: dates.map((_, week) => ({ capacity: mode === 'unknown' && index === 0 && week === 0 ? 36 : 40, confirmed: index === 0 && week === 0 ? 45 : index === 0 && week === 1 ? 25 : 35, expected: 5, possible: 70 })) }))
    const cell = (path, hours, period, unknown = false, provisional = false, noEvidence = false) => ({ path, kind: 'Hours', value: unknown || parameters.unit === 'fte' && standard <= 0 ? null : parameters.unit === 'fte' ? hours / period.denominatorHours : hours,
      knownHours: noEvidence ? null : hours, missingInputs: 0, coverage: provisional ? 'ProvisionalAvailability' : unknown ? 'KnownSubtotal' : parameters.unit === 'fte' && standard <= 0 ? 'StandardUnavailable' : 'VisibleOnly' })
    const comparisonRows = capacityRows.flatMap((group, index) => periods.map(period => {
      const indices = period.weeks.map(week => dates.indexOf(week)), partial = mode === 'unknown' && index === 0, provisional = partial && indices.includes(0)
      const sum = field => indices.reduce((total, week) => total + group.source[week][field], 0)
      const demand = sum('confirmed') + sum('expected'), deficit = indices.reduce((total, week) => total + Math.max(0, group.source[week].confirmed + group.source[week].expected - group.source[week].capacity), 0)
      return { key: group.key, disciplineId: group.disciplineId, discipline: group.discipline, periodKey: period.key,
        capacity: cell('capacity', sum('capacity'), period, provisional, provisional), confirmed: cell('confirmed', sum('confirmed'), period, partial), expected: cell('expected', sum('expected'), period, partial), possible: cell('possible', sum('possible'), period, partial),
        demand: cell('demand', demand, period, partial), deficit: cell('deficit', deficit, period, partial || provisional, provisional, partial || provisional), partialPeople: partial ? 1 : 0, provisionalPeople: provisional ? 1 : 0 }
    }))
    const measureNames = ['Capacity', 'Time away', 'Confirmed', 'Expected', 'Possible', 'Remaining capacity', 'People over-planned', 'People under-planned', 'People with partial view']
    const sourceValues = week => { const capacity = capacityRows.reduce((total, row) => total + row.source[week].capacity, 0), confirmed = capacityRows.reduce((total, row) => total + row.source[week].confirmed, 0), expected = count * 5; return [capacity, mode === 'unknown' && week === 0 ? 4 : 0, confirmed, expected, count * 70, capacity - confirmed - expected, count > 0 && week === 0 ? 1 : 0, 0, mode === 'unknown' ? 1 : 0] }
    const rows = measureNames.map((measure, row) => ({ measure, ...Object.fromEntries(dates.map((_, week) => [`w${week}`, sourceValues(week)[row]])) }))
    const columns = [column('measure', 'Measure'), ...dates.map((day, week) => column(`w${week}`, day))]
    const converted = parameters.unit !== 'hours' || parameters.period !== 'weeks'
    const selectedColumns = [columns[0], ...periods.map((period, index) => column(converted ? `u${index}` : `w${index}`, converted ? `${period.label} (${parameters.unit === 'fte' ? 'FTE' : 'h'}; ISO weeks: ${period.weekCount})` : period.label)), ...(converted ? dates.map((day, week) => column(`countW${week}`, `${day} · weekly people count`)) : [])]
    const notApplicable = path => ({ path, kind: 'NotApplicable', value: null, knownHours: null, missingInputs: 0, coverage: 'NotApplicable' })
    const typedRows = rows.map((_, row) => {
      const countCell = (path, week) => { const uncertain = mode === 'unknown' && (row === 6 && week === 0 || row === 7); return { path, kind: 'Count', value: uncertain ? null : rows[row][`w${week}`], knownHours: null, missingInputs: 0, coverage: uncertain ? 'ProvisionalAvailability' : row < 8 ? 'AvailabilityDependentCount' : 'VisibleOnly' } }
      const cells = periods.map((period, index) => {
        const path = converted ? `u${index}` : `w${index}`
        if (row >= 6) return converted ? notApplicable(path) : countCell(path, index)
        const provisional = mode === 'unknown' && [0, 1, 5].includes(row) && period.weeks.includes(dates[0]), partial = mode === 'unknown' && row === 5
        return cell(path, period.weeks.reduce((sum, week) => sum + rows[row][`w${dates.indexOf(week)}`], 0), period, partial || provisional, provisional)
      })
      if (converted) dates.forEach((_, week) => cells.push(row >= 6 ? countCell(`countW${week}`, week) : notApplicable(`countW${week}`)))
      return { rowIndex: row, kind: row >= 6 ? 'Count' : 'Hours', cells }
    })
    const input = metric => ({ count: metric === 'unestimated' && count > 0 ? 1 : 0, knownCount: metric === 'unestimated' && count > 0 ? 1 : 0, href: `/reports/team-capacity?${new URLSearchParams({ ...parameters, taskInput: metric, inputPage: '1', inputPageSize: '50' })}` })
    return { code: 'team-capacity', title: 'Team Capacity', itemType: null, columns, rows, total: rows.length, totalIsLowerBound: false, truncated: false, listLink: null, asOf: '2026-11-02T12:00:00Z',
      parameters: Object.entries(parameters).map(([key, value]) => ({ label: definition.params.find(param => param.key === key)?.label ?? key, value })), savedParameters: parameters,
      taskInputs: { basis: 'PlannerPeopleContext', scopeCoverage: 'VisibleOnly', unavailableReferences: 0, parameters, asOf: '2026-11-02T12:00:00Z', unestimated: input('unestimated'), openUndated: input('open-undated') },
      unitView: { unit: parameters.unit, period: parameters.period, standardWeeklyHours: standard, displayedWeekCount: dates.length, periods, columns: selectedColumns, rows: typedRows, scopeCoverage: 'VisibleOnly', unestimatedTasks: count > 0 ? 1 : 0 },
      capacityComparison: { groupBy: 'homeDiscipline', basis: 'PLN09ConfirmedExpected', unit: parameters.unit, asOf: '2026-11-02T12:00:00Z', scopeCoverage: 'VisibleOnly', periods, rows: comparisonRows, returnedGroups: count, totalGroups: mode === 'capped' ? count + 1 : count, truncated: mode === 'capped' } }
  }
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
    await page.addInitScript(() => { window.__chartCopies = []; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async value => window.__chartCopies.push(value) } }) })
    me.flags = { ...me.flags, capacity_reports: true, capacity_screens: true, visual_system: true, home_overview: false, screen_patterns: false, capacity_polish: false, work_screens: false, people_screens: false, small_ux: false, live_events: false }; me.settings.today = dates[0]
    await page.route('**/api/v1/reports**', route => { const url = new URL(route.request().url()), query = Object.fromEntries(url.searchParams), parts = url.pathname.split('/'); assert.equal(route.request().method(), 'GET')
      if (!parts[4]) return route.fulfill({ json: [{ ...definition, params: me.flags.capacity_screens ? definition.params : definition.params.filter(param => !['unit', 'period'].includes(param.key)) }] })
      uiOnly.forEach(key => assert.equal(query[key], undefined, `${key} must stay out of data requests`))
      if (parts[5] === 'export') { exports.push(query); return route.fulfill({ contentType: 'text/csv', body: 'Measure,2026-11-02\nCapacity,560\n' }) }
      if (parts[5] === 'task-inputs') { metrics.push(query); const { page: current, pageSize, ...parameters } = query, hasTask = mode !== 'empty'; return route.fulfill({ json: { code: 'team-capacity', metric: parts[6], basis: 'PlannerPeopleContext', scopeCoverage: 'VisibleOnly', unavailableReferences: 0, total: hasTask ? 1 : 0, knownTotal: hasTask ? 1 : 0, page: Number(current), pageSize: Number(pageSize), parameters, asOf: '2026-11-02T12:00:00Z', items: hasTask ? [task] : [] } }) }
      runs.push(query); const body = make(canonical(query)); if (!me.flags.capacity_screens) { delete body.capacityComparison; delete body.unitView; delete body.taskInputs; delete body.savedParameters }
      return route.fulfill({ json: body })
    })
    await page.route('**/api/v1/views**', route => { if (route.request().method() === 'GET') return route.fulfill({ json: viewList() }); const body = route.request().postDataJSON(); saves.push(body); return route.fulfill({ status: 201, json: viewSaved(id(9999)) }) })
    await page.route('**/api/v1/tasks/**', route => route.fulfill({ json: route.request().url().endsWith('/dependencies') ? taskDependencies() : taskDetail({ task, permissions: taskPermissions({ edit: taskPermission(false), assign: taskPermission(false), setReviewer: taskPermission(false), dueDate: taskPermission(false), block: taskPermission(false), delete: taskPermission(false), comment: false, dependencies: false, enterTime: false }) }) }))
    await page.route('**/api/v1/items/Task/**', route => route.fulfill({ json: route.request().url().endsWith('/comments') ? comments : links }))

    await goto(); await group(firstKey).getByText('short by 10 h', { exact: false }).waitFor()
    assert.equal(await section().locator('[data-report-capacity-chart]').count(), 1); assert.equal(await section().locator('select').count(), 1)
    assert.equal(await section().locator('[data-report-chart-group]').count(), 12); assert.equal(await section().locator('[data-report-comparison-row]').count(), 12)
    assert.equal(await section().getByRole('option', { name: /person/i }).count(), 0)
    for (const key of await section().locator('[data-report-chart-group]').evaluateAll(groups => groups.map(group => group.getAttribute('data-report-chart-group'))))
      for (const measure of ['capacity', 'demand', 'deficit']) assert.equal(await group(key).locator(`[data-report-bar="${measure}"]`).getAttribute('data-value'), await dataRow(key).locator(`[data-report-comparison-cell="${measure}"]`).getAttribute('data-value'))
    assert.equal((await section().locator('[data-report-comparison-row] > th[scope="row"]').evaluateAll(headers => headers.map(header => header.childNodes[0].textContent))).filter(name => name === 'Civil').length, 2)
    assert.match(await group(firstKey).getAttribute('aria-label'), /capacity 40 h; planning demand 50 h; weekly shortfall 10 h/)
    const matrixRow = page.locator('table[data-report-table="team-capacity"] tbody tr').first()
    await page.waitForFunction(() => innerWidth === 1440 && innerHeight === 1000); await page.evaluate(async () => { await document.fonts.ready }); await page.locator('main#content').evaluate(main => main.scrollTo({ top: 0, left: 0, behavior: 'auto' }))
    await matrixRow.evaluate(async row => { let previous, stable = 0; for (let frame = 0; frame < 120; frame++) { await new Promise(resolve => requestAnimationFrame(resolve)); const rect = row.getBoundingClientRect(), current = JSON.stringify([innerWidth, innerHeight, document.querySelector('main#content').scrollTop, rect.x, rect.y, rect.width, rect.height]); stable = current === previous ? stable + 1 : 0; previous = current; if (stable >= 2) return } throw new Error('Original report matrix did not settle') })
    const rect = await matrixRow.boundingBox(); assert.ok(rect && rect.height > 0); geometry.push(rect); assert.equal(await page.locator('main#content').evaluate(main => main.scrollTop), 0); assert.ok(rect.y <= 405, `original matrix first data row ${rect.y}>405`)

    // UI focus never fetches again; pages contain only returned groups and leave the canonical source untouched.
    const readCount = runs.length; await section().getByRole('button', { name: 'Next disciplines', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('chartPage') === '2'); await section().getByText('Showing 13–14 of 14 returned disciplines.', { exact: true }).waitFor(); assert.equal(await section().getByRole('button', { name: 'Next disciplines', exact: true }).isDisabled(), true)
    await section().getByLabel('Comparison period', { exact: true }).selectOption(dates[4]); await page.waitForURL(url => url.searchParams.get('chartPeriod') === dates[4]); assert.equal(new URL(page.url()).searchParams.has('chartPage'), false)
    await section().getByRole('button', { name: 'Next disciplines', exact: true }).click(); await section().getByText('Showing 13–14 of 14 returned disciplines.', { exact: true }).waitFor(); assert.equal(runs.length, readCount)
    await page.goBack(); await section().getByText('Showing 1–12 of 14 returned disciplines.', { exact: true }).waitFor(); assert.equal(new URL(page.url()).searchParams.get('chartPeriod'), dates[4])
    await page.goForward(); await section().getByText('Showing 13–14 of 14 returned disciplines.', { exact: true }).waitFor(); assert.equal(runs.length, readCount)
    await openControls(); await page.getByRole('button', { name: 'Copy link', exact: true }).click(); const copied = new URL(await page.evaluate(() => window.__chartCopies.at(-1))); assert.deepEqual(Object.fromEntries(copied.searchParams), { ...initial, chartPeriod: dates[4], chartPage: '2' })
    await page.getByRole('region', { name: 'Views', exact: true }).getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click(); const save = page.getByRole('dialog', { name: 'Save this view', exact: true }); await save.getByLabel('Name', { exact: true }).fill('Synthetic chart data scope')
    const saved = watch(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/views'); await save.getByRole('button', { name: 'Save', exact: true }).click(); await finished(saved); await save.waitFor({ state: 'hidden' }); assert.deepEqual(saves.at(-1).params, initial)
    const exported = watch(response => new URL(response.url()).pathname.endsWith('/reports/team-capacity/export')); await page.getByRole('button', { name: 'Export', exact: true }).click(); await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click(); await finished(exported); assert.deepEqual(exports.at(-1), { ...initial, format: 'csv' })
    await goto(initial, { chartPeriod: dates[4], chartPage: '2' }); const origin = page.url(), metric = watch(response => new URL(response.url()).pathname.endsWith('/task-inputs/unestimated'))
    await page.locator('[data-report-task-input="unestimated"]').click(); await finished(metric); const inputs = page.getByRole('dialog', { name: 'Unestimated tasks', exact: true }); await inputs.getByRole('button', { name: `${task.key} ${task.name}`, exact: true }).waitFor(); assert.deepEqual(metrics.at(-1), { ...initial, page: '1', pageSize: '50' })
    assert.equal(new URL(page.url()).searchParams.get('chartPeriod'), dates[4]); assert.equal(new URL(page.url()).searchParams.get('chartPage'), '2'); const inputOrigin = page.url()
    const taskRead = watch(response => new URL(response.url()).pathname === `/api/v1/tasks/${task.id}`); await inputs.getByRole('button', { name: `${task.key} ${task.name}`, exact: true }).click(); await finished(taskRead); await page.getByRole('dialog').getByRole('heading', { name: task.name, exact: true }).waitFor(); await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(inputOrigin)
    await inputs.getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(origin); await page.waitForFunction(() => document.activeElement?.getAttribute('data-report-task-input') === 'unestimated'); await section().getByText('Showing 13–14 of 14 returned disciplines.', { exact: true }).waitFor()

    await openControls(); let read = reportRead({ unit: 'fte', period: 'weeks' }); await page.getByLabel('Report unit', { exact: true }).selectOption('fte'); await finished(read); assert.equal(new URL(page.url()).searchParams.has('chartPeriod'), false); assert.equal(new URL(page.url()).searchParams.has('chartPage'), false)
    read = reportRead({ unit: 'fte', period: 'months' }); await page.getByLabel('Period', { exact: true }).selectOption('months'); await finished(read); await group(firstKey).getByText('short by 10 h · 0.0625 FTE', { exact: false }).waitFor()
    assert.equal(await dataRow(firstKey).locator('[data-report-comparison-cell="capacity"]').getAttribute('data-value'), '1'); assert.equal(await dataRow(firstKey).locator('[data-report-comparison-cell="demand"]').getAttribute('data-value'), '1'); assert.equal(await dataRow(firstKey).locator('[data-report-comparison-cell="deficit"]').getAttribute('data-value'), '0.0625')
    const monthly = { ...initial, unit: 'fte', period: 'months' }; await goto(monthly, { chartPeriod: 'unknown', chartPage: '99999' }); await section().getByText('Chart focus was reset to an available period and returned page.', { exact: true }).waitFor(); assert.equal(await section().getByLabel('Comparison period', { exact: true }).inputValue(), '2026-11')
    mode = 'unknown'; await goto(monthly); await group(firstKey).getByText('Weekly shortfall unavailable', { exact: true }).waitFor(); assert.equal(await group(firstKey).locator('[data-report-bar="deficit"]').getAttribute('data-value'), null); assert.equal(await dataRow(firstKey).locator('[data-report-comparison-cell="deficit"]').getAttribute('data-value'), null)
    await dataRow(firstKey).getByText('Recorded provisional source hours: 156 h', { exact: true }).waitFor(); await section().getByLabel('Comparison period', { exact: true }).selectOption('2026-12'); await dataRow(firstKey).locator('[data-report-comparison-cell="demand"]').getByText('Unavailable', { exact: true }).waitFor()
    mode = 'complete'; standard = 0; await goto(monthly); await dataRow(firstKey).locator('[data-report-comparison-cell="deficit"]').getByText('FTE unavailable', { exact: true }).waitFor(); assert.equal(await group(firstKey).locator('[data-report-bar="deficit"]').getAttribute('data-value'), null); standard = 40
    mode = 'capped'; await goto(initial, { chartPage: '34' }); await section().getByText('Showing 397–400 of 400 returned disciplines.', { exact: true }).waitFor(); assert.equal(await section().getByRole('button', { name: 'Next disciplines', exact: true }).isDisabled(), true); await section().getByText('400 of 401 disciplines returned within the 2,000-row limit. Refine the report scope to see excluded disciplines.', { exact: true }).waitFor()
    mode = 'empty'; await goto(); await section().getByText('No disciplines in the authorised comparison scope.', { exact: true }).waitFor(); assert.equal(await section().locator('[data-report-capacity-chart]').count(), 0)
    mode = 'complete'; me.flags.visual_system = false; await goto(); await group(firstKey).getByText('short by 10 h', { exact: false }).waitFor(); assert.equal(await page.locator('details.report-controls').count(), 0)
    me.flags.visual_system = true; await page.setViewportSize({ width: 375, height: 812 }); await goto(); await group(firstKey).getByText('short by 10 h', { exact: false }).waitFor(); assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
    // Native type-ahead uses the already verified headless macOS path; OS-popup arrow keys do not commit there.
    const periodControl = section().getByLabel('Comparison period', { exact: true }); await periodControl.focus(); await page.keyboard.type(dates[1]); await page.waitForURL(url => url.searchParams.get('chartPeriod') === dates[1]); await group(firstKey).getByText('No recorded weekly shortfall', { exact: true }).waitFor()
    me.flags.capacity_screens = false; await page.setViewportSize({ width: 1440, height: 1000 }); const dark = { from: dates[0], weeks: '5', includeMyDrafts: 'false' }; await goto(dark, { chartPeriod: dates[4], chartPage: '2' }); assert.equal(await section().count(), 0); assert.equal(runs.at(-1).chartPeriod, undefined); assert.equal(runs.at(-1).chartPage, undefined)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: same supplied capacity/chart table values, actual10h deficit, monthly weekly-shortfall basis/FTE, identity/page/cap, canonical save/copy/export, Task origin, unknowns, original matrix geometry, phone/keyboard/off')
    console.log(JSON.stringify({ geometry }))
  } catch (error) { console.error(await page.locator('body').innerText(), JSON.stringify({ runs, metrics, saves, exports, geometry, errors, unknownGets: mock.unknownGets }, null, 2)); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
