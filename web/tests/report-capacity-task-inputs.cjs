// Remaining S23-B UI acceptance, synthetic only. Real source/permissions/historical membership have API coverage.
const assert = require('node:assert/strict')
const { newMockPage, me, project, links, comments } = require('./platform-fixtures.cjs')
const { taskRow, taskDetail, taskPermissions, taskPermission } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { referenceData, referenceDiscipline, referenceOffice } = require('./me.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0008-${String(n).padStart(12, '0')}`
const civil = project.disciplines[0].disciplineId, office = id(200), unit = id(201), supervisor = id(202)
const param = (key, label, type, value = null, options = null) => ({ key, label, type, default: value, options })
const capacityParams = [param('supervisorId', 'Supervisor', 'person'), param('disciplineId', 'Discipline', 'discipline'), param('from', 'From', 'date'), param('weeks', 'Weeks', 'number', '12'), param('includeMyDrafts', 'Include my private drafts', 'bool', 'true')]
const definitions = [
  { code: 'workload-employee', title: 'Workload by Employee', params: [param('supervisorId', 'Supervisor', 'person'), param('disciplineId', 'Discipline', 'discipline'), param('officeId', 'Office', 'office'), param('projectId', 'Project', 'project')] },
  { code: 'workload-discipline', title: 'Workload by Discipline', params: [param('officeId', 'Office', 'office')] },
  { code: 'team-capacity', title: 'Team Capacity', params: [...capacityParams, param('unitId', 'Unit', 'select', null, [{ value: unit, label: 'Synthetic historical unit' }])] },
  { code: 'allocations-by-source', title: 'Allocations by Source', params: [...capacityParams, param('show', 'Show', 'select', 'hours-by-source', ['hours-by-source', 'entries-without-project'].map(value => ({ value, label: value }))), param('source', 'Source', 'select', null, [{ value: 'Admin', label: 'Admin' }]), param('confidence', 'Confidence', 'select', null, [{ value: 'Confirmed', label: 'Confirmed' }])] },
  { code: 'task-hours', title: 'Task Hours', params: [param('projectId', 'Project', 'project'), param('userId', 'Person', 'person'), param('from', 'From', 'date'), param('to', 'To', 'date'), param('scope', 'Entries', 'select', 'mine', ['mine', 'team'].map(value => ({ value, label: value })))] },
].map(def => ({ ...def, description: 'Current permitted report values in the selected scope.', question: 'Which work needs attention?', itemType: null, columns: [] }))
const tasks = [
  taskRow({ id: id(1), projectId: project.id, key: 'T-001', name: 'Synthetic shared report input' }),
  ...[2, 3, 4].map(n => taskRow({ id: id(n), projectId: project.id, key: `T-00${n}`, name: `Synthetic dated unestimated input ${n}`, dueDate: '2026-09-16' })),
  taskRow({ id: id(5), projectId: project.id, key: 'T-005', name: 'Synthetic zero-estimate undated input', estimatedHours: 0 }),
]
const col = (path, header, type = 'text') => ({ path, header, type })
const weeks = ['2026-09-14', '2026-09-21', '2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26', '2026-11-02']
const employeeColumns = [col('person', 'Person'), col('supervisor', 'Supervisor'), col('capacity', 'Capacity', 'number'), col('partialScope', 'Scope'),
  ...weeks.flatMap((week, i) => [col(`w${i}`, `Week of ${week}`, 'number'), ...['Available', 'Confirmed', 'Proposed', 'Committed'].map(field => col(`w${i}${field}`, `Week of ${week} · ${field}`, 'number'))]),
  ...[['noDueDate', 'No due date'], ['openTasks', 'Open tasks'], ['unestimated', 'Unestimated'], ['overdue', 'Overdue tasks'], ['projects', 'Projects']].map(([key, label]) => col(key, label, 'number')), col('indicator', 'Indicator')]
const reportData = (code, parameters) => {
  if (code === 'workload-employee') return { columns: employeeColumns, rows: [{ person: me.displayName, supervisor: 'Synthetic supervisor', capacity: 40, partialScope: 'Complete visible scope',
    ...Object.fromEntries(weeks.flatMap((_, i) => [[`w${i}`, 0], [`w${i}Available`, 40], [`w${i}Confirmed`, 0], [`w${i}Proposed`, 0], [`w${i}Committed`, 0]])), noDueDate: 0, openTasks: 5, unestimated: 4, overdue: 0, projects: 1, indicator: 'Recorded report value' }] }
  if (code === 'workload-discipline') return { columns: [col('discipline', 'Discipline'), ...weeks.map((week, i) => col(`w${i}`, `Week of ${week}`, 'number')), ...['noDueDate', 'openTasks', 'unestimated', 'people'].map(field => col(field, field, 'number'))],
    rows: [{ discipline: 'Civil', ...Object.fromEntries(weeks.map((_, i) => [`w${i}`, 0])), noDueDate: 0, openTasks: 5, unestimated: 4, people: 1 }] }
  if (code === 'task-hours') return { columns: [col('workDate', 'Date', 'date'), col('person', 'Person'), col('projectNumber', 'Project'), col('taskKey', 'Key', 'key'), col('taskName', 'Task'), col('hours', 'Hours', 'number'), col('note', 'Note')],
    rows: [{ workDate: '2026-09-15', person: me.displayName, projectNumber: project.projectNumber, taskKey: tasks[0].key, taskName: tasks[0].name, hours: 2, note: null, itemType: 'Task', id: tasks[0].id },
      { workDate: '2026-09-15', person: me.displayName, projectNumber: project.projectNumber, taskKey: 'T-DELETED', taskName: 'Synthetic recorded deleted task', hours: 1, note: null, itemType: 'Task', id: id(999) }, { person: 'Total', hours: 3 }] }
  if (code === 'allocations-by-source' && parameters.show === 'entries-without-project') return { columns: [col('personName', 'Person'), col('ownerName', 'Owner'), col('label', 'Label'), col('source', 'Source'), col('hoursPerWeek', 'Hours', 'number'), col('startWeek', 'Start', 'date'), col('endWeek', 'End', 'date'), col('confidence', 'Confidence'), col('visibility', 'Visibility')],
    rows: [{ personName: me.displayName, ownerName: 'Synthetic supervisor', label: 'Synthetic unlinked admin entry', source: 'Admin', hoursPerWeek: 4, startWeek: '2026-09-14', endWeek: '2026-09-21', confidence: 'Confirmed', visibility: 'Published' }] }
  const unitColumns = code === 'team-capacity' ? [col('unit', 'Unit'), col('unitPath', 'Unit path')] : []
  return { columns: [col('measure', 'Measure'), ...unitColumns, col('w0', '2026-09-14', 'number'), col('w1', '2026-09-21', 'number')], rows: (code === 'team-capacity' ? ['Capacity', 'Time away', 'Confirmed', 'Expected', 'Possible', 'Remaining', 'Over planned', 'Under planned', 'Partial'] : ['Admin', 'Approved', 'Total']).map(measure => ({ measure, unit: 'Synthetic historical unit', unitPath: 'Synthetic region / Synthetic historical unit', w0: measure === 'Capacity' ? 40 : 4, w1: measure === 'Capacity' ? 40 : 4 })) }
}
const basis = code => code === 'task-hours' ? 'CurrentRecordedTaskContext' : code.startsWith('workload-') ? 'WorkloadPeopleContext' : 'PlannerPeopleContext'
const basisCopy = code => code === 'task-hours' ? 'Current inputs for distinct tasks referenced by permitted time entries. Recorded hours stay separate; a task can appear in both counts.' : code.startsWith('workload-') ? 'Tasks assigned to people in this report scope, including their other visible disciplines. A task can appear in both counts.' : 'Tasks assigned to people represented in this report scope; task context is separate from planning-entry source and confidence. A task can appear in both counts.'
const uiKeys = ['taskInput', 'inputPage', 'inputPageSize', 'catalogue', 'view', 'viewProject', 'panel']
const asOf = '2026-09-14T12:00:00Z', listAsOf = '2026-09-14T12:05:00Z'
const canonical = (code, query) => ({ ...query, ...(code === 'team-capacity' || code === 'allocations-by-source' ? { from: query.from ?? '2026-09-14', weeks: query.weeks ?? '12', includeMyDrafts: query.includeMyDrafts ?? 'true', ...(code === 'allocations-by-source' ? { show: query.show ?? 'hours-by-source' } : {}) } : {}),
  ...(code === 'task-hours' ? { from: query.from ?? '2026-09-14', to: query.to ?? '2026-09-20', scope: query.scope ?? 'mine' } : {}) })
const forms = [
  ['workload-employee', { disciplineId: civil }], ['workload-discipline', { officeId: office }],
  ['team-capacity', { supervisorId: supervisor, disciplineId: civil, unitId: unit, from: '2026-09-14', weeks: '2', includeMyDrafts: 'false' }],
  ['allocations-by-source', { supervisorId: supervisor, from: '2026-09-14', weeks: '2', includeMyDrafts: 'false', show: 'hours-by-source', source: 'Admin', confidence: 'Confirmed' }],
  ['allocations-by-source', { supervisorId: supervisor, from: '2026-09-14', weeks: '2', includeMyDrafts: 'false', show: 'entries-without-project', source: 'Admin', confidence: 'Confirmed' }],
  ['task-hours', { userId: me.id, from: '2026-09-14', to: '2026-09-20', scope: 'mine' }],
]

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const errors = [], runs = [], inputReads = [], exported = [], saved = [], geometry = []
  let refuse = false, knownEmpty = false, stored = []
  const watch = predicate => { const pending = page.waitForResponse(predicate); void pending.catch(() => {}); return pending }
  const read = (code, query, metric, status = 200) => watch(response => {
    const url = new URL(response.url())
    return response.request().method() === 'GET' && response.status() === status && url.pathname === `/api/v1/reports/${code}${metric ? `/task-inputs/${metric}` : ''}` &&
      JSON.stringify(Object.entries(Object.fromEntries(url.searchParams)).sort()) === JSON.stringify(Object.entries(query).sort())
  })
  const finish = async response => { assert.equal(await (await response).finished(), null) }
  const table = code => page.locator(`table[data-report-table="${code}"]`)
  const dialog = () => page.getByRole('dialog', { name: 'Unestimated tasks', exact: true })
  const inputLink = () => page.locator('[data-report-task-input="unestimated"]')
  const controls = () => page.locator('details.report-controls')
  const openControls = async () => { if ((await controls().getAttribute('open')) === null) await controls().locator('summary').click(); await page.getByRole('form', { name: 'Report parameters', exact: true }).waitFor() }
  const goto = async (code, query, expected = query) => { const pending = read(code, expected); await page.goto(`${base}/reports/${code}?${new URLSearchParams(query)}`); await finish(pending); await table(code).locator('tbody tr').first().waitFor() }
  const reportGeometry = async code => {
    // A settled, unscrolled desktop layout is the measurement boundary. Never wait for y<=405 or scroll to make it pass.
    await page.waitForFunction(() => innerWidth === 1440 && innerHeight === 1000 && document.querySelector('main#content')?.clientHeight > 0)
    await page.evaluate(() => document.fonts.ready)
    await page.locator('main#content').evaluate(element => element.scrollTo({ top: 0, left: 0, behavior: 'auto' }))
    await page.waitForFunction(() => { const main = document.querySelector('main#content'); return main.scrollTop === 0 && main.scrollLeft === 0 })
    await table(code).locator('tbody tr').first().evaluate(async row => {
      let previous = null, equalFrames = 0
      for (let frame = 0; frame < 120; frame++) {
        await new Promise(resolve => requestAnimationFrame(resolve))
        const main = document.querySelector('main#content'), band = document.querySelector('.visual-page-band'), rect = row.getBoundingClientRect()
        const sample = JSON.stringify([innerWidth, innerHeight, main.clientWidth, main.clientHeight, main.scrollTop, main.scrollLeft, band?.getBoundingClientRect().height, rect.x, rect.y, rect.width, rect.height, document.fonts.status])
        equalFrames = sample === previous ? equalFrames + 1 : 0; previous = sample
        if (equalFrames >= 2) return
      }
      throw new Error('Report layout did not settle before first-data geometry measurement')
    })
    return table(code).evaluate(element => {
      const box = node => { const rect = node?.getBoundingClientRect(); return rect ? { top: rect.top, height: rect.height, width: rect.width } : null }
      const main = document.querySelector('main#content'), results = element.closest('.report-results')
      return { viewport: { width: innerWidth, height: innerHeight }, fonts: document.fonts.status,
        main: { ...box(main), scrollTop: main.scrollTop, scrollLeft: main.scrollLeft, scrollHeight: main.scrollHeight },
        band: box(document.querySelector('.visual-page-band')), results: box(results),
        counts: box(results?.querySelector('[data-report-counts]')), inputs: box(results?.querySelector('.report-inputs')),
        appliedTokens: [...(results?.querySelectorAll('.report-results__parameter') ?? [])].map(node => ({ text: node.textContent, ...box(node) })),
        notes: [...(results?.querySelectorAll('.report-results__notes > p') ?? [])].map(node => ({ text: node.textContent, ...box(node) })),
        controlsOpen: results?.querySelector('details.report-controls')?.open ?? null,
        table: box(element), header: box(element.querySelector('thead')), firstRow: box(element.querySelector('tbody tr')) }
    })
  }
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => errors.push(error.message)); page.on('console', message => { if (message.type() === 'error' && !(/\/task-inputs\//.test(message.location().url) && /403/.test(message.text()))) errors.push(message.text()) })
    await page.addInitScript(() => { window.__reportCopies = []; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async value => window.__reportCopies.push(value) } }) })
    me.flags = { ...me.flags, capacity_screens: true, capacity_reports: true, visual_system: true, org_structure: true, screen_patterns: false, home_overview: false, capacity_polish: false, work_screens: false, small_ux: false, live_events: false }
    me.settings.today = '2026-09-14'
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')], offices: [referenceOffice(office, 'Synthetic office')] }) }))
    await page.route('**/api/v1/reports**', route => {
      const request = route.request(), url = new URL(request.url()), parts = url.pathname.split('/'), code = parts[4], query = Object.fromEntries(url.searchParams)
      assert.equal(request.method(), 'GET')
      if (!code) return route.fulfill({ json: definitions.filter(def => me.flags.capacity_reports || !['team-capacity', 'allocations-by-source'].includes(def.code)).map(def => { const result = { ...def }; if (!me.flags.capacity_screens) delete result.question; return result }) })
      const def = definitions.find(row => row.code === code); assert.ok(def, code)
      for (const key of uiKeys) assert.equal(query[key], undefined, `${key} must not be data/export input`)
      const source = metric => knownEmpty && code === 'task-hours' ? [] : tasks.filter(task => metric === 'unestimated' ? task.estimatedHours == null : task.dueDate == null)
      if (parts[5] === 'export') { exported.push({ code, query }); return route.fulfill({ contentType: 'text/csv', body: 'Measure,Hours\nRecorded,3\n' }) }
      if (parts[5] === 'task-inputs') {
        inputReads.push({ code, metric: parts[6], query })
        if (refuse) return route.fulfill({ status: 403, json: { status: 403, title: 'Synthetic current input refusal', detail: 'Synthetic current input refusal' } })
        const { page: pageValue, pageSize, ...parameters } = query, items = source(parts[6]), n = Number(pageValue), size = Number(pageSize)
        assert.deepEqual(parameters, canonical(code, parameters))
        return route.fulfill({ json: { code, metric: parts[6], basis: basis(code), scopeCoverage: code === 'task-hours' || code === 'workload-discipline' ? 'VisibleOnly' : 'CompleteForVisibleParameters', unavailableReferences: code === 'task-hours' ? 1 : 0,
          total: code === 'task-hours' ? null : items.length, knownTotal: items.length, page: n, pageSize: size, items: items.slice((n - 1) * size, n * size), parameters, asOf: listAsOf } })
      }
      runs.push({ code, query }); const parameters = canonical(code, query), data = reportData(code, parameters)
      const count = metric => ({ count: code === 'task-hours' ? null : source(metric).length, knownCount: source(metric).length,
        href: `/reports/${code}?${new URLSearchParams({ ...parameters, taskInput: metric, inputPage: '1', inputPageSize: '50' })}` })
      return route.fulfill({ json: { code, title: def.title, itemType: null, ...data, total: data.rows.length, totalIsLowerBound: false, truncated: false, listLink: null,
        parameters: Object.entries(parameters).map(([key, value]) => ({ label: def.params.find(p => p.key === key)?.label ?? key, value: key === 'supervisorId' ? 'Synthetic supervisor' : key === 'disciplineId' ? 'Civil' : key === 'unitId' ? 'Synthetic historical unit' : key === 'officeId' ? 'Synthetic office' : key === 'userId' ? me.displayName : key === 'projectId' ? project.projectNumber : value })),
        ...(me.flags.visual_system ? { asOf } : {}), ...(me.flags.capacity_screens ? { savedParameters: parameters, taskInputs: { basis: basis(code), scopeCoverage: code === 'task-hours' || code === 'workload-discipline' ? 'VisibleOnly' : 'CompleteForVisibleParameters', unavailableReferences: code === 'task-hours' ? 1 : 0, unestimated: count('unestimated'), openUndated: count('open-undated'), parameters, asOf } } : {}) } })
    })
    await page.route('**/api/v1/views**', route => {
      const request = route.request(), url = new URL(request.url())
      if (request.method() === 'GET') return route.fulfill({ json: viewList(stored.filter(view => view.listType === url.searchParams.get('listType'))) })
      assert.equal(request.method(), 'POST'); const body = request.postDataJSON(); saved.push(body)
      stored.push({ ...savedView({ id: id(900), name: body.name, params: body.params }), listType: body.listType }); return route.fulfill({ status: 201, json: viewSaved(id(900)) })
    })
    await page.route('**/api/v1/tasks/**', route => {
      const path = new URL(route.request().url()).pathname, task = tasks.find(task => task.id === path.split('/')[4]); assert.ok(task, path)
      if (path.endsWith('/dependencies')) return route.fulfill({ json: taskDependencies() })
      return route.fulfill({ json: taskDetail({ task, permissions: taskPermissions({ edit: taskPermission(false), assign: taskPermission(false), setReviewer: taskPermission(false), dueDate: taskPermission(false), block: taskPermission(false), delete: taskPermission(false), comment: false, dependencies: false, enterTime: false }) }) })
    })
    await page.route('**/api/v1/items/Task/**', route => route.fulfill({ json: route.request().url().endsWith('/comments') ? comments : links }))

    for (const [code, parameters] of forms) {
      await page.setViewportSize({ width: 1440, height: 1000 })
      const context = { ...parameters, catalogue: 'attention', viewProject: project.id }
      await goto(code, context, parameters); await inputLink().waitFor()
      await page.getByText(basisCopy(code) + (code === 'task-hours' || code === 'workload-discipline' ? ' Only task records you may view in this report scope are counted.' : ''), { exact: true }).waitFor()
      const measured = await reportGeometry(code), rect = measured.firstRow; assert.ok(rect && rect.height > 0)
      assert.deepEqual(measured.viewport, { width: 1440, height: 1000 }); assert.equal(measured.fonts, 'loaded'); assert.equal(measured.main.scrollTop, 0); assert.equal(measured.main.scrollLeft, 0); assert.equal(measured.controlsOpen, false)
      geometry.push({ code, form: parameters.show ?? code, firstDataTop: rect.top, firstDataHeight: rect.height, measured }); assert.ok(rect.top <= 405, `${code}/${parameters.show ?? ''}: actual first report data row top ${rect.top} exceeds405`)
      if (code === 'task-hours') {
        await table(code).getByRole('cell', { name: 'Synthetic recorded deleted task', exact: true }).waitFor()
        await page.getByText('Recorded task references with unavailable current inputs: 1. Matching counts retain their known subtotal.', { exact: true }).waitFor()
        assert.equal(await inputLink().innerText(), 'Unestimated tasks', 'unknown total is never presented as a complete4 or0')
      } else assert.equal(await inputLink().innerText(), '4 unestimated tasks')

      // Use a genuinely small viewport for overflowing origin surfaces; standard first-row geometry was measured above.
      await page.setViewportSize({ width: 1440, height: 400 })
      await page.waitForFunction(() => { const main = document.querySelector('main#content'); return main.scrollHeight > main.clientHeight })
      await page.locator('main#content').evaluate(element => { element.scrollTop = 40 }); await inputLink().scrollIntoViewIfNeeded()
      const origin = page.url(), mainScroll = await page.locator('main#content').evaluate(element => element.scrollTop), beforeRuns = runs.length
      assert.ok(mainScroll > 0, `${code}: the report must genuinely scroll before opening its input context`)
      const first = read(code, { ...parameters, page: '1', pageSize: '50' }, 'unestimated')
      await inputLink().click(); await finish(first); await dialog().locator('[data-report-input-total="4"]').waitFor()
      assert.deepEqual(inputReads.at(-1), { code, metric: 'unestimated', query: { ...parameters, page: '1', pageSize: '50' } }); assert.equal(runs.length, beforeRuns)
      await dialog().getByText(basisCopy(code) + ' ' + (code === 'task-hours' || code === 'workload-discipline' ? 'Only task records you may view in this report scope are counted.' : 'Counts cover records you may view within these report parameters.'), { exact: true }).waitFor()
      assert.equal(await dialog().getByText('Synthetic recorded deleted task', { exact: true }).count(), 0)
      const source = dialog().getByRole('button', { name: `${tasks[1].key} ${tasks[1].name}`, exact: true }); await source.scrollIntoViewIfNeeded()
      const inputOrigin = page.url(), scroll = await page.locator('[data-slot="dialog-body"]').evaluate(element => element.scrollTop)
      assert.ok(scroll > 0, `${code}: input body must genuinely overflow before the non-first source opens`)
      const taskRead = watch(response => new URL(response.url()).pathname === `/api/v1/tasks/${tasks[1].id}`)
      await source.click(); await finish(taskRead); await page.getByRole('dialog').getByRole('heading', { name: tasks[1].name, exact: true }).waitFor()
      await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(inputOrigin)
      await dialog().locator('[data-report-input-total="4"]').waitFor()
      await page.waitForFunction(expected => document.activeElement?.id === expected.id && document.querySelector('[data-slot="dialog-body"]')?.scrollTop === expected.scroll, { id: `report-input-task-${tasks[1].id}`, scroll })
      await dialog().getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(origin)
      await page.waitForFunction(top => document.querySelector('main#content')?.scrollTop === top && document.activeElement?.getAttribute('data-report-task-input') === 'unestimated', mainScroll)
      assert.equal(runs.length, beforeRuns)

      // Every actual form also supports paged/reloaded canonical links; previous context cannot supply a broader source.
      const deep = { ...parameters, taskInput: 'unestimated', inputPage: '2', inputPageSize: '1' }, second = read(code, { ...parameters, page: '2', pageSize: '1' }, 'unestimated')
      await goto(code, deep, parameters); await finish(second); await dialog().getByRole('button', { name: `${tasks[1].key} ${tasks[1].name}`, exact: true }).waitFor()
      const refreshed = read(code, { ...parameters, page: '2', pageSize: '1' }, 'unestimated'); await page.reload(); await finish(refreshed)
      await dialog().getByText(code === 'task-hours' ? 'Known matches: page 2 of 4' : 'Page 2 of 4', { exact: true }).waitFor()
      await dialog().getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(url => !url.searchParams.has('taskInput') && !url.searchParams.has('inputPage'))
    }

    // Actual optional project control: choose a project and clear only that dimension while Civil remains selected.
    await page.setViewportSize({ width: 1440, height: 1000 }); await goto('workload-employee', { disciplineId: civil })
    await openControls(); await page.getByLabel('Project', { exact: true }).getByText('All my visible projects', { exact: true }).waitFor()
    const selected = read('workload-employee', { disciplineId: civil, projectId: project.id })
    await page.getByLabel('Project', { exact: true }).click(); await page.getByRole('radio', { name: /P-MOCK.*Synthetic live project/ }).click(); await finish(selected); await table('workload-employee').locator('tbody tr').first().waitFor()
    const cleared = read('workload-employee', { disciplineId: civil }); await page.getByLabel('Project', { exact: true }).click(); await page.getByRole('button', { name: 'All my visible projects', exact: true }).click(); await finish(cleared)
    assert.equal(new URL(page.url()).searchParams.has('projectId'), false); assert.equal(new URL(page.url()).searchParams.get('disciplineId'), civil)
    await page.getByRole('button', { name: 'Copy link', exact: true }).click()
    const copied = new URL(await page.evaluate(() => window.__reportCopies.at(-1))); assert.deepEqual(Object.fromEntries(copied.searchParams), { disciplineId: civil })
    await page.getByRole('region', { name: 'Views', exact: true }).getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    const saveDialog = page.getByRole('dialog', { name: 'Save this view', exact: true }); await saveDialog.getByLabel('Name', { exact: true }).fill('Synthetic Civil people context')
    const savedResponse = watch(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/views')
    await saveDialog.getByRole('button', { name: 'Save', exact: true }).click(); await finish(savedResponse); await saveDialog.waitFor({ state: 'hidden' })
    assert.deepEqual(saved.at(-1).params, { disciplineId: civil }); assert.equal(saved.at(-1).listType, 'report:workload-employee')
    const exportedResponse = watch(response => new URL(response.url()).pathname === '/api/v1/reports/workload-employee/export')
    await page.getByRole('button', { name: 'Export', exact: true }).click(); await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click(); await finish(exportedResponse)
    assert.deepEqual(exported.at(-1).query, { disciplineId: civil, format: 'csv' }); stored = []

    // Null complete totals with known0 remain unknown; recorded-hour rows still show their existing3h total.
    knownEmpty = true; const timeParameters = forms.at(-1)[1], empty = read('task-hours', { ...timeParameters, page: '1', pageSize: '50' }, 'unestimated')
    await goto('task-hours', { ...timeParameters, taskInput: 'unestimated', inputPage: '1', inputPageSize: '50' }, timeParameters); await finish(empty)
    assert.equal(await table('task-hours').locator('tbody tr').last().locator('td').nth(5).innerText(), '3')
    await dialog().getByText('Known matching tasks: 0', { exact: true }).waitFor(); await dialog().getByText('No known matching current tasks in this report scope', { exact: true }).waitFor()
    assert.equal(await dialog().getByText('0 unestimated tasks', { exact: true }).count(), 0); assert.equal(await dialog().getByRole('button', { name: 'Next page', exact: true }).isDisabled(), true); knownEmpty = false

    // A later current-permission refusal removes prior task rows and totals instead of leaving a cached page visible.
    const first = read('task-hours', { ...timeParameters, page: '1', pageSize: '1' }, 'unestimated')
    await goto('task-hours', { ...timeParameters, taskInput: 'unestimated', inputPage: '1', inputPageSize: '1' }, timeParameters); await finish(first); await dialog().locator('[data-report-input-total="4"]').waitFor()
    refuse = true; const refused = read('task-hours', { ...timeParameters, page: '2', pageSize: '1' }, 'unestimated', 403)
    await dialog().getByRole('button', { name: 'Next page', exact: true }).click(); await finish(refused); await dialog().getByRole('alert').filter({ hasText: 'Synthetic current input refusal' }).waitFor()
    assert.equal(await dialog().locator('tbody tr, [data-report-input-total]').count(), 0); refuse = false

    // Counts/dialog are independent of the visual system and other feature owners. Owner-off catalogue stays honest.
    me.flags.visual_system = false; await goto('workload-employee', { disciplineId: civil }); assert.equal(await controls().count(), 0)
    await page.getByLabel('Project', { exact: true }).waitFor(); await inputLink().waitFor()
    const independent = read('workload-employee', { disciplineId: civil, page: '1', pageSize: '50' }, 'unestimated'); await inputLink().click(); await finish(independent); await dialog().locator('[data-report-input-total="4"]').waitFor()
    me.flags.capacity_reports = false; await page.goto(`${base}/reports`); await page.getByRole('navigation', { name: 'Report catalogue', exact: true }).waitFor()
    assert.equal(await page.locator('[data-report-code="team-capacity"], [data-report-code="allocations-by-source"]').count(), 0)
    me.flags.capacity_reports = true; me.flags.visual_system = true; await page.setViewportSize({ width: 390, height: 844 })
    for (const [code, parameters] of forms) {
      await goto(code, parameters); await inputLink().waitFor(); assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
      const phone = read(code, { ...parameters, page: '1', pageSize: '50' }, 'unestimated'); await inputLink().click(); await finish(phone); await dialog().locator('[data-report-input-total="4"]').waitFor()
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
    }
    me.flags.capacity_screens = false; await page.setViewportSize({ width: 1440, height: 1000 })
    const offQuery = { disciplineId: civil, projectId: project.id }, off = read('workload-employee', offQuery)
    await page.goto(`${base}/reports/workload-employee?${new URLSearchParams({ ...offQuery, taskInput: 'unestimated', inputPage: '2', inputPageSize: '1' })}`); await finish(off)
    await page.getByLabel('Project', { exact: true }).waitFor(); assert.equal(await controls().count(), 0); assert.equal(await page.locator('[data-report-task-input], [data-report-input-table], [data-report-table]').count(), 0); assert.equal(await page.getByRole('dialog').count(), 0)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: six remaining report forms, actual source basis, optional project scopes, nullable/known0 references, exact metric/source return/focus/paging, canonical persistence/export and owner/phone/off state')
    console.log(JSON.stringify({ actualFirstReportRows: geometry }))
  } catch (error) { console.error(await page.locator('body').innerText(), JSON.stringify({ runs, inputReads, geometry, unknownGets: mock.unknownGets, errors }, null, 2)); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
