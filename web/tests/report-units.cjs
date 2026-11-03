// S25 UI acceptance uses an actual8-week/two-month fixture. Exact source/permission behavior has separate API cases.
const assert = require('node:assert/strict')
const { newMockPage, me, project, links, comments } = require('./platform-fixtures.cjs')
const { taskRow, taskDetail, taskPermissions, taskPermission } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0009-${String(n).padStart(12, '0')}`
const civil = project.disciplines[0].disciplineId
const weeks = ['2026-11-02', '2026-11-09', '2026-11-16', '2026-11-23', '2026-11-30', '2026-12-07', '2026-12-14', '2026-12-21']
const param = (key, label, type, value = null, options = null) => ({ key, label, type, default: value, options })
const units = [param('unit', 'Report unit', 'select', 'hours', [{ value: 'hours', label: 'Hours' }, { value: 'fte', label: 'FTE' }]), param('period', 'Period', 'select', 'weeks', [{ value: 'weeks', label: 'Weeks' }, { value: 'months', label: 'Months' }])]
const capacity = [param('supervisorId', 'Supervisor', 'person'), param('disciplineId', 'Discipline', 'discipline'), param('from', 'From', 'date'), param('weeks', 'Weeks', 'number', '12'), param('includeMyDrafts', 'Include my private drafts', 'bool', 'true')]
const definitions = [
  { code: 'workload-employee', title: 'Workload by Employee', params: [param('supervisorId', 'Supervisor', 'person'), param('disciplineId', 'Discipline', 'discipline'), param('officeId', 'Office', 'office'), param('projectId', 'Project', 'project'), ...units] },
  { code: 'workload-discipline', title: 'Workload by Discipline', params: [param('officeId', 'Office', 'office'), ...units] },
  { code: 'team-capacity', title: 'Team Capacity', params: [...capacity, ...units] },
  { code: 'allocations-by-source', title: 'Allocations by Source', params: [...capacity, param('show', 'Show', 'select', 'hours-by-source', ['hours-by-source', 'entries-without-project'].map(value => ({ value, label: value }))), param('source', 'Source', 'select', null, [{ value: 'Admin', label: 'Admin' }]), ...units] },
].map(def => ({ ...def, description: 'Current permitted report values in the selected scope.', question: 'Who has room in the coming weeks?', columns: [], itemType: null }))
const unestimated = taskRow({ id: id(10), projectId: project.id, key: 'T-010', name: 'Synthetic missing report inputs', assigneeId: me.id, assigneeName: me.displayName })
const col = (path, header, type = 'text') => ({ path, header, type })
const labels = ['', 'Available', 'Confirmed', 'Proposed', 'Committed']
const basis = code => code.startsWith('workload-') ? 'WorkloadPeopleContext' : 'PlannerPeopleContext'
const UI_KEYS = ['taskInput', 'inputPage', 'inputPageSize', 'catalogue', 'viewProject', 'view', 'panel']
const canonical = (code, query) => {
  const value = { ...query }
  if (code === 'team-capacity' || code === 'allocations-by-source') { value.from ??= '2026-11-02'; value.weeks ??= '12'; value.includeMyDrafts ??= 'true' }
  if (code === 'allocations-by-source') value.show ??= 'hours-by-source'
  if (value.show !== 'entries-without-project') { value.unit ??= 'hours'; value.period ??= 'weeks' } else { delete value.unit; delete value.period }
  return value
}
const calendarWeeks = (code, query) => code.startsWith('workload-') ? weeks : Array.from({ length: Number(query.weeks) }, (_, index) => {
  const day = new Date(`${query.from}T00:00:00Z`); day.setUTCDate(day.getUTCDate() + index * 7); return day.toISOString().slice(0, 10)
})

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const runs = [], metricReads = [], writes = [], exports = [], errors = [], geometry = [], expectedErrors = new Set()
  let standard = 40, missing = false, precise = false, provisional = false, stored = [], lastResponse = null
  const watch = predicate => { const pending = page.waitForResponse(predicate); void pending.catch(() => {}); return pending }
  const read = (code, expected, metric, status = 200) => watch(response => {
    const url = new URL(response.url())
    return response.request().method() === 'GET' && response.status() === status && url.pathname === `/api/v1/reports/${code}${metric ? `/task-inputs/${metric}` : ''}` && JSON.stringify(Object.entries(Object.fromEntries(url.searchParams)).sort()) === JSON.stringify(Object.entries(expected).sort())
  })
  const finish = async response => { assert.equal(await (await response).finished(), null) }
  const table = code => page.locator(`table[data-report-table="${code}"]`)
  const controls = () => page.locator('details.report-controls')
  const openControls = async () => { if (await controls().count() && (await controls().getAttribute('open')) === null) await controls().locator('summary').click(); await page.getByRole('form', { name: 'Report parameters', exact: true }).waitFor() }
  const goto = async (code, query = {}, expected = query) => { const pending = read(code, expected); await page.goto(`${base}/reports/${code}?${new URLSearchParams(query)}`); await finish(pending); await table(code).locator('tbody tr').first().waitFor() }
  const quantity = (row, path) => row.locator(`[data-report-unit-cell="${path}"]`)
  const measure = async code => {
    await page.waitForFunction(() => innerWidth === 1440 && innerHeight === 1000)
    await page.evaluate(async () => { await document.fonts.ready })
    await page.locator('main#content').evaluate(main => main.scrollTo({ top: 0, left: 0, behavior: 'auto' }))
    await table(code).locator('tbody tr').first().evaluate(async row => {
      let previous, stable = 0
      for (let frame = 0; frame < 120; frame++) {
        await new Promise(resolve => requestAnimationFrame(resolve)); const rect = row.getBoundingClientRect()
        const sample = JSON.stringify([innerWidth, innerHeight, document.querySelector('main#content').scrollTop, rect.x, rect.y, rect.width, rect.height])
        stable = sample === previous ? stable + 1 : 0; previous = sample; if (stable >= 2) return
      }
      throw new Error('Report units layout did not settle')
    })
    const rect = await table(code).locator('tbody tr').first().boundingBox(); assert.ok(rect && rect.height > 0)
    assert.equal(await page.locator('main#content').evaluate(main => main.scrollTop), 0)
    geometry.push({ code, top: rect.y, height: rect.height }); assert.ok(rect.y <= 405, `${code} actual first data row top ${rect.y} exceeds405`)
  }
  const make = (code, parameters) => {
    const dates = calendarWeeks(code, parameters), periods = []
    dates.forEach(day => {
      const thursday = new Date(`${day}T00:00:00Z`); thursday.setUTCDate(thursday.getUTCDate() + 3)
      const key = parameters.period === 'months' ? thursday.toISOString().slice(0, 7) : day
      let period = periods.find(period => period.key === key)
      if (!period) { period = { key, label: key, weeks: [], weekCount: 0, denominatorHours: null }; periods.push(period) }
      period.weeks.push(day); period.weekCount++; period.denominatorHours = standard > 0 ? period.weekCount * standard : null
    })
    const matrix = parameters.show !== 'entries-without-project'
    let rows, columns, source, suffixes = ['']
    const demand = (n, index) => code.startsWith('workload-') && index >= 4 ? 0 : n
    if (code === 'workload-employee') {
      suffixes = labels
      columns = [col('person', 'Person'), col('supervisor', 'Supervisor'), col('capacity', 'Capacity (h/week)', 'number'), col('partialScope', 'Scope'), ...dates.flatMap((day, i) => suffixes.map(suffix => col(`w${i}${suffix}`, `Week of ${day}${suffix ? ` · ${suffix}` : ''}`, 'number'))), col('noDueDate', 'No due date (h)', 'number'), col('openTasks', 'Open tasks', 'number'), col('unestimated', 'Unestimated', 'number')]
      rows = [40, 32].map((capacity, index) => ({ person: index === 0 ? 'Synthetic Alex' : 'Synthetic Blair', supervisor: 'Synthetic supervisor', capacity, partialScope: 'Visible work only', noDueDate: index === 0 && missing ? 8 : 0, openTasks: 1, unestimated: index === 0 && missing ? 1 : 0,
        ...Object.fromEntries(dates.flatMap((_, i) => suffixes.map(suffix => [`w${i}${suffix}`, suffix === 'Available' ? capacity - (provisional && index === 0 && i === 0 ? 4 : 0) : ['Confirmed', 'Proposed'].includes(suffix) ? 0 : demand(index === 0 ? 40 : 30, i)]))) }))
      source = rows.map((_, rowIndex) => ({ rowIndex, kind: 'Hours', values: Object.fromEntries(suffixes.map(suffix => [suffix, dates.map((_, i) => rows[rowIndex][`w${i}${suffix}`])])) }))
    } else if (code === 'workload-discipline') {
      columns = [col('discipline', 'Discipline'), ...dates.map((day, i) => col(`w${i}`, `Week of ${day}`, 'number')), col('noDueDate', 'No due date (h)', 'number'), col('openTasks', 'Open tasks', 'number'), col('unestimated', 'Unestimated', 'number'), col('people', 'People', 'number')]
      rows = ['Civil', 'Electrical'].map((discipline, index) => ({ discipline, noDueDate: 0, openTasks: 1, unestimated: index === 0 && missing ? 1 : 0, people: 1, ...Object.fromEntries(dates.map((_, i) => [`w${i}`, demand(index === 0 ? 40 : 30, i)])) }))
      source = rows.map((_, rowIndex) => ({ rowIndex, kind: 'Hours', values: { '': dates.map((_, i) => rows[rowIndex][`w${i}`]) } }))
    } else if (!matrix) {
      columns = [col('personName', 'Person'), col('ownerName', 'Owner'), col('label', 'Label'), col('source', 'Source'), col('hoursPerWeek', 'Hours', 'number'), col('startWeek', 'Start', 'date'), col('endWeek', 'End', 'date'), col('confidence', 'Confidence'), col('visibility', 'Visibility')]
      rows = [{ personName: 'Synthetic Alex', ownerName: 'Synthetic supervisor', label: 'Synthetic unlinked entry', source: 'Admin', hoursPerWeek: 8, startWeek: dates[0], endWeek: dates.at(-1), confidence: 'Confirmed', visibility: 'Published' }]; source = []
    } else {
      const measures = code === 'team-capacity' ? ['Capacity', 'Time away', 'Confirmed', 'Expected', 'Possible', 'Remaining', 'Over planned people', 'Under planned people', 'Partial people'] : ['Admin', 'Approved', 'Total']
      const values = code === 'team-capacity' ? [72, 0, 70, 0, 0, 2, 0, 0, 0] : parameters.from === '2026-11-30' && parameters.weeks === '2' ? [40, 0, 40] : [70, 0, 70]
      columns = [col('measure', 'Measure'), ...dates.map((day, i) => col(`w${i}`, day, 'number'))]
      rows = measures.map((measure, index) => ({ measure, ...Object.fromEntries(dates.map((_, i) => [`w${i}`, provisional && code === 'team-capacity' && i === 0 && [0, 1, 5].includes(index) ? ({ 0: 68, 1: 4, 5: -2 })[index] : values[index]])) }))
      source = rows.map((_, rowIndex) => ({ rowIndex, kind: code === 'team-capacity' && rowIndex >= 6 ? 'Count' : 'Hours', values: { '': dates.map((_, i) => rows[rowIndex][`w${i}`]) } }))
    }
    if (code.startsWith('workload-')) source.push({ rowIndex: -1, kind: 'Total', values: Object.fromEntries(suffixes.map(suffix => [suffix, dates.map((_, i) => source.reduce((sum, row) => sum + row.values[suffix][i], 0))])) })
    const separate = code === 'team-capacity' && (parameters.unit !== 'hours' || parameters.period !== 'weeks')
    const originalPaths = new Set(dates.flatMap((_, i) => suffixes.map(suffix => `w${i}${suffix}`))), first = columns.findIndex(column => originalPaths.has(column.path))
    const path = (periodIndex, suffix) => parameters.unit === 'hours' && parameters.period === 'weeks' ? `w${periodIndex}${suffix}` : `u${periodIndex}${suffix}`
    const selectedColumns = matrix ? [...columns.slice(0, first), ...periods.flatMap((period, p) => suffixes.map(suffix => col(path(p, suffix), `${period.label} · ${suffix || 'Estimated work'} (${parameters.unit === 'fte' ? 'FTE' : 'h'}; ISO weeks: ${period.weekCount})`, 'number'))),
      ...(separate ? dates.map((day, i) => col(`countW${i}`, `${day} · weekly people count`, 'number')) : []), ...columns.slice(first).filter(column => !originalPaths.has(column.path))] : columns
    const typedRows = source.map(row => {
      const cells = periods.flatMap((period, p) => suffixes.map(suffix => {
        const knownHours = period.weeks.reduce((sum, week) => sum + row.values[suffix][dates.indexOf(week)], 0)
        const unavailable = missing && code.startsWith('workload-') && [0, -1].includes(row.rowIndex) && ['', 'Committed'].includes(suffix)
        const uncertain = provisional && (code === 'workload-employee' && [0, -1].includes(row.rowIndex) && suffix === 'Available' || code === 'team-capacity' && [0, 1, 5, 6, 7].includes(row.rowIndex)) && (row.rowIndex === 7 || period.weeks.includes(dates[0]))
        if (row.kind === 'Count') return { path: path(p, suffix), kind: separate ? 'NotApplicable' : 'Count', value: separate || uncertain ? null : knownHours, knownHours: null, missingInputs: 0, coverage: separate ? 'NotApplicable' : uncertain ? 'ProvisionalAvailability' : row.rowIndex < 8 ? 'AvailabilityDependentCount' : 'VisibleOnly' }
        return { path: path(p, suffix), kind: 'Hours', value: unavailable || uncertain || (parameters.unit === 'fte' && standard <= 0) ? null : parameters.unit === 'fte' ? knownHours / (period.weekCount * standard) : knownHours,
          knownHours, missingInputs: unavailable ? 1 : 0, coverage: uncertain ? 'ProvisionalAvailability' : unavailable ? 'KnownSubtotal' : parameters.unit === 'fte' && standard <= 0 ? 'StandardUnavailable' : 'VisibleOnly' }
      }))
      if (separate) dates.forEach((_, i) => {
        const uncertain = provisional && (row.rowIndex === 6 && i === 0 || row.rowIndex === 7)
        cells.push({ path: `countW${i}`, kind: row.kind === 'Count' ? 'Count' : 'NotApplicable', value: row.kind === 'Count' && !uncertain ? row.values[''][i] : null, knownHours: null, missingInputs: 0, coverage: row.kind !== 'Count' ? 'NotApplicable' : uncertain ? 'ProvisionalAvailability' : row.rowIndex < 8 ? 'AvailabilityDependentCount' : 'VisibleOnly' })
      })
      if (code === 'workload-employee' && row.rowIndex >= 0) cells.push({ path: 'capacity', kind: 'WeeklyRate', value: rows[row.rowIndex].capacity, knownHours: null, missingInputs: 0, coverage: 'VisibleOnly' }, { path: 'noDueDate', kind: 'UndatedHours', value: row.rowIndex === 0 && missing ? null : 0, knownHours: row.rowIndex === 0 && missing ? 8 : 0, missingInputs: row.rowIndex === 0 && missing ? 1 : 0, coverage: row.rowIndex === 0 && missing ? 'KnownSubtotal' : 'VisibleOnly' })
      return { rowIndex: row.rowIndex, kind: row.kind, cells }
    })
    if (precise && code === 'workload-employee') {
      // An exact supplied DTO distinct from the rounded legacy wire: the browser must render it, not recalculate from0.
      rows.forEach(row => dates.forEach((_, i) => { row[`w${i}`] = 0; row[`w${i}Committed`] = 0 }))
      for (const row of typedRows) for (const cell of row.cells) if (cell.kind === 'Hours' && /^(u\d+|w\d+)(Committed)?$/.test(cell.path)) {
        const firstPeriod = /^u0(Committed)?$/.test(cell.path), hasSource = row.rowIndex === 0 || row.rowIndex === -1
        cell.value = firstPeriod && hasSource ? .0008125 : 0; cell.knownHours = firstPeriod && hasSource ? .13 : 0
      }
    }
    const inputs = { basis: basis(code), scopeCoverage: 'VisibleOnly', unavailableReferences: 0, parameters, asOf: '2026-11-02T13:00:00Z' }
    for (const [property, metric] of [['unestimated', 'unestimated'], ['openUndated', 'open-undated']]) inputs[property] = { count: missing ? 1 : 0, knownCount: missing ? 1 : 0, href: `/reports/${code}?${new URLSearchParams({ ...parameters, taskInput: metric, inputPage: '1', inputPageSize: '50' })}` }
    return { code, title: definitions.find(def => def.code === code).title, itemType: null, rows, columns, total: rows.length, totalIsLowerBound: false, truncated: false, listLink: null, savedParameters: parameters, taskInputs: inputs,
      parameters: Object.entries(parameters).map(([key, value]) => ({ label: definitions.find(def => def.code === code).params.find(param => param.key === key)?.label ?? key, value: key === 'projectId' ? project.projectNumber : key === 'disciplineId' ? 'Civil' : value })), asOf: '2026-11-02T13:00:00Z',
      ...(matrix ? { unitView: { unit: parameters.unit, period: parameters.period, standardWeeklyHours: standard, displayedWeekCount: dates.length, periods, columns: selectedColumns, rows: typedRows, scopeCoverage: 'VisibleOnly', unestimatedTasks: missing ? 1 : 0 } } : {}) }
  }
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error' && !expectedErrors.has(message.location().url)) errors.push(message.text()) })
    await page.addInitScript(() => { window.__unitCopies = []; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async value => window.__unitCopies.push(value) } }) })
    me.flags = { ...me.flags, capacity_screens: true, capacity_reports: true, visual_system: true, home_overview: false, screen_patterns: false, capacity_polish: false, work_screens: false, small_ux: false, live_events: false }; me.settings.today = '2026-11-02'
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')] }) }))
    await page.route('**/api/v1/reports**', route => {
      const request = route.request(), url = new URL(request.url()), parts = url.pathname.split('/'), code = parts[4], query = Object.fromEntries(url.searchParams)
      assert.equal(request.method(), 'GET')
      if (!code) return route.fulfill({ json: definitions.filter(def => me.flags.capacity_reports || !['team-capacity', 'allocations-by-source'].includes(def.code)).map(def => ({ ...def, params: me.flags.capacity_screens ? def.params : def.params.filter(param => !['unit', 'period'].includes(param.key)) })) })
      UI_KEYS.forEach(key => assert.equal(query[key], undefined, `${key} is not a report data parameter`))
      const selected = canonical(code, query)
      if (parts[5] === 'task-inputs') {
        metricReads.push({ code, query }); const { page: currentPage, pageSize, ...parameters } = query
        assert.deepEqual(parameters, canonical(code, parameters))
        return route.fulfill({ json: { code, metric: parts[6], basis: basis(code), scopeCoverage: 'VisibleOnly', unavailableReferences: 0, total: missing ? 1 : 0, knownTotal: missing ? 1 : 0, page: Number(currentPage), pageSize: Number(pageSize), items: missing ? [unestimated] : [], parameters, asOf: '2026-11-02T13:05:00Z' } })
      }
      if (query.show === 'entries-without-project' && ((query.unit && query.unit !== 'hours') || (query.period && query.period !== 'weeks'))) { expectedErrors.add(request.url()); return route.fulfill({ status: 400, json: { title: 'Synthetic incompatible report unit', detail: 'Synthetic incompatible report unit', status: 400 } }) }
      if (parts[5] === 'export') { exports.push({ code, query }); return route.fulfill({ contentType: 'text/csv', body: 'Standard full-time hours per week,40\nDisplayed ISO weeks,8\nReport period,Months\n\nPerson,November FTE\nSynthetic Alex,1.0\nSynthetic Blair,0.75\nVisible report total,1.75\n' }) }
      runs.push({ code, query }); lastResponse = make(code, selected)
      if (!me.flags.capacity_screens) { delete lastResponse.unitView; delete lastResponse.taskInputs; delete lastResponse.savedParameters; lastResponse.parameters = lastResponse.parameters.filter(parameter => !['Report unit', 'Period'].includes(parameter.label)) }
      return route.fulfill({ json: lastResponse })
    })
    await page.route('**/api/v1/views**', route => {
      const request = route.request(), url = new URL(request.url())
      if (request.method() === 'GET') return route.fulfill({ json: viewList(stored.filter(view => view.listType === url.searchParams.get('listType'))) })
      assert.equal(request.method(), 'POST'); const body = request.postDataJSON(); writes.push(body); stored.push({ ...savedView({ id: id(900), name: body.name, params: body.params }), listType: body.listType }); return route.fulfill({ status: 201, json: viewSaved(id(900)) })
    })
    await page.route('**/api/v1/tasks/**', route => route.fulfill({ json: route.request().url().endsWith('/dependencies') ? taskDependencies() : taskDetail({ task: unestimated, permissions: taskPermissions({ edit: taskPermission(false), assign: taskPermission(false), setReviewer: taskPermission(false), dueDate: taskPermission(false), block: taskPermission(false), delete: taskPermission(false), comment: false, dependencies: false, enterTime: false }) }) }))
    await page.route('**/api/v1/items/Task/**', route => route.fulfill({ json: route.request().url().endsWith('/comments') ? comments : links }))

    const scope = { projectId: project.id }, fteMonth = { ...scope, unit: 'fte', period: 'months' }
    await goto('workload-employee', fteMonth)
    const employee = table('workload-employee'), alex = employee.locator('tbody tr').nth(0), blair = employee.locator('tbody tr').nth(1)
    await quantity(alex, 'u0').getByText('1.0', { exact: true }).waitFor(); await quantity(blair, 'u0').getByText('0.75', { exact: true }).waitFor()
    await quantity(employee.locator('tfoot'), 'u0').getByText('1.75', { exact: true }).waitFor(); assert.equal(await quantity(blair, 'capacity').innerText(), '32')
    assert.equal(lastResponse.unitView.displayedWeekCount, 8); assert.deepEqual(lastResponse.unitView.periods.map(period => [period.key, period.weekCount, period.denominatorHours]), [['2026-11', 4, 160], ['2026-12', 4, 160]])
    const explanation = page.locator('[data-report-unit-basis]'); await explanation.getByText('2026-11: 4 displayed weeks; denominator 160 h', { exact: true }).waitFor()
    await explanation.getByText('Displayed ISO weeks: 8', { exact: false }).waitFor()
    await measure('workload-employee')

    // Actual controls retain filters, exact8-week horizon, and URL choices; Hours and Months is not a summed FTE label.
    await openControls(); const hourRead = read('workload-employee', { ...scope, unit: 'hours', period: 'months' })
    await page.getByLabel('Report unit', { exact: true }).selectOption('hours'); await finish(hourRead); await quantity(alex, 'u0').getByText('160', { exact: true }).waitFor(); await quantity(employee.locator('tfoot'), 'u0').getByText('280', { exact: true }).waitFor()
    const weekRead = read('workload-employee', { ...scope, unit: 'hours', period: 'weeks' })
    await page.getByLabel('Period', { exact: true }).selectOption('weeks'); await finish(weekRead); await quantity(alex, 'w0').getByText('40', { exact: true }).waitFor(); assert.equal(lastResponse.unitView.periods.length, 8)
    await goto('workload-employee', fteMonth); await openControls()
    await page.getByRole('button', { name: 'Copy link', exact: true }).click(); const copied = new URL(await page.evaluate(() => window.__unitCopies.at(-1))); assert.deepEqual(Object.fromEntries(copied.searchParams), fteMonth)
    await page.getByRole('region', { name: 'Views', exact: true }).getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    const save = page.getByRole('dialog', { name: 'Save this view', exact: true }); await save.getByLabel('Name', { exact: true }).fill('Synthetic November standard FTE')
    const saved = watch(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/views'); await save.getByRole('button', { name: 'Save', exact: true }).click(); await finish(saved); await save.waitFor({ state: 'hidden' })
    assert.deepEqual(writes.at(-1).params, fteMonth)
    const exported = watch(response => new URL(response.url()).pathname === '/api/v1/reports/workload-employee/export'); await page.getByRole('button', { name: 'Export', exact: true }).click(); await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click(); await finish(exported); assert.deepEqual(exports.at(-1).query, { ...fteMonth, format: 'csv' })
    await goto('workload-employee', Object.fromEntries(copied.searchParams)); await quantity(alex, 'u0').getByText('1.0', { exact: true }).waitFor(); stored = []

    // Every actual eligible matrix gets its own authoritative typed view. Weekly people counts retain counts beside month hours.
    for (const code of ['workload-discipline', 'team-capacity', 'allocations-by-source']) {
      const parameters = code === 'workload-discipline' ? { unit: 'fte', period: 'months' } : { from: '2026-11-02', weeks: '4', includeMyDrafts: 'false', unit: 'fte', period: 'months', ...(code === 'allocations-by-source' ? { show: 'hours-by-source', source: 'Admin' } : {}) }
      await goto(code, parameters); await table(code).locator('[data-report-unit-cell="u0"]').first().waitFor(); assert.deepEqual(lastResponse.taskInputs.parameters, canonical(code, parameters)); await measure(code)
      if (code === 'team-capacity') {
        assert.equal(await table(code).getByRole('columnheader', { name: /weekly people count/ }).count(), 4)
        assert.equal(await table(code).locator('tbody tr').nth(6).locator('[data-report-unit-cell="u0"]').count(), 0)
        const noMonthQuantity = table(code).locator('tbody tr').nth(6).locator('[data-report-unit-not-applicable="u0"]')
        assert.equal(await noMonthQuantity.locator('.sr-only').textContent(), 'Not applicable', 'structural month/count cells are not announced as unavailable data')
        await quantity(table(code).locator('tbody tr').nth(6), 'countW0').getByText('0', { exact: true }).waitFor(); assert.equal(await table(code).locator('tbody tr').nth(0).locator('[data-report-unit-cell="countW0"]').count(), 0)
        assert.equal(await table(code).locator('tbody tr').nth(0).locator('[data-report-unit-not-applicable="countW0"] .sr-only').textContent(), 'Not applicable')
      }
    }
    await goto('allocations-by-source', { from: '2026-11-30', weeks: '2', includeMyDrafts: 'false', show: 'hours-by-source', unit: 'fte', period: 'months' })
    assert.deepEqual(lastResponse.unitView.periods.map(period => [period.key, period.weekCount, period.denominatorHours]), [['2026-12', 2, 80]])
    await quantity(table('allocations-by-source').locator('tbody tr').first(), 'u0').getByText('1.0', { exact: true }).waitFor()

    // Changing to the fixed-rate entry form clears only irrelevant selectors and keeps every real scope parameter.
    await openControls(); const entriesQuery = { from: '2026-11-30', weeks: '2', includeMyDrafts: 'false', show: 'entries-without-project' }, entries = read('allocations-by-source', entriesQuery)
    await page.getByLabel('Show', { exact: true }).selectOption('entries-without-project'); await finish(entries); await table('allocations-by-source').getByRole('cell', { name: 'Synthetic unlinked entry', exact: true }).waitFor()
    assert.equal(await page.getByLabel('Report unit', { exact: true }).count(), 0); assert.equal(await page.getByLabel('Period', { exact: true }).count(), 0); assert.equal(await page.locator('[data-report-unit-basis], [data-report-unit-cell]').count(), 0)
    assert.equal(new URL(page.url()).searchParams.has('unit'), false); assert.equal(new URL(page.url()).searchParams.has('period'), false); assert.equal(await table('allocations-by-source').getByRole('cell', { name: '8', exact: true }).count(), 1)
    const incompatible = read('allocations-by-source', { ...entriesQuery, unit: 'fte' }, undefined, 400); await page.goto(`${base}/reports/allocations-by-source?${new URLSearchParams({ ...entriesQuery, unit: 'fte' })}`); await finish(incompatible); await page.getByRole('alert').filter({ hasText: 'Synthetic incompatible report unit' }).waitFor(); assert.equal(await page.locator('[data-report-unit-basis], [data-report-unit-cell]').count(), 0)

    precise = true; await goto('workload-employee', fteMonth); await quantity(alex, 'u0').getByText('0.0008', { exact: true }).waitFor()
    assert.equal(lastResponse.rows[0].w0, 0); assert.equal(Number(await quantity(alex, 'u0').getAttribute('data-unit-value')), .0008125); precise = false

    // Provisional availability never becomes verified FTE. Its recorded hours and weekly indicator qualifications remain visible.
    provisional = true; await goto('workload-employee', fteMonth)
    await quantity(alex, 'u0Available').getByText('Availability provisional', { exact: true }).waitFor(); await quantity(alex, 'u0Available').getByText('Recorded provisional source hours: 156 h', { exact: true }).waitFor()
    assert.equal(await quantity(alex, 'u0Available').getAttribute('data-unit-value'), null); assert.equal(await quantity(employee.locator('tfoot'), 'u0Available').getAttribute('data-unit-coverage'), 'ProvisionalAvailability')
    await quantity(alex, 'u1Available').getByText('1.0', { exact: true }).waitFor(); await quantity(alex, 'u0').getByText('1.0', { exact: true }).waitFor(); assert.equal(lastResponse.rows[0].w0Available, 36)
    await goto('team-capacity', { from: '2026-11-02', weeks: '4', includeMyDrafts: 'false', unit: 'fte', period: 'months' })
    const teamRows = table('team-capacity').locator('tbody tr')
    await quantity(teamRows.nth(0), 'u0').getByText('Recorded provisional source hours: 284 h', { exact: true }).waitFor(); await quantity(teamRows.nth(1), 'u0').getByText('Recorded provisional source hours: 4 h', { exact: true }).waitFor()
    for (const index of [0, 1, 5]) { await quantity(teamRows.nth(index), 'u0').getByText('Availability provisional', { exact: true }).waitFor(); assert.equal(await quantity(teamRows.nth(index), 'u0').getAttribute('data-unit-value'), null) }
    await quantity(teamRows.nth(6), 'countW0').getByText('Availability provisional', { exact: true }).waitFor(); assert.equal(await quantity(teamRows.nth(6), 'countW0').getAttribute('data-unit-value'), null)
    await quantity(teamRows.nth(6), 'countW1').getByText('0', { exact: true }).waitFor(); await quantity(teamRows.nth(6), 'countW1').getByText('Recorded indicator; availability coverage is not assessed', { exact: true }).waitFor(); provisional = false

    // Null totals preserve known source hours; changing a unit never changes S23's task cohort or source-return display choices.
    missing = true; await goto('workload-employee', fteMonth); await quantity(alex, 'u0').getByText('Known source hours: 160 h', { exact: true }).waitFor(); assert.equal(await quantity(alex, 'u0').getAttribute('data-unit-value'), null)
    await quantity(alex, 'noDueDate').getByText('Known source hours: 8 h', { exact: true }).waitFor(); assert.equal(await quantity(alex, 'noDueDate').getAttribute('data-unit-value'), null)
    const input = read('workload-employee', { ...fteMonth, page: '1', pageSize: '50' }, 'unestimated'), origin = page.url()
    await page.locator('[data-report-task-input="unestimated"]').click(); await finish(input); const inputs = page.getByRole('dialog', { name: 'Unestimated tasks', exact: true }); await inputs.getByRole('button', { name: `${unestimated.key} ${unestimated.name}`, exact: true }).waitFor()
    assert.deepEqual(metricReads.at(-1).query, { ...fteMonth, page: '1', pageSize: '50' }); const inputOrigin = page.url()
    const taskRead = watch(response => new URL(response.url()).pathname === `/api/v1/tasks/${unestimated.id}`); await inputs.getByRole('button', { name: `${unestimated.key} ${unestimated.name}`, exact: true }).click(); await finish(taskRead)
    await page.getByRole('dialog').getByRole('heading', { name: unestimated.name, exact: true }).waitFor(); await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(inputOrigin)
    await inputs.getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(origin); await page.waitForFunction(() => document.activeElement?.getAttribute('data-report-task-input') === 'unestimated')
    assert.equal(new URL(page.url()).searchParams.get('unit'), 'fte'); assert.equal(new URL(page.url()).searchParams.get('period'), 'months')
    missing = false; standard = 0; me.settings.defaultWeeklyCapacityHours = 0; await goto('workload-employee', fteMonth)
    await quantity(alex, 'u0').getByText('FTE unavailable', { exact: true }).waitFor(); await quantity(alex, 'u0').getByText('Known source hours: 160 h', { exact: true }).waitFor(); assert.equal(await quantity(alex, 'u0').getAttribute('data-unit-value'), null)
    standard = 40; me.settings.defaultWeeklyCapacityHours = 40; me.flags.visual_system = false; await goto('workload-employee', fteMonth)
    await page.getByLabel('Report unit', { exact: true }).waitFor(); await quantity(blair, 'u0').getByText('0.75', { exact: true }).waitFor(); assert.equal(await controls().count(), 0)
    me.flags.visual_system = true; await page.setViewportSize({ width: 390, height: 844 }); await goto('workload-employee', fteMonth); await quantity(alex, 'u0').getByText('1.0', { exact: true }).waitFor()
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false); await openControls(); await page.getByLabel('Report unit', { exact: true }).waitFor()
    me.flags.capacity_screens = false; await page.setViewportSize({ width: 1440, height: 1000 }); const off = read('workload-employee', scope)
    await page.goto(`${base}/reports/workload-employee?${new URLSearchParams(scope)}`); await finish(off); await page.getByLabel('Project', { exact: true }).waitFor()
    assert.equal(await page.getByLabel('Report unit', { exact: true }).count(), 0); assert.equal(await page.getByLabel('Period', { exact: true }).count(), 0); assert.equal(await page.locator('[data-report-unit-basis], [data-report-unit-cell], [data-report-unit-total]').count(), 0)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: exact supplied four-week November FTE inside8-week Workload, all four matrix forms, typed precision/provisional and undated evidence, context/count units, canonical unit/period save/copy/export, unknown/zero denominator, S23 source return, phone/off')
    console.log(JSON.stringify({ geometry }))
  } catch (error) { console.error(await page.locator('body').innerText(), JSON.stringify({ runs, metricReads, writes, exports, geometry, unknownGets: mock.unknownGets, errors }, null, 2)); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
