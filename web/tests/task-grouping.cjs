// S11-A actual-app/mock-API checks: full-source summaries, visible-page membership, normal commands and unchanged-off behavior.
const assert = require('node:assert/strict')
const { newMockPage, me, project, comments, links, panelIds } = require('./platform-fixtures.cjs')
const { taskRow, taskPage, taskDetail, taskPermissions, taskTransition, taskState, taskTransitioned } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { myWork, myWorkPerson, myWorkProject } = require('./my-work.fixture.cjs')
const { projectList, projectRow } = require('./projects.fixture.cjs')
const { deliverableList, deliverableRow } = require('./deliverables.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-10-03T12:00:00Z', civil = project.disciplines[0].id
const projects = [project, ...[1, 2].map(n => ({ ...project, id: id(7300 + n), projectNumber: `P-MOCK-${n}`, name: 'Same project label' }))]
const four = [10, 10, 15, 5].map((estimatedHours, i) => taskRow({ id: id(7000 + i), projectId: project.id, key: `P-MOCK-T${i + 1}`, name: `Grouped task ${i + 1}`,
  assigneeId: me.id, assigneeName: me.displayName, projectDisciplineId: civil, deliverableId: panelIds.deliverable, deliverableKey: 'D-001', deliverableName: 'Synthetic deliverable', estimatedHours,
  status: i < 2 ? 'Complete' : 'In Progress', progressPct: i < 2 ? 100 : 0, dueDate: i === 2 ? '2026-10-02' : '2026-10-09', completedAt: i < 2 ? stamp : null,
  lastActivityAt: stamp, state: taskState({ isUnassigned: false, isMissingDueDate: false, isOverdue: i === 2, daysOverdue: i === 2 ? 1 : 0 }) }))
const paged = Array.from({ length: 201 }, (_, i) => taskRow({ ...four[3], id: id(7400 + i), key: `P-MOCK-T${String(i + 1).padStart(3, '0')}`, name: `Paged group work ${i + 1}`, estimatedHours: 1 }))
let mode = 'four'
function cohort() {
  if (mode.startsWith('recovery-')) return [{ ...four[3], deliverableId: mode === 'recovery-null' ? null : mode === 'recovery-unavailable' ? id(7990) : panelIds.deliverable,
    deliverableKey: mode === 'recovery-linked' ? 'D-001' : null, deliverableName: mode === 'recovery-linked' ? 'Synthetic deliverable' : null }]
  if (mode === 'paged' || mode === 'drift') return [...paged]
  if (mode === 'multi') return projects.map((p, i) => taskRow({ ...four[3], id: id(7800 + i), projectId: p.id, projectNumber: p.projectNumber, projectName: p.name, projectDisciplineId: id(7900 + i), disciplineName: 'Civil', deliverableId: null, deliverableKey: null, deliverableName: null }))
  if (mode === 'zero') return four.map((row, i) => ({ ...row, estimatedHours: 0, state: i === 2 ? null : row.state }))
  if (mode === 'mixed') return [...four.map((row, i) => ({ ...row, estimatedHours: i === 1 ? null : row.estimatedHours })), { ...four[3], id: id(7999), key: 'T-CANCEL', name: 'Selected cancelled task', estimatedHours: 8, status: 'Cancelled' }]
  return four
}
function keyValue(row, by) { return by === 'deliverable' ? row.deliverableId : by === 'milestone' ? row.effectiveMilestoneId : by === 'assignee' ? row.assigneeId : by === 'status' ? row.status : by === 'due' ? row.state?.isOverdue ? 'overdue' : 'later' : row.projectDisciplineId }
function summary(rows) {
  const live = rows.filter(row => row.status !== 'Cancelled'), weighted = live.every(row => row.estimatedHours != null), complete = live.filter(row => row.status === 'Complete')
  const denominator = weighted ? live.reduce((sum, row) => sum + row.estimatedHours, 0) : live.length
  const numerator = weighted ? complete.reduce((sum, row) => sum + row.estimatedHours, 0) : complete.length
  const open = rows.filter(row => !['Complete', 'Cancelled'].includes(row.status))
  return { rowCount: rows.length, estimatedHours: rows.reduce((sum, row) => sum + (row.estimatedHours ?? 0), 0), unestimatedCount: rows.filter(row => row.estimatedHours == null).length,
    overdueCount: rows.filter(row => row.state?.isOverdue).length, unavailableStateCount: rows.filter(row => !row.state).length,
    progressPct: !live.length || !denominator ? null : Math.floor(numerator * 20 / denominator) * 5, progressBasis: !live.length ? 'NoTasks' : weighted ? 'Hours' : 'Count', progressTaskCount: live.length,
    nextDueDate: open.map(row => row.dueDate).filter(Boolean).sort()[0] ?? null, undatedOpenCount: open.filter(row => !row.dueDate).length }
}
function groups(rows, shown, by) {
  const outer = new Set(rows.map(row => row.projectId)).size > 1, out = []
  for (const pid of [...new Set(shown.map(row => row.projectId))]) {
    const allProject = rows.filter(row => row.projectId === pid), visibleProject = shown.filter(row => row.projectId === pid), p = allProject[0], parent = `${pid}:project`
    if (outer) out.push({ key: parent, parentKey: null, dimension: 'project', projectId: pid, projectNumber: p.projectNumber, projectName: p.projectName, value: pid, label: p.projectName, referenceAvailable: true, ...summary(allProject), pageShown: visibleProject.length, pageTaskIds: visibleProject.map(row => row.id) })
    for (const value of [...new Set(visibleProject.map(row => keyValue(row, by)))]) {
      const allGroup = allProject.filter(row => keyValue(row, by) === value), visible = visibleProject.filter(row => keyValue(row, by) === value)
      out.push({ key: `${pid}:${by}:${value ?? 'none'}`, parentKey: outer ? parent : null, dimension: by, projectId: pid, projectNumber: p.projectNumber, projectName: p.projectName, value,
        label: by === 'discipline' ? 'Civil' : by === 'deliverable' && value && mode !== 'recovery-unavailable' ? 'D-001 Synthetic deliverable' : null,
        referenceAvailable: mode !== 'recovery-unavailable', ...summary(allGroup), pageShown: visible.length, pageTaskIds: visible.map(row => row.id) })
    }
  }
  return out
}

;(async () => {
  const flags = { ...me.flags }, originalCreateTaskIn = project.permissions.createTaskIn, { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], reads = [], mutations = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/') && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) mutations.push({ method: request.method(), path: new URL(request.url()).pathname }) })
  try {
    me.flags = { ...Object.fromEntries(Object.keys(flags).map(key => [key, false])), work_screens: true }
    await page.addInitScript(value => { Date.now = () => value }, Date.parse(stamp))
    await page.route('**/api/v1/projects?**', route => route.fulfill({ json: projectList(projects.map(p => projectRow(p))) }))
    await page.route(`**/api/v1/projects/${project.id}/deliverables*`, route => route.fulfill({ json: deliverableList([deliverableRow({ id: panelIds.deliverable, projectId: project.id,
      key: 'D-001', name: 'Synthetic deliverable', projectDisciplineId: civil, ownerId: me.id, status: 'In Progress' })]) }))
    async function list(route) {
      const url = new URL(route.request().url()); reads.push(Object.fromEntries(url.searchParams))
      const by = url.searchParams.get('group'), size = Number(url.searchParams.get('pageSize')) || 200, index = Number(url.searchParams.get('page')) || 1
      let rows = cohort()
      if (mode === 'drift' && index > 1) rows.push({ ...paged[0], id: id(7777), key: 'P-MOCK-T202', name: 'New live cohort task' })
      if (url.searchParams.has('open')) rows = rows.filter(row => !['Complete', 'Cancelled'].includes(row.status))
      const from = url.searchParams.get('dueFrom'); if (from) rows = rows.filter(row => row.dueDate && row.dueDate >= from)
      const status = url.searchParams.get('status'); if (status) rows = rows.filter(row => status.split(',').includes(row.status))
      const shown = rows.slice((index - 1) * size, index * size)
      return route.fulfill({ json: taskPage(shown, { page: index, pageSize: size, totalCount: rows.length, ...(me.flags.work_screens && by && by !== 'none' && mode !== 'partial' ? { groups: groups(rows, shown, by), asOf: stamp } : {}) }) })
    }
    await page.route('**/api/v1/tasks?**', list); await page.route(`**/api/v1/projects/${project.id}/tasks?**`, list)
    await page.route('**/api/v1/me/work', route => route.fulfill({ json: myWork({ person: myWorkPerson(me.id, me.displayName), tasks: [four[2], four[3]], completed: four.slice(0, 2), projects: [myWorkProject(project)] }) }))
    await page.route(/\/api\/v1\/tasks\/[0-9a-f-]+(?:\/dependencies|\/transition)?$/, async route => {
      const request = route.request(), path = new URL(request.url()).pathname, taskId = path.split('/')[4], row = [...four, ...paged].find(row => row.id === taskId)
      assert.ok(row)
      if (path.endsWith('/dependencies')) return route.fulfill({ json: taskDependencies() })
      if (path.endsWith('/transition')) {
        const body = request.postDataJSON(); writes.push({ taskId, body })
        assert.equal(taskId, four[3].id); assert.equal(body.toStatus, 'Complete'); assert.equal(body.rowVersion, 1)
        Object.assign(four[3], { status: 'Complete', lane: 'Done', progressPct: 100, rowVersion: 2, completedAt: stamp })
        return route.fulfill({ json: { ...taskTransitioned(taskId, 'Complete', 2), progressPct: 100 } })
      }
      return route.fulfill({ json: taskDetail({ task: row, permissions: taskPermissions({ transitions: row.id === four[3].id ? [taskTransition('Complete')] : [] }) }) })
    })
    await page.route(/\/api\/v1\/items\/Task\/[0-9a-f-]+\/(comments|links)$/, route => route.fulfill({ json: new URL(route.request().url()).pathname.endsWith('/comments') ? comments : links }))
    const workspace = `${base}/tasks?group=discipline&projects=${project.id}&sort=key%3Aasc`
    const header = () => page.locator('[data-task-group]:visible').filter({ has: page.locator('[data-task-group-summary]') }).first()
    await page.goto(workspace); await header().getByText('50% by hours', { exact: true }).waitFor()
    await header().getByText('4 shown / 4 matched tasks', { exact: true }).waitFor(); await header().getByText('1 overdue', { exact: true }).waitFor()
    await header().getByText(/40 h estimated across matched rows · 0 unestimated/).waitFor()
    const origin = page.url(); await page.getByRole('button', { name: four[2].name, exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('panel') === `Task:${four[2].id}`)
    await page.getByRole('dialog').last().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    mode = 'paged'; await page.reload(); await header().getByText('100 shown / 201 matched tasks', { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Next', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('page') === '2'); await header().getByText('100 shown / 201 matched tasks', { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Next', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('page') === '3'); await header().getByText('1 shown / 201 matched tasks', { exact: true }).waitFor()
    await page.getByRole('button', { name: /^Group/ }).click(); await page.getByRole('menuitemradio', { name: 'Milestone', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('group') === 'milestone' && !url.searchParams.has('page'))
    assert.equal(reads.at(-1).projects, project.id)
    const projectUrl = `${base}/projects/${project.projectNumber}/tasks?group=discipline&sort=key%3Aasc&cols=key,name,estimate`
    await page.goto(projectUrl); await header().getByText('201 shown / 201 matched tasks', { exact: true }).waitFor()
    mode = 'drift'; await page.reload(); await page.getByText(/Group changed between page reads/).waitFor(); await page.locator('[data-task-group-unavailable]').first().waitFor()
    mode = 'multi'; await page.goto(`${base}/tasks?group=deliverable&projects=${projects.map(p => p.id).join(',')}`)
    await page.locator('[data-task-group]:visible').filter({ hasText: 'Other tasks' }).first().waitFor()
    const keys = await page.locator('[data-task-group]:visible').evaluateAll(nodes => nodes.map(node => node.dataset.taskGroup))
    assert.equal(keys.length, 6); assert.equal(new Set(keys).size, 6)
    mode = 'zero'; await page.goto(workspace); await header().getByText('Progress unavailable (hours)', { exact: true }).waitFor(); await header().getByText(/0 known overdue · 1 state unavailable/).waitFor()
    mode = 'mixed'; await page.goto(`${workspace}&status=Complete,In+Progress,Cancelled`); await header().getByText('50% by count · excludes Cancelled', { exact: true }).waitFor(); await header().getByText(/38 h estimated across matched rows · 1 unestimated/).waitFor()
    mode = 'partial'; await page.goto(workspace); await page.getByText('Full filtered group totals unavailable', { exact: true }).waitFor(); assert.equal(await page.locator('[data-task-group-summary]').count(), 0)
    // FR-SCR-16: each My Work mount (not its tabs, filters or pages) records one visit.
    const visitPath = '/api/v1/me/work/visits', visit = { method: 'POST', path: visitPath }
    const visited = () => page.waitForRequest(request => request.method() === 'POST' && new URL(request.url()).pathname === visitPath)
    let visitSent = visited()
    mode = 'four'; await page.goto(`${base}/my-work?tab=overview`); await page.locator('[data-task-group="mywork:overdue"]').getByText('1 shown / 1 matched tasks', { exact: true }).waitFor(); await visitSent
    mode = 'paged'; const mine = `${base}/my-work?tab=upcoming&who=assigned&projects=${project.id}&group=discipline`
    visitSent = visited(); await page.goto(mine); await header().getByText('200 shown / 201 matched tasks', { exact: true }).waitFor(); await visitSent; await page.getByRole('navigation', { name: 'Grouped task pages' }).getByRole('button', { name: 'Next', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('page') === '2'); await header().getByText('1 shown / 201 matched tasks', { exact: true }).waitFor(); assert.equal(reads.at(-1).assigneeId, me.id); assert.equal(reads.at(-1).projects, project.id)
    mode = 'four'; visitSent = visited(); await page.goto(mine); await visitSent; await page.getByRole('checkbox', { name: new RegExp(four[3].key) }).click(); await page.waitForFunction(() => !document.querySelector('[role="checkbox"][aria-checked="false"]'))
    assert.equal(writes.length, 1); assert.deepEqual(mutations, [visit, visit, visit, { method: 'POST', path: `/api/v1/tasks/${four[3].id}/transition` }]); await page.goto(workspace); await header().getByText('60% by hours', { exact: true }).waitFor()
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 }); await page.goto(workspace); await header().waitFor()
      assert.ok(await page.locator('main#content').evaluate(node => node.scrollWidth <= node.clientWidth + 1), `group headers stay inside list/card surface at ${width}`)
      await page.goto(projectUrl); await header().waitFor(); assert.ok(await header().evaluate(node => node.scrollWidth <= node.clientWidth + 1), `project header wraps at ${width}`)
    }
    // Exact missing-source recovery: unavailable is not the known no-deliverable bucket.
    project.permissions.createTaskIn = [civil]
    const deliverableUrl = `${base}/projects/${project.projectNumber}/tasks?group=deliverable&cols=key,name,estimate`
    for (const width of [375, 1280]) {
      await page.setViewportSize({ width, height: 900 })
      mode = 'recovery-unavailable'; await page.goto(deliverableUrl)
      const unavailable = page.locator('[data-task-group]:visible').filter({ hasText: 'Group reference unavailable' }).first()
      await unavailable.waitFor(); assert.equal(await unavailable.getByRole('button', { name: 'Add task', exact: true }).count(), 0)
      assert.equal(await page.getByRole('dialog').count(), 0)
      for (const [nextMode, expected] of [['recovery-linked', panelIds.deliverable], ['recovery-null', '']]) {
        mode = nextMode; await page.reload(); await header().waitFor()
        await header().getByRole('button', { name: 'Add task', exact: true }).click()
        const form = page.getByRole('dialog').last(); await form.locator('#t-del').waitFor()
        await form.locator(`#t-del option[value="${expected}"]`).waitFor({ state: 'attached' })
        assert.equal(await form.locator('#t-del').inputValue(), expected, `known group create context at ${width}`)
        assert.equal(await form.locator('#t-disc').inputValue(), civil, `normal discipline context at ${width}`)
        await form.getByRole('button', { name: 'Cancel', exact: true }).click()
      }
    }
    project.permissions.createTaskIn = originalCreateTaskIn; mode = 'four'
    assert.deepEqual(mutations, [visit, visit, visit, { method: 'POST', path: `/api/v1/tasks/${four[3].id}/transition` }], 'recovery inspection opens forms without writes')
    me.flags.work_screens = false; await page.goto(workspace); await page.getByRole('heading', { name: /^Tasks/ }).first().waitFor()
    assert.equal(await page.locator('[data-task-group]').count(), 0); assert.equal('group' in reads.at(-1), false)
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: full4task40h50% group;201acrosspages; actualProject/null hierarchy; zero/missing/count/cancelled/partial truths; infinite-page drift; MyWork fullgroup pager/membership; normal guarded Complete; exact record return; unavailable Add here refused while known linked/null forms retain context at375/1280;375/768/1280 headers and unchanged-off')
  } finally { me.flags = flags; project.permissions.createTaskIn = originalCreateTaskIn; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
