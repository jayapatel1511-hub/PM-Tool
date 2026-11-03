// S23-B: synthetic UI contract and actual report-row geometry. API source/count correctness has separate real-DB cases.
const assert = require('node:assert/strict')
const { newMockPage, me, project, links, comments } = require('./platform-fixtures.cjs')
const { taskRow, taskDetail, taskPermissions, taskPermission } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0007-${String(n).padStart(12, '0')}`
const civil = project.disciplines[0].disciplineId
const param = (key, label, type, value = null, options = null) => ({ key, label, type, default: value, options })
const projects = param('projectId', 'Projects', 'projects')
const definitions = [
  { code: 'stale-work', title: 'Stale Work', params: [projects, param('days', 'Days without activity', 'number')] },
  { code: 'overdue-tasks', title: 'Overdue Tasks', params: [projects, param('disciplineId', 'Discipline', 'discipline'), param('assigneeId', 'Assignee', 'person'), param('minDaysOverdue', 'Minimum days overdue', 'number', '1')] },
  { code: 'blocked-tasks', title: 'Blocked Tasks', params: [projects, param('disciplineId', 'Discipline', 'discipline'), param('blockerType', 'Blocker type', 'select', null, ['task', 'decision', 'manual'].map(value => ({ value, label: value })))] },
  { code: 'tasks-blocking-others', title: 'Tasks Blocking Others', params: [projects] },
  { code: 'review-queue', title: 'Review Queue', params: [param('reviewerId', 'Reviewer', 'person'), projects] },
  { code: 'tasks-due-this-week', title: 'Tasks Due This Week', params: [projects, param('disciplineId', 'Discipline', 'discipline'), param('assigneeId', 'Assignee', 'person'), param('week', 'Week', 'date'), param('mine', 'Mine', 'bool')] },
  { code: 'project-activity-log', title: 'Project Activity Log', params: [param('projectId', 'Project', 'project'), param('from', 'From', 'date'), param('to', 'To', 'date')] },
].map(def => ({ ...def, question: 'Which work needs attention?', description: 'Current permitted work, using the selected report parameters.', itemType: 'Task', columns: [] }))
const shared = taskRow({ id: id(1), projectId: project.id, key: 'T-001', name: 'Synthetic shared input task' })
const unestimated = [shared, ...Array.from({ length: 52 }, (_, i) => taskRow({ id: id(i + 2), projectId: project.id, key: `T-${String(i + 2).padStart(3, '0')}`, name: `Synthetic unestimated task ${i + 2}`, dueDate: '2026-09-16' }))]
const undated = [shared, ...Array.from({ length: 52 }, (_, i) => taskRow({ id: id(i + 101), projectId: project.id, key: `T-${i + 101}`, name: `Synthetic zero-estimate undated task ${i + 101}`, estimatedHours: 0 }))]
const source = [...unestimated, ...undated.slice(1)]
const column = (path, header, type = 'text') => ({ path, header, type })
// Match the actual six report column sets; catalogue rows and an artificially short proxy table are not geometry evidence.
const reportColumns = {
  'stale-work': [column('key', 'Key', 'key'), column('name', 'Task'), column('projectNumber', 'Project'), column('assigneeName', 'Assignee'), column('status', 'Status'), column('lastActivityAt', 'Last activity', 'datetime'), column('staleDays', 'Days', 'number')],
  'tasks-due-this-week': [column('key', 'Key', 'key'), column('name', 'Task'), column('projectNumber', 'Project'), column('deliverableKey', 'Deliverable'), column('disciplineName', 'Discipline'), column('assigneeName', 'Assignee'), column('dueDate', 'Due', 'date'), column('status', 'Status'), column('indicators', 'Indicators')],
  'overdue-tasks': [column('key', 'Key', 'key'), column('name', 'Task'), column('projectNumber', 'Project'), column('assigneeName', 'Assignee'), column('dueDate', 'Due', 'date'), column('state.daysOverdue', 'Days overdue', 'number'), column('state.blockingCount', 'Blocking count', 'number'), column('affectedMilestones', 'Affected milestones'), column('status', 'Status')],
  'blocked-tasks': [column('key', 'Key', 'key'), column('name', 'Task'), column('projectNumber', 'Project'), column('assigneeName', 'Assignee'), column('state.blockedSince', 'Blocked since', 'date'), column('state.daysBlocked', 'Days blocked', 'number'), column('blockers', 'Blockers'), column('affectedMilestones', 'Affected milestones')],
  'tasks-blocking-others': [column('key', 'Key', 'key'), column('name', 'Task'), column('projectNumber', 'Project'), column('assigneeName', 'Assignee'), column('dueDate', 'Due', 'date'), column('status', 'Status'), column('state.blockingCount', 'Successors', 'number'), column('successorMilestones', 'Successor milestones')],
  'review-queue': [column('key', 'Key', 'key'), column('name', 'Item'), column('itemTypeLabel', 'Type'), column('projectNumber', 'Project'), column('assigneeName', 'Assignee'), column('reviewerName', 'Reviewer'), column('readySince', 'Ready since', 'date'), column('daysWaiting', 'Days waiting', 'number'), column('round', 'Round', 'number')],
}
const asOf = '2026-09-14T12:00:00Z', inputAsOf = '2026-09-14T12:05:00Z'
const ephemeral = ['taskInput', 'inputPage', 'inputPageSize', 'catalogue', 'view', 'viewProject', 'panel']
const canonical = (code, query) => ({ ...query, ...(code === 'stale-work' ? { days: query.days ?? '14' } : code === 'overdue-tasks' ? { minDaysOverdue: query.minDaysOverdue ?? '1' } : {}) })
const cohort = (code, metric) => code === 'tasks-due-this-week' ? metric === 'unestimated' ? [unestimated[1]] : [] : metric === 'unestimated' ? unestimated : undated
const href = (code, parameters, metric) => `/reports/${code}?${new URLSearchParams({ ...parameters, taskInput: metric, inputPage: '1', inputPageSize: '50' })}`

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const errors = [], runs = [], lists = [], exports = [], writes = [], geometry = []
  let stored = [], refuseInputs = false
  const watch = predicate => { const pending = page.waitForResponse(predicate); void pending.catch(() => {}); return pending }
  const nextRead = (code, expected, metric, status = 200) => watch(response => {
    const url = new URL(response.url())
    return response.request().method() === 'GET' && response.status() === status && url.pathname === `/api/v1/reports/${code}${metric ? `/task-inputs/${metric}` : ''}` &&
      JSON.stringify(Object.entries(Object.fromEntries(url.searchParams)).sort()) === JSON.stringify(Object.entries(expected).sort())
  })
  const finish = async response => { assert.equal(await (await response).finished(), null) }
  const controls = () => page.locator('details.report-controls')
  const openControls = async () => { if ((await controls().getAttribute('open')) === null) await controls().locator('summary').click(); await page.getByRole('form', { name: 'Report parameters', exact: true }).waitFor() }
  const reportRows = code => page.locator(`table[data-report-table="${code}"] tbody tr`)
  const inputDialog = () => page.getByRole('dialog', { name: 'Unestimated tasks', exact: true })
  const gotoReport = async (code, query = {}, expected = query) => {
    const read = nextRead(code, expected); await page.goto(`${base}/reports/${code}${Object.keys(query).length ? `?${new URLSearchParams(query)}` : ''}`); await finish(read); await reportRows(code).first().waitFor()
  }
  const measure = async (code, name, compact = true) => {
    const first = reportRows(code).first(); await first.waitFor()
    await page.evaluate(() => document.fonts.ready)
    await page.locator('main#content').evaluate(element => element.scrollTo({ top: 0, left: 0, behavior: 'auto' }))
    await first.evaluate(async row => {
      let previous = null, equalFrames = 0
      for (let frame = 0; frame < 120; frame++) {
        await new Promise(resolve => requestAnimationFrame(resolve))
        const rect = row.getBoundingClientRect(), sample = JSON.stringify([innerWidth, innerHeight, rect.x, rect.y, rect.width, rect.height, document.fonts.status])
        equalFrames = sample === previous ? equalFrames + 1 : 0; previous = sample
        if (equalFrames >= 2) return
      }
      throw new Error('Report layout did not settle before first-data geometry measurement')
    })
    const rect = await first.boundingBox(); assert.ok(rect && rect.height > 0, 'a nonempty real report data row must be measured')
    geometry.push({ name, code, compact, top: rect.y, height: rect.height }); if (compact) assert.ok(rect.y <= 405, `${name}: real report first data row top ${rect.y} exceeds 405px`)
  }
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => errors.push(error.message))
    page.on('console', message => { if (message.type() === 'error' && !(/\/task-inputs\//.test(message.location().url) && /403/.test(message.text()))) errors.push(message.text()) })
    await page.addInitScript(() => { window.__reportCopies = []; Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async value => window.__reportCopies.push(value) } }) })
    me.flags = { ...me.flags, capacity_screens: true, visual_system: true, screen_patterns: false, work_screens: false, small_ux: false, home_overview: false, live_events: false }
    me.settings.today = '2026-09-14'
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')] }) }))
    await page.route('**/api/v1/reports**', async route => {
      const request = route.request(), url = new URL(request.url()), parts = url.pathname.split('/'), code = parts[4]
      assert.equal(request.method(), 'GET', 'task-input review must remain read-only')
      if (!code) return route.fulfill({ json: definitions.map(def => { const row = { ...def }; if (!me.flags.capacity_screens) delete row.question; return row }) })
      const def = definitions.find(row => row.code === code); assert.ok(def, code)
      const query = Object.fromEntries(url.searchParams)
      for (const key of ephemeral) assert.equal(query[key], undefined, `${key} must not leak into report/list/export requests`)
      if (parts[5] === 'export') { exports.push({ code, query }); return route.fulfill({ contentType: 'text/csv', body: 'Task,Name\nT-001,Synthetic shared input task\n' }) }
      if (parts[5] === 'task-inputs') {
        const metric = parts[6], { page: pageValue, pageSize, ...data } = query
        lists.push({ code, metric, query })
        if (refuseInputs) return route.fulfill({ status: 403, json: { title: 'Synthetic input permission refusal', detail: 'Synthetic input permission refusal', status: 403 } })
        assert.deepEqual(data, canonical(code, data), 'input GET receives concrete canonical report parameters')
        const items = cohort(code, metric), n = Number(pageValue), size = Number(pageSize)
        return route.fulfill({ json: { code, metric, basis: 'ReportTaskRows', scopeCoverage: 'CompleteForVisibleParameters', unavailableReferences: 0, total: items.length, knownTotal: items.length,
          page: n, pageSize: size, items: items.slice((n - 1) * size, n * size), parameters: data, asOf: inputAsOf } })
      }
      runs.push({ code, query })
      const parameters = canonical(code, query), rows = code === 'tasks-due-this-week' ? [unestimated[1]] : source
      const taskInputs = { basis: 'ReportTaskRows', scopeCoverage: 'CompleteForVisibleParameters', unavailableReferences: 0, parameters, asOf,
        unestimated: { count: cohort(code, 'unestimated').length, knownCount: cohort(code, 'unestimated').length, href: href(code, parameters, 'unestimated') },
        openUndated: { count: cohort(code, 'open-undated').length, knownCount: cohort(code, 'open-undated').length, href: href(code, parameters, 'open-undated') } }
      return route.fulfill({ json: { code, title: def.title, itemType: def.itemType, columns: reportColumns[code] ?? reportColumns['stale-work'], rows, total: rows.length, totalIsLowerBound: false, truncated: false, listLink: null,
        parameters: Object.entries(parameters).map(([key, value]) => ({ label: def.params.find(p => p.key === key)?.label ?? key, value: key === 'projectId' ? project.projectNumber : key === 'disciplineId' ? 'Civil' : ['assigneeId', 'reviewerId'].includes(key) ? me.displayName : key === 'mine' ? 'Yes' : key === 'blockerType' ? 'Decision' : value })),
        ...(me.flags.visual_system ? { asOf } : {}), ...(me.flags.capacity_screens ? { savedParameters: parameters, ...(code !== 'project-activity-log' ? { taskInputs } : {}) } : {}) } })
    })
    await page.route('**/api/v1/views**', async route => {
      const request = route.request(), url = new URL(request.url())
      if (request.method() === 'GET') return route.fulfill({ json: viewList(stored.filter(row => row.listType === url.searchParams.get('listType'))) })
      assert.equal(request.method(), 'POST'); const body = request.postDataJSON(); writes.push(body)
      stored.push({ ...savedView({ id: id(900), name: body.name, params: body.params }), listType: body.listType })
      return route.fulfill({ status: 201, json: viewSaved(id(900)) })
    })
    await page.route('**/api/v1/tasks/**', route => {
      const path = new URL(route.request().url()).pathname, task = source.find(row => row.id === path.split('/')[4])
      assert.ok(task, path)
      if (path.endsWith('/dependencies')) return route.fulfill({ json: taskDependencies() })
      return route.fulfill({ json: taskDetail({ task, description: `Source ${task.name}`, permissions: taskPermissions({ edit: taskPermission(false), assign: taskPermission(false), setReviewer: taskPermission(false), dueDate: taskPermission(false), block: taskPermission(false), delete: taskPermission(false), comment: false, dependencies: false, enterTime: false }) }) })
    })
    await page.route('**/api/v1/items/Task/**', route => route.fulfill({ json: route.request().url().endsWith('/comments') ? comments : links }))

    // Flag independence, complete overlapping counts, compact mounted controls and actual first-row geometry.
    const originQuery = { projectId: project.id, days: '21', catalogue: 'attention', viewProject: project.id }
    await gotoReport('stale-work', originQuery, { projectId: project.id, days: '21' })
    assert.equal(await controls().getAttribute('open'), null)
    await page.getByRole('link', { name: '53 unestimated tasks', exact: true }).waitFor()
    await page.getByRole('link', { name: '53 open tasks with no due date', exact: true }).waitFor()
    await page.getByText("Tasks in this report's filtered results; the two counts may include the same task.", { exact: true }).waitFor()
    await measure('stale-work', 'compact visual report with overlapping inputs')
    // Ordinary task keys in the enhanced report retain their report origin too.
    const tableTask = source[1], tableOrigin = page.url()
    const tableLink = reportRows('stale-work').nth(1).getByRole('link', { name: tableTask.key, exact: true })
    const tableTaskRead = watch(response => response.url().endsWith(`/api/v1/tasks/${tableTask.id}`))
    await tableLink.click(); await finish(tableTaskRead)
    await page.waitForURL(url => url.pathname === '/reports/stale-work' && url.searchParams.get('panel') === `Task:${tableTask.id}`)
    await page.getByRole('dialog').getByRole('heading', { name: tableTask.name, exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(tableOrigin)
    await page.waitForFunction(key => document.activeElement?.textContent === key, tableTask.key)
    const summaryLink = page.locator('[data-report-task-input="unestimated"]'), reportOrigin = page.url(), runCount = runs.length
    await page.locator('main#content').evaluate(element => { element.scrollTop = 100 })
    const sourceScroll = await page.locator('main#content').evaluate(element => element.scrollTop); assert.ok(sourceScroll > 0)
    const firstInputs = nextRead('stale-work', { projectId: project.id, days: '21', page: '1', pageSize: '50' }, 'unestimated')
    await summaryLink.click(); await finish(firstInputs); await inputDialog().locator('[data-report-input-total="53"]').waitFor()
    assert.equal(runs.length, runCount, 'opening the input dialog does not rerun the report')
    assert.equal(new URL(page.url()).searchParams.get('catalogue'), 'attention'); assert.equal(new URL(page.url()).searchParams.get('viewProject'), project.id)
    assert.equal(await inputDialog().locator('tbody tr').count(), 50)
    await inputDialog().getByText('This list is a fresh read under your current permissions; its count may have changed since the report was opened.', { exact: true }).waitFor()
    const sourceButton = inputDialog().getByRole('button', { name: `${unestimated[49].key} ${unestimated[49].name}`, exact: true })
    await sourceButton.scrollIntoViewIfNeeded()
    const inputOrigin = page.url(), before = await page.evaluate(() => ({ main: document.querySelector('main#content').scrollTop, input: document.querySelector('[data-slot="dialog-body"]').scrollTop }))
    assert.ok(before.input > 0, 'the real task-input body must overflow')
    const taskRead = watch(response => response.url().endsWith(`/api/v1/tasks/${unestimated[49].id}`))
    await sourceButton.click(); await finish(taskRead); await page.waitForURL(url => url.searchParams.get('panel') === `Task:${unestimated[49].id}`)
    await page.getByRole('dialog').getByRole('heading', { name: unestimated[49].name, exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(inputOrigin)
    await inputDialog().locator('[data-report-input-total="53"]').waitFor()
    await page.waitForFunction(position => document.querySelector('main#content')?.scrollTop === position.main && document.querySelector('[data-slot="dialog-body"]')?.scrollTop === position.input, before)
    await page.waitForFunction(target => document.activeElement?.id === target, `report-input-task-${unestimated[49].id}`)
    const secondInputs = nextRead('stale-work', { projectId: project.id, days: '21', page: '2', pageSize: '50' }, 'unestimated')
    await inputDialog().getByRole('button', { name: 'Next page', exact: true }).click(); await finish(secondInputs)
    await inputDialog().getByText('Page 2 of 2', { exact: true }).waitFor(); assert.equal(await inputDialog().locator('tbody tr').count(), 3)
    await inputDialog().getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(reportOrigin)
    await page.waitForFunction(scroll => document.querySelector('main#content')?.scrollTop === scroll && document.activeElement?.getAttribute('data-report-task-input') === 'unestimated', sourceScroll)
    assert.equal(runs.length, runCount, 'Task and input closes preserve report scope without a second report read')

    // Deep links/reload retain the exact canonical scope and page; the overlapping record belongs to both lists.
    const deep = { projectId: project.id, days: '21', taskInput: 'open-undated', inputPage: '1', inputPageSize: '1', catalogue: 'attention' }
    const deepList = nextRead('stale-work', { projectId: project.id, days: '21', page: '1', pageSize: '1' }, 'open-undated')
    await gotoReport('stale-work', deep, { projectId: project.id, days: '21' }); await finish(deepList)
    const undatedDialog = page.getByRole('dialog', { name: 'Open tasks with no due date', exact: true })
    await undatedDialog.getByRole('button', { name: `${shared.key} ${shared.name}`, exact: true }).waitFor()
    const reloadList = nextRead('stale-work', { projectId: project.id, days: '21', page: '1', pageSize: '1' }, 'open-undated')
    await page.reload(); await finish(reloadList); await undatedDialog.locator('[data-report-input-total="53"]').waitFor()
    const nextUndated = nextRead('stale-work', { projectId: project.id, days: '21', page: '2', pageSize: '1' }, 'open-undated')
    await undatedDialog.getByRole('button', { name: 'Next page', exact: true }).click(); await finish(nextUndated)
    await undatedDialog.getByRole('button', { name: `${undated[1].key} ${undated[1].name}`, exact: true }).waitFor()
    await undatedDialog.getByRole('cell', { name: '0', exact: true }).waitFor()
    await undatedDialog.getByRole('button', { name: 'Return to report', exact: true }).click()
    await page.waitForURL(url => !url.searchParams.has('taskInput') && url.searchParams.get('catalogue') === 'attention')

    // The maximum supported page size is still a bounded read with the same full known total.
    const largePage = nextRead('stale-work', { projectId: project.id, days: '21', page: '1', pageSize: '200' }, 'unestimated')
    await gotoReport('stale-work', { projectId: project.id, days: '21', taskInput: 'unestimated', inputPage: '1', inputPageSize: '200', catalogue: 'attention' }, { projectId: project.id, days: '21' }); await finish(largePage)
    await inputDialog().locator('[data-report-input-total="53"]').waitFor(); assert.equal(await inputDialog().locator('tbody tr').count(), 53)
    assert.equal(await inputDialog().getByRole('button', { name: 'Next page', exact: true }).isDisabled(), true)
    await inputDialog().getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(url => !url.searchParams.has('taskInput'))

    // Copy/save/export only canonical data parameters, then a changed parameter clears ephemeral input state.
    await openControls(); await page.getByRole('button', { name: 'Copy link', exact: true }).click()
    const copied = new URL(await page.evaluate(() => window.__reportCopies.at(-1)))
    assert.deepEqual(Object.fromEntries(copied.searchParams), { projectId: project.id, days: '21' })
    await page.getByRole('region', { name: 'Views', exact: true }).getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
    const save = page.getByRole('dialog', { name: 'Save this view', exact: true }); await save.getByLabel('Name', { exact: true }).fill('Synthetic report scope')
    const saved = watch(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/views')
    await save.getByRole('button', { name: 'Save', exact: true }).click(); await finish(saved); await save.waitFor({ state: 'hidden' })
    assert.deepEqual(writes.at(-1).params, { projectId: project.id, days: '21' }); assert.equal(writes.at(-1).listType, 'report:stale-work')
    const exported = watch(response => new URL(response.url()).pathname === '/api/v1/reports/stale-work/export')
    await page.getByRole('button', { name: 'Export', exact: true }).click(); await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click(); await finish(exported)
    await page.waitForFunction(() => !document.querySelector('[role="menu"]'))
    assert.deepEqual(exports.at(-1).query, { projectId: project.id, days: '21', format: 'csv' })
    const changed = nextRead('stale-work', { projectId: project.id, days: '28' })
    await page.getByLabel('Days without activity', { exact: true }).fill('28'); await finish(changed); await reportRows('stale-work').first().waitFor()
    for (const key of ['taskInput', 'inputPage', 'inputPageSize']) assert.equal(new URL(page.url()).searchParams.has(key), false)

    // Every initial six report code preserves its own scalar qualifiers. Complex filters retain the same geometry bound.
    stored = []
    for (const [code, query] of [
      ['overdue-tasks', { projectId: project.id, minDaysOverdue: '5' }],
      ['blocked-tasks', { projectId: project.id, disciplineId: civil, blockerType: 'decision' }],
      ['tasks-blocking-others', { projectId: project.id }],
      ['review-queue', { projectId: project.id, reviewerId: me.id }],
      ['tasks-due-this-week', { projectId: project.id, disciplineId: civil, assigneeId: me.id, week: '2026-09-14', mine: 'true' }],
    ]) {
      await gotoReport(code, query); await measure(code, `complex scoped ${code}`)
      const read = nextRead(code, { ...query, page: '1', pageSize: '50' }, 'unestimated')
      await page.locator('[data-report-task-input="unestimated"]').click(); await finish(read)
      await inputDialog().locator(`[data-report-input-total="${cohort(code, 'unestimated').length}"]`).waitFor()
      assert.deepEqual(lists.at(-1), { code, metric: 'unestimated', query: { ...query, page: '1', pageSize: '50' } })
      await inputDialog().getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(url => !url.searchParams.has('taskInput'))
    }
    await page.getByRole('link', { name: '0 open tasks with no due date', exact: true }).waitFor()
    const emptyInputs = nextRead('tasks-due-this-week', { projectId: project.id, disciplineId: civil, assigneeId: me.id, week: '2026-09-14', mine: 'true', page: '1', pageSize: '50' }, 'open-undated')
    await page.locator('[data-report-task-input="open-undated"]').click(); await finish(emptyInputs)
    await undatedDialog.getByText('No matching tasks in this report scope', { exact: true }).waitFor(); assert.equal(await undatedDialog.locator('tbody tr').count(), 0)
    await undatedDialog.getByRole('button', { name: 'Next page', exact: true }).isDisabled().then(value => assert.equal(value, true))

    // A refused fresh page never leaves the previously visible records/counts on screen.
    const refusalQuery = { days: '21', taskInput: 'unestimated', inputPage: '1', inputPageSize: '1' }
    const beforeRefusal = nextRead('stale-work', { days: '21', page: '1', pageSize: '1' }, 'unestimated')
    await gotoReport('stale-work', refusalQuery, { days: '21' }); await finish(beforeRefusal); await inputDialog().locator('[data-report-input-total="53"]').waitFor()
    refuseInputs = true
    const refused = nextRead('stale-work', { days: '21', page: '2', pageSize: '1' }, 'unestimated', 403)
    await inputDialog().getByRole('button', { name: 'Next page', exact: true }).click(); await finish(refused)
    await inputDialog().getByRole('alert').filter({ hasText: 'Synthetic input permission refusal' }).waitFor()
    assert.equal(await inputDialog().locator('tbody tr, [data-report-input-total]').count(), 0)
    assert.equal(await inputDialog().getByText(shared.name, { exact: true }).count(), 0); refuseInputs = false

    // Invalid metric never broadens to a valid list, and unsupported report omits input controls entirely.
    const readsBeforeInvalid = lists.length
    await gotoReport('stale-work', { days: '21', taskInput: 'invented' }, { days: '21' })
    await page.getByRole('dialog', { name: 'Task inputs in this report', exact: true }).getByRole('alert').waitFor(); assert.equal(lists.length, readsBeforeInvalid)
    await gotoReport('project-activity-log', { projectId: project.id, from: '2026-09-01', to: '2026-09-14' })
    assert.equal(await page.locator('[data-report-task-input]').count(), 0)

    // Saved required-project defaults still bootstrap while the compact control disclosure is closed.
    stored = [{ ...savedView({ id: id(901), name: 'Synthetic required scope', params: { projectId: project.id, from: '2026-09-01', to: '2026-09-14' }, isDefault: true }), listType: 'report:project-activity-log' }]
    const bootstrapped = nextRead('project-activity-log', stored[0].params)
    await page.goto(`${base}/reports/project-activity-log`); await finish(bootstrapped); await reportRows('project-activity-log').first().waitFor()
    assert.equal(await controls().getAttribute('open'), null); assert.equal(new URL(page.url()).searchParams.get('projectId'), project.id); stored = []

    // Counts/dialog also work with visual_system=false, while the checkpoint17 expanded controls remain exact.
    me.flags.visual_system = false; await gotoReport('stale-work', { days: '21' })
    await page.getByLabel('Days without activity', { exact: true }).waitFor(); assert.equal(await controls().count(), 0)
    await measure('stale-work', 'expanded controls with visual system disabled', false)
    const independentInputs = nextRead('stale-work', { days: '21', page: '1', pageSize: '50' }, 'unestimated')
    await page.locator('[data-report-task-input="unestimated"]').click(); await finish(independentInputs); await inputDialog().locator('[data-report-input-total="53"]').waitFor()
    await inputDialog().getByRole('button', { name: 'Return to report', exact: true }).click(); await page.waitForURL(url => !url.searchParams.has('taskInput'))
    // Visual-on geometry and phone overflow; legacy off has the unchanged inline controls and no new UI/reads.
    me.flags.visual_system = true; await gotoReport('stale-work', { days: '21' }); await measure('stale-work', 'compact visual system enabled')
    await page.setViewportSize({ width: 390, height: 844 }); await gotoReport('stale-work', { days: '21' })
    await openControls(); await page.getByLabel('Days without activity', { exact: true }).waitFor()
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
    await controls().locator('summary').click()
    const phoneInputs = nextRead('stale-work', { days: '21', page: '1', pageSize: '50' }, 'unestimated')
    await page.locator('[data-report-task-input="unestimated"]').click(); await finish(phoneInputs); await inputDialog().locator('[data-report-input-total="53"]').waitFor()
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
    me.flags.capacity_screens = false; me.flags.visual_system = false; await page.setViewportSize({ width: 1440, height: 1000 })
    const off = nextRead('stale-work', { days: '21' })
    await page.goto(`${base}/reports/stale-work?days=21&taskInput=unestimated&inputPage=2&inputPageSize=1`); await finish(off)
    await page.getByLabel('Days without activity', { exact: true }).waitFor()
    assert.equal(await controls().count(), 0); assert.equal(await page.locator('[data-report-task-input], [data-report-input-table], [data-report-table]').count(), 0)
    assert.equal(await page.getByRole('dialog').count(), 0)
    // The remaining independent pair: visual system alone must not enable report-completeness UI or reads.
    me.flags.visual_system = true
    const listCountOff = lists.length, visualOnly = nextRead('stale-work', { days: '21' })
    await page.goto(`${base}/reports/stale-work?days=21&taskInput=unestimated&inputPage=2&inputPageSize=1`); await finish(visualOnly)
    await page.getByLabel('Days without activity', { exact: true }).waitFor()
    assert.equal(await page.locator('html').getAttribute('data-visual'), 'v2')
    assert.equal(await controls().count(), 0); assert.equal(await page.locator('[data-report-task-input], [data-report-input-table], [data-report-table]').count(), 0)
    assert.equal(await page.getByRole('dialog').count(), 0); assert.equal(lists.length, listCountOff)
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: six exact task-report input scopes, full pagers/overlap/zero, retained Task and report return, live refusal, canonical save/copy/export, mounted compact controls/default bootstrap, phone/off state')
    console.log(JSON.stringify({ meaningfulFirstReportRows: geometry }))
  } catch (error) { console.error(await page.locator('body').innerText(), { runs, lists, geometry, unknownGets: mock.unknownGets, errors }); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
