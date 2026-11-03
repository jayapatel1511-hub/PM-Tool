// S11-B actual-app/mock-API acceptance. The companion API tests own real manifest/date eligibility.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds, comments, links } = require('./platform-fixtures.cjs')
const { taskRow, taskPage, taskDetail, taskPermissions, taskState } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { projectList, projectRow } = require('./projects.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-10-03T12:00:00Z'
const required = ['key', 'name', 'project']
const defaults = [...required, 'status', 'assignee', 'dueDate', 'priority', 'progress', 'indicators', 'lastActivity', 'comments']
const five = ['deliverable', 'reviewer', 'estimate', 'lastActivity', 'submissionDate']
const eight = [...required, 'deliverable', 'reviewer', 'estimate', 'lastActivity', 'submissionDate']
const preference = `hub.workspaceTaskColumns.${me.id}`, projectPreference = 'hub.taskColumns'
const projects = [project, ...[1, 2].map(n => ({ ...project, id: id(8300 + n), projectNumber: `P-COLS-${n}`, name: `Synthetic column project ${n}` }))]
const twelve = projects.flatMap((p, n) => Array.from({ length: 4 }, (_, i) => taskRow({
  id: id(8400 + n * 10 + i), projectId: p.id, projectNumber: p.projectNumber, projectName: p.name, key: `${p.projectNumber}-T${i + 1}`,
  name: `Column task ${n + 1}.${i + 1}`, projectDisciplineId: n ? id(8350 + n) : project.disciplines[0].id,
  deliverableId: n ? id(8360 + n) : panelIds.deliverable, deliverableKey: 'D-001', deliverableName: 'Same deliverable label',
  assigneeId: me.id, assigneeName: me.displayName, reviewerId: id(8390), reviewerName: 'Pat Reviewer', estimatedHours: i === 0 ? 0 : i === 1 ? null : i,
  dueDate: '2027-02-20', lastActivityAt: stamp, lastActivityActorName: 'Pat', commentCount: 3,
  submissionDate: { state: 'Known', targetDate: '2027-02-11' }, state: taskState({ isUnassigned: false, isMissingDueDate: false }),
})))
const paged = Array.from({ length: 201 }, (_, i) => taskRow({ ...twelve[0], id: id(8800 + i), key: `PAGED-T${String(i + 1).padStart(3, '0')}`, name: `Column paged task ${i + 1}`, estimatedHours: 1 }))
let mode = 'multi', targetDate = '2027-02-11'
function cohort() {
  const rows = mode === 'paged' ? paged : mode === 'unknown' ? twelve.slice(0, 4).map((row, i) => ({ ...row,
    submissionDate: i === 0 ? { state: 'Known', targetDate: null } : i === 1 ? { state: 'Unavailable', targetDate: null } : i === 2 ? undefined : { state: 'Known', targetDate },
  })) : twelve.map(row => ({ ...row, submissionDate: { state: 'Known', targetDate } }))
  if (me.flags.work_screens) return rows
  return rows.map(row => { const { submissionDate, ...legacy } = row; return legacy })
}
function summary(rows) {
  return { rowCount: rows.length, estimatedHours: rows.reduce((sum, row) => sum + (row.estimatedHours ?? 0), 0), unestimatedCount: rows.filter(row => row.estimatedHours == null).length,
    overdueCount: 0, unavailableStateCount: 0, progressPct: 0, progressBasis: rows.some(row => row.estimatedHours == null) ? 'Count' : 'Hours', progressTaskCount: rows.length,
    nextDueDate: '2027-02-20', undatedOpenCount: 0 }
}
function groups(rows, visible) {
  const outer = new Set(rows.map(row => row.projectId)).size > 1, out = []
  for (const pid of [...new Set(visible.map(row => row.projectId))]) {
    const allProject = rows.filter(row => row.projectId === pid), shown = visible.filter(row => row.projectId === pid), first = allProject[0], parent = `${pid}:project`
    const common = { projectId: pid, projectNumber: first.projectNumber, projectName: first.projectName, referenceAvailable: true, ...summary(allProject), pageShown: shown.length, pageTaskIds: shown.map(row => row.id) }
    if (outer) out.push({ ...common, key: parent, parentKey: null, dimension: 'project', value: pid, label: first.projectName })
    out.push({ ...common, key: `${pid}:deliverable:${first.deliverableId}`, parentKey: outer ? parent : null, dimension: 'deliverable', value: first.deliverableId, label: 'D-001 Same deliverable label' })
  }
  return out
}

;(async () => {
  const originalFlags = { ...me.flags }, originalSettings = { ...me.settings }, { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], reads = [], details = [], writes = [], storedViews = []
  try {
    page.setDefaultTimeout(15000)
    me.flags = { ...Object.fromEntries(Object.keys(originalFlags).map(key => [key, false])), work_screens: true, live_events: true }
    me.settings.dateFormat = 'yyyy-MM-dd'
    page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => { const path = new URL(request.url()).pathname; if (path.startsWith('/api/') && ['POST', 'PATCH', 'PUT', 'DELETE'].includes(request.method())) writes.push({ path, method: request.method() }) })
    // Only the SSE transport is mocked here. Real useLiveEvents and keysForChange must refresh wtasks on Submission changes.
    await page.addInitScript(() => {
      const fetchNormally = window.fetch.bind(window)
      window.fetch = async (input, options) => {
        const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url, location.href)
        if (url.pathname !== '/api/v1/events') return fetchNormally(input, options)
        const stream = new ReadableStream({ start(controller) {
          window.__workColumnEvent = event => controller.enqueue(new TextEncoder().encode(`data: ${JSON.stringify(event)}\n\n`))
          options?.signal?.addEventListener('abort', () => { try { controller.close() } catch {} }, { once: true })
        } })
        return new Response(stream, { headers: { 'content-type': 'text/event-stream', 'cache-control': 'no-cache' } })
      }
    })
    await page.route('**/api/v1/projects?**', route => route.fulfill({ json: projectList(projects.map(p => projectRow(p))) }))
    await page.route('**/api/v1/tasks?**', route => {
      const url = new URL(route.request().url()), query = Object.fromEntries(url.searchParams), size = Number(query.pageSize) || 100, index = Number(query.page) || 1
      reads.push(query)
      for (const key of ['cols', 'view', 'panel', 'taskSection']) assert.equal(query[key], undefined, `${key} must not become an invented task predicate`)
      const rows = cohort(), shown = rows.slice((index - 1) * size, index * size)
      return route.fulfill({ json: taskPage(shown, { page: index, pageSize: size, totalCount: rows.length,
        ...(me.flags.work_screens && query.group === 'deliverable' ? { groups: groups(rows, shown), asOf: stamp } : {}) }) })
    })
    await page.route(/\/api\/v1\/tasks\/[0-9a-f-]+(?:\/dependencies)?(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname, taskId = path.split('/')[4], row = cohort().find(row => row.id === taskId)
      assert.ok(row); details.push(path)
      return route.fulfill({ json: path.endsWith('/dependencies') ? taskDependencies() : taskDetail({ task: row, project: { id: row.projectId, projectNumber: row.projectNumber, name: row.projectName, status: 'Active' }, permissions: taskPermissions({ transitions: [] }) }) })
    })
    await page.route(/\/api\/v1\/items\/Task\/[0-9a-f-]+\/(comments|links|activity)(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname
      if (path.endsWith('/activity')) return route.fulfill({ json: { items: [], page: 1, pageSize: 100, totalCount: 0 } })
      return route.fulfill({ json: path.endsWith('/comments') ? { ...comments, canComment: false, items: Array.from({ length: 3 }, (_, n) => ({ ...comments.items[0], id: id(9500 + n), body: `Column discussion ${n + 1}`, canEdit: false, canDelete: false, mentions: [] })) } : links })
    })
    await page.route('**/api/v1/views**', route => {
      const request = route.request()
      if (request.method() === 'GET') return route.fulfill({ json: viewList(storedViews) })
      assert.equal(request.method(), 'POST')
      const body = request.postDataJSON(), viewId = id(9300 + storedViews.length)
      assert.equal(body.listType, 'workspace-tasks'); assert.equal(body.scope, 'Personal'); assert.equal(body.projectId, null)
      const params = Object.fromEntries(Object.entries(body.params).filter(([key]) => !['panel', 'page', 'view', 'taskSection'].includes(key)))
      storedViews.push(savedView({ id: viewId, name: body.name, params }))
      return route.fulfill({ status: 201, json: viewSaved(viewId) })
    })
    const scope = projects.map(p => p.id).join(','), url = `${base}/tasks?projects=${scope}&group=deliverable&sort=key%3Aasc&q=Column`
    const table = () => page.locator('table:visible')
    const cell = (taskId, column) => page.locator(`tr:visible`).filter({ has: page.locator(`[data-workspace-column="key"] button`, { hasText: cohort().find(row => row.id === taskId).key }) }).locator(`[data-workspace-column="${column}"]`)
    const headers = () => table().locator('thead th')
    async function save(name) {
      await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
      const dialog = page.getByRole('dialog').last(); await dialog.getByLabel('Name', { exact: true }).fill(name); await dialog.getByRole('button', { name: 'Save', exact: true }).click()
      await dialog.waitFor({ state: 'detached' }); await page.waitForURL(next => next.searchParams.has('view'))
    }
    await page.goto(url); await page.getByRole('button', { name: twelve[0].name, exact: true }).waitFor()
    await page.evaluate(({ preference, projectPreference }) => { localStorage.removeItem(preference); localStorage.setItem(projectPreference, '["project-only-sentinel"]') }, { preference, projectPreference })
    await page.reload(); await page.getByRole('button', { name: twelve[0].name, exact: true }).waitFor()
    assert.equal(await headers().count(), 11)
    assert.equal(await table().locator('tbody tr:has(td)').count(), 12)
    assert.equal(await table().locator('[data-task-group]').count(), 6)
    assert.equal(new Set(await table().locator('[data-task-group]').evaluateAll(nodes => nodes.map(node => node.dataset.taskGroup))).size, 6)
    await page.getByRole('button', { name: 'Columns', exact: true }).click()
    for (const label of ['Deliverable', 'Reviewer', 'Est. hrs', 'Last activity', 'Submission date']) assert.equal(await page.getByRole('menuitemcheckbox', { name: label, exact: true }).count(), 1)
    await page.keyboard.press('Escape')
    assert.equal(details.length, 0, 'closed rows do not fetch per-item facts')
    await save('Default workspace columns')
    assert.deepEqual(storedViews[0].params.cols.split(','), defaults)
    assert.equal(storedViews[0].params.projects, scope); assert.equal(storedViews[0].params.group, 'deliverable'); assert.equal(storedViews[0].params.q, 'Column')

    await page.goto(`${url}&cols=deliverable,reviewer,estimate,lastActivity,submissionDate,reviewer,unknown`)
    await cell(twelve[0].id, 'submissionDate').getByText('2027-02-11', { exact: true }).waitFor()
    assert.equal(await headers().count(), 8)
    assert.deepEqual(await headers().allTextContents(), ['Key', 'Task', 'Project', 'Deliverable', 'Reviewer', 'Est. hrs', 'Last activity', 'Submission date'])
    assert.deepEqual(await table().locator('[data-task-group] th').evaluateAll(nodes => [...new Set(nodes.map(node => node.colSpan))]), [8])
    assert.match(await cell(twelve[0].id, 'estimate').innerText(), /^0(?:\s*h)?$/)
    await cell(twelve[1].id, 'estimate').getByText('Not estimated', { exact: true }).waitFor()
    assert.equal(await cell(twelve[0].id, 'reviewer').evaluate(el => {
      const copy = el.cloneNode(true); copy.querySelectorAll('[aria-hidden="true"]').forEach(node => node.remove())
      return copy.textContent.trim()
    }), 'Pat Reviewer')
    await cell(twelve[0].id, 'lastActivity').getByRole('group', { name: 'Last activity', exact: true }).waitFor()
    await save('Five optional work columns'); assert.deepEqual(storedViews[1].params.cols.split(','), eight)
    await page.evaluate(key => localStorage.setItem(key, '["key","name","project"]'), preference)
    await page.goto(url); await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Five optional work columns', exact: true }).click()
    await page.waitForURL(next => next.searchParams.get('cols') === eight.join(',')); await headers().filter({ hasText: 'Submission date' }).waitFor(); assert.equal(await headers().count(), 8)
    const beforeEventUrl = page.url(), beforeEventReads = reads.length
    await page.waitForFunction(() => typeof window.__workColumnEvent === 'function')
    targetDate = '2027-02-13'
    await page.evaluate(({ projectId, itemId }) => window.__workColumnEvent({ topic: 'project', itemType: 'SubmissionPackage', projectId, itemId }), { projectId: project.id, itemId: id(9399) })
    await cell(twelve[0].id, 'submissionDate').getByText('2027-02-13', { exact: true }).waitFor()
    assert.ok(reads.length > beforeEventReads, 'Submission event refreshes workspace rows'); assert.equal(page.url(), beforeEventUrl)

    mode = 'unknown'; await page.goto(`${url}&cols=${eight.join(',')}`)
    await cell(twelve[0].id, 'submissionDate').getByText('No open submission', { exact: true }).waitFor()
    await cell(twelve[1].id, 'submissionDate').getByText('Unavailable', { exact: true }).waitFor()
    await cell(twelve[2].id, 'submissionDate').getByText('Unavailable', { exact: true }).waitFor()
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 })
      if (width < 768) {
        const cards = page.locator('ul[aria-label="Tasks"]:visible')
        assert.equal(await cards.locator('[data-workspace-column="submissionDate"]').count(), 4)
        await cards.getByText('No open submission', { exact: true }).waitFor(); assert.equal(await cards.getByText('Unavailable', { exact: true }).count(), 2)
        assert.equal(await cards.locator('[data-workspace-column="reviewer"]').count(), 4)
      } else assert.equal(await headers().count(), 8)
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `no page overflow at${width}`)
    }

    mode = 'paged'; await page.setViewportSize({ width: 1280, height: 900 })
    await page.goto(`${base}/tasks?projects=${project.id}&group=deliverable&sort=key%3Aasc&q=Column&page=2&cols=${eight.join(',')}`)
    await page.getByRole('button', { name: paged[100].name, exact: true }).waitFor()
    const pageTwo = new URL(page.url())
    await page.getByRole('button', { name: 'Columns', exact: true }).press('Enter')
    const reviewer = page.getByRole('menuitemcheckbox', { name: 'Reviewer', exact: true }); await reviewer.focus(); await reviewer.press('Space')
    await page.waitForURL(next => !next.searchParams.get('cols').split(',').includes('reviewer')); await page.keyboard.press('Escape')
    const afterChoice = new URL(page.url()); for (const [key, value] of pageTwo.searchParams) if (key !== 'cols') assert.equal(afterChoice.searchParams.get(key), value)
    await headers().filter({ hasText: 'Reviewer' }).waitFor({ state: 'detached' })
    assert.equal(afterChoice.searchParams.get('page'), '2'); assert.equal(await headers().count(), 7)
    assert.deepEqual(JSON.parse(await page.evaluate(key => localStorage.getItem(key), preference)), eight.filter(key => key !== 'reviewer'))
    assert.equal(await page.evaluate(key => localStorage.getItem(key), projectPreference), '["project-only-sentinel"]')
    await page.reload(); await page.getByRole('button', { name: paged[100].name, exact: true }).waitFor(); assert.equal(await headers().count(), 7)
    await page.getByRole('button', { name: 'Next', exact: true }).click(); await page.waitForURL(next => next.searchParams.get('page') === '3')
    await page.getByRole('button', { name: paged[200].name, exact: true }).waitFor(); assert.equal(await table().locator('tbody tr:has(td)').count(), 1)
    await page.getByRole('button', { name: 'Previous', exact: true }).click(); await page.waitForURL(next => next.searchParams.get('page') === '2'); await page.getByRole('button', { name: paged[100].name, exact: true }).waitFor()
    await page.getByRole('button', { name: 'Columns', exact: true }).click(); await page.getByRole('menuitemcheckbox', { name: 'Comments', exact: true }).click(); await page.waitForURL(next => next.searchParams.get('cols')?.split(',').includes('comments')); await page.keyboard.press('Escape')
    const origin = page.url(); await page.locator(`[data-work-comments="${paged[100].id}"]:visible`).click()
    await page.waitForURL(next => next.searchParams.get('panel') === `Task:${paged[100].id}` && next.searchParams.get('taskSection') === 'comments')
    await page.getByRole('dialog').last().getByText('Column discussion 1', { exact: true }).waitFor()
    await page.getByRole('dialog').last().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    assert.equal(new URL(page.url()).searchParams.get('page'), '2')
    // A URL with no recognized IDs still retains all identity columns, regardless of stored defaults.
    mode = 'multi'; await page.goto(`${url}&cols=unknown,unknown`); await page.getByRole('button', { name: twelve[0].name, exact: true }).waitFor(); assert.equal(await headers().count(), 3)
    me.flags.work_screens = false
    await page.goto(`${url}&cols=${eight.join(',')}`); await page.getByRole('button', { name: twelve[0].name, exact: true }).waitFor()
    assert.equal(await headers().count(), 9); assert.equal(await page.getByRole('button', { name: 'Columns', exact: true }).count(), 0)
    assert.equal(await page.locator('[data-workspace-column="submissionDate"]').count(), 0); assert.equal(reads.at(-1).group, undefined)
    assert.deepEqual(writes.filter(write => write.path !== '/api/v1/views'), [], 'columns, reads and exact Comments return perform no business writes')
    assert.equal(writes.filter(write => write.path === '/api/v1/views').length, 2)
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ result: 'PASS', columns: five, projects: 3, tasks: 12, selected: 8, pages: [100, 100, 1], liveSubmissionRefresh: true, savedViews: storedViews.length, widths: [375, 768, 1280], off: 9 }))
  } finally { me.flags = originalFlags; me.settings = originalSettings; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
