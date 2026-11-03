// S13 actual-app/mock-API acceptance; normal timestamp/workflow/permissions are proved by companion API cases.
const assert = require('node:assert/strict')
const { newMockPage, me, project, comments, links } = require('/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/web/tests/platform-fixtures.cjs')
const { taskRow, taskPage, taskState, taskDetail, taskPermissions, taskTransition, taskTransitioned } = require('/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/web/tests/tasks.fixture.cjs')
const { taskDependencies } = require('/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/web/tests/dependencies.fixture.cjs')
const { projectList, projectRow } = require('/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/web/tests/projects.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const entered = '2026-09-28T12:00:00Z', now = '2026-10-04T12:00:00Z'
const lead = (name, n) => ({ state: 'Known', id: id(n), name })
const facts = (extra = {}) => ({ reviewRequestedAt: null, reviewWaitingDays: null, reviewStaleDays: 5, disciplineLead: lead('Marc Lead', 4101), projectManager: lead('Priya PM', 4102), ...extra })
const five = [0, 2, 3, 3, 4].map((estimatedHours, i) => taskRow({ id: id(4200 + i), projectId: project.id, key: `BOARD-T${i + 1}`, name: `Board header task ${i + 1}`,
  status: 'In Progress', lane: 'In Progress', assigneeId: me.id, assigneeName: me.displayName, estimatedHours, dueDate: i === 0 ? '2026-10-03' : '2026-10-20',
  board: facts(), state: taskState({ isUnassigned: false, isMissingDueDate: false, isOverdue: i === 0, daysOverdue: i === 0 ? 1 : 0 }) }))
const reviews = ['Ready for Review', 'In Review'].map((status, i) => taskRow({ ...five[1], id: id(4300 + i), key: `REVIEW-T${i + 1}`, name: `Waiting review ${i + 1}`, status, lane: 'Review',
  requiresReview: true, reviewerId: id(4103), reviewerName: 'Diane Reviewer', lastActivityAt: now, statusChangedAt: i ? now : entered,
  board: facts({ reviewRequestedAt: entered, reviewWaitingDays: 6 }) }))
const movable = taskRow({ ...five[1], id: id(4400), key: 'MOVE-T1', name: 'Normal guarded board move', status: 'Not Started', lane: 'To Do', rowVersion: 1 })
const second = taskRow({ ...five[2], id: id(4401), key: 'MOVE-T2', name: 'Existing lane task' })
const projects = [project, ...[1, 2].map(n => ({ ...project, id: id(4500 + n), projectNumber: `P-BOARD-${n}`, name: 'Same project name' }))]
let mode = 'five', allowMove = true, ordered = null
function cohort() {
  if (mode === 'move') { const rows = [movable, second]; return ordered ? ordered.map(value => rows.find(row => row.id === value)) : rows }
  if (mode === 'discipline') return [0, 1].map(n => taskRow({ ...five[n], id: id(4600 + n), projectDisciplineId: id(4610 + n), disciplineName: 'Same discipline name', board: facts({ disciplineLead: lead(`Lead ${n + 1}`, 4620 + n) }) }))
  if (mode === 'project') return projects.map((p, n) => taskRow({ ...five[n], id: id(4700 + n), projectId: p.id, projectNumber: p.projectNumber, projectName: p.name, board: facts({ projectManager: lead(`PM ${n + 1}`, 4710 + n) }) }))
  if (mode === 'unknown') return [
    { ...reviews[0], reviewerId: null, reviewerName: null, estimatedHours: null, state: null, board: facts({ reviewRequestedAt: entered, reviewWaitingDays: 5, disciplineLead: { state: 'Unassigned', id: null, name: null } }) },
    { ...reviews[1], reviewerName: null, estimatedHours: 0, board: facts({ reviewRequestedAt: entered, reviewWaitingDays: 4, disciplineLead: { state: 'Unavailable', id: id(4998), name: null } }) },
    { ...reviews[0], id: id(4997), name: 'Missing board read', board: undefined },
    { ...reviews[1], id: id(4996), name: 'Future review timestamp', board: facts({ reviewRequestedAt: '2026-10-05T12:00:00Z', reviewWaitingDays: null }) },
  ]
  if (mode === 'paged' || mode === 'truncated') return Array.from({ length: mode === 'paged' ? 201 : 501 }, (_, i) => taskRow({ ...five[1], id: id(5000 + i), key: `PAGE-${i + 1}`, name: `Loaded board task ${i + 1}`, estimatedHours: 1 }))
  if (mode === 'empty') return []
  return [...five, ...reviews]
}

;(async () => {
  const flags = { ...me.flags }, originalSettings = { ...me.settings }, originalStatus = project.status, { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], reads = [], writes = []
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...Object.fromEntries(Object.keys(flags).map(key => [key, false])), work_screens: true }
    me.settings.today = '2026-10-04'
    await page.addInitScript(value => { Date.now = () => value }, Date.parse(now))
    await page.route('**/api/v1/projects?**', route => route.fulfill({ json: projectList(projects.map(p => projectRow(p))) }))
    const list = route => {
      const query = Object.fromEntries(new URL(route.request().url()).searchParams); reads.push(query)
      const size = Number(query.pageSize) || 200, index = Number(query.page) || 1, rows = cohort()
      return route.fulfill({ json: taskPage(rows.slice((index - 1) * size, index * size), { page: index, pageSize: size, totalCount: rows.length }) })
    }
    await page.route('**/api/v1/tasks?**', list); await page.route(`**/api/v1/projects/${project.id}/tasks*`, list)
    await page.route(/\/api\/v1\/tasks\/[0-9a-f-]+(?:\/dependencies|\/transition)?(?:\?.*)?$/, route => {
      const request = route.request(), path = new URL(request.url()).pathname, taskId = path.split('/')[4], row = cohort().find(row => row.id === taskId)
      assert.ok(row)
      if (path.endsWith('/dependencies')) return route.fulfill({ json: taskDependencies() })
      if (path.endsWith('/transition')) {
        const body = request.postDataJSON(); writes.push({ path, body }); assert.equal(taskId, movable.id)
        assert.deepEqual(body, { toStatus: 'In Progress', rowVersion: 1, reason: null, comment: null, reviewerId: null })
        Object.assign(movable, { status: 'In Progress', lane: 'In Progress', rowVersion: 2 })
        return route.fulfill({ json: taskTransitioned(taskId, 'In Progress', 2) })
      }
      return route.fulfill({ json: taskDetail({ task: row, permissions: taskPermissions({ transitions: [taskTransition('In Progress', { allowed: allowMove, reason: allowMove ? null : 'Read-only board transition refused' })] }) }) })
    })
    await page.route(`**/api/v1/projects/${project.id}/board-order`, route => {
      assert.equal(route.request().method(), 'PUT'); const body = route.request().postDataJSON(); writes.push({ path: new URL(route.request().url()).pathname, body })
      assert.deepEqual(body.taskIds, [second.id, movable.id]); ordered = body.taskIds; return route.fulfill({ status: 204 })
    })
    await page.route(/\/api\/v1\/items\/Task\/[0-9a-f-]+\/(comments|links|activity)(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname
      return route.fulfill({ json: path.endsWith('/activity') ? { items: [], page: 1, pageSize: 100, totalCount: 0 } : path.endsWith('/comments') ? comments : links })
    })
    const workspace = `${base}/boards?projects=${project.id}&sort=dueDate`
    const projectUrl = `${base}/projects/${project.projectNumber}/board`
    const header = lane => page.locator(`[data-board-lane="${lane}"]`).first()
    await page.goto(workspace); await header('In Progress').getByText('12 h estimated · 0 unestimated', { exact: true }).waitFor()
    await header('In Progress').getByText('1 overdue', { exact: true }).waitFor(); assert.equal(await header('In Progress').locator('[aria-label="5 cards shown"]').count(), 1)
    for (const row of reviews) {
      const wait = page.locator(`[data-board-review-waiting="${row.id}"]`); assert.equal(await wait.innerText(), '▲ 6 days waiting'); assert.equal(await wait.getAttribute('data-board-review-stale'), 'true')
      assert.match(await wait.getAttribute('class'), /text-warn/); await page.locator(`[data-board-reviewer="${row.id}"]`).getByText('Diane Reviewer', { exact: true }).waitFor()
    }
    assert.equal(reads.at(-1).doneSince, '2026-09-20'); assert.equal(reads.at(-1).sort, 'dueDate')
    const origin = page.url(); await page.getByRole('button', { name: reviews[0].name, exact: true }).press('Enter')
    await page.waitForURL(url => url.searchParams.get('panel') === `Task:${reviews[0].id}`)
    await page.getByRole('dialog').last().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)

    mode = 'discipline'; await page.goto(`${projectUrl}?swim=discipline&sort=priority`)
    await page.getByText('Discipline Lead: Lead 1', { exact: true }).waitFor(); await page.getByText('Discipline Lead: Lead 2', { exact: true }).waitFor()
    assert.equal(await page.getByRole('heading', { name: /^Same discipline name/ }).count(), 2)
    mode = 'project'; await page.goto(`${base}/boards?projects=${projects.map(p => p.id).join(',')}&sort=dueDate&swim=project`)
    for (const n of [1, 2, 3]) await page.getByText(`Project Manager: PM ${n}`, { exact: true }).waitFor()
    assert.equal(await page.locator('[data-board-swimlane-facts]').count(), 3)

    mode = 'unknown'; await page.goto(workspace)
    await page.locator(`[data-board-reviewer="${reviews[0].id}"]`).getByText('Unassigned', { exact: true }).waitFor()
    await page.locator(`[data-board-reviewer="${reviews[1].id}"]`).getByText('Unavailable', { exact: true }).waitFor()
    assert.equal(await page.locator(`[data-board-review-waiting="${reviews[0].id}"]`).innerText(), '▲ 5 days waiting')
    assert.equal(await page.locator(`[data-board-review-waiting="${reviews[1].id}"]`).innerText(), '4 days waiting')
    assert.equal(await page.getByText('Waiting time unavailable', { exact: true }).count(), 2)
    await header('Review').getByText('4 h estimated · 1 unestimated', { exact: true }).waitFor(); await header('Review').getByText('0 overdue · 1 state unavailable', { exact: true }).waitFor()
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 }); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `no body overflow at${width}`)
      await page.locator(`[data-board-review-waiting="${reviews[0].id}"]`).scrollIntoViewIfNeeded(); await page.locator(`[data-board-review-waiting="${reviews[0].id}"]`).waitFor()
    }
    for (const pair of [[false, false], [false, true], [true, false], [true, true]]) {
      me.flags.work_screens = pair[0]; me.flags.screen_patterns = pair[1]; await page.goto(workspace); await page.getByRole('button', { name: reviews[0].name, exact: true }).waitFor()
      assert.equal(await page.locator('[data-board-review-waiting]').count(), pair[0] ? 4 : 0)
      assert.equal(await page.locator('[data-board-totals]').count(), pair[0] ? 4 : 0)
    }
    me.flags.work_screens = true; me.flags.screen_patterns = false; await page.setViewportSize({ width: 1440, height: 1000 })
    for (const [kind, shown, total] of [['paged', 201, 201], ['truncated', 500, 501]]) {
      mode = kind; const start = reads.length; await page.goto(workspace)
      await header('In Progress').getByText(`${shown} h estimated · 0 unestimated`, { exact: true }).waitFor()
      assert.equal(await header('In Progress').locator(`[aria-label="${shown} cards shown"]`).count(), 1)
      assert.ok(reads.length - start >= (total > 400 ? 3 : 2)); await page.locator('[data-board-figures-scope]').getByText(/shown cards only/).waitFor()
      if (total > shown) await page.getByText(/501 cards; the first 500 are shown/).waitFor()
    }
    mode = 'empty'; await page.goto(workspace); await page.getByText('No tasks match these projects and filters.', { exact: true }).waitFor(); assert.equal(await page.locator('main#content').getByRole('listitem').count(), 0)

    // Existing keyboard activation/cancel, guarded normal drop and manual order payloads remain real app interactions.
    mode = 'move'; await page.goto(`${projectUrl}?sort=board`)
    const handle = () => page.getByRole('button', { name: new RegExp(`^Move ${movable.key} `) })
    await handle().focus(); await handle().press('Space'); await page.keyboard.press('Escape')
    assert.equal(writes.length, 0)
    await handle().scrollIntoViewIfNeeded(); const from = await handle().boundingBox(), target = await page.getByRole('list', { name: 'In Progress', exact: true }).boundingBox()
    assert.ok(from && target)
    const moved = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/transition'))
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2); await page.mouse.down(); await page.mouse.move(from.x + from.width / 2 + 10, from.y + from.height / 2, { steps: 3 })
    await page.mouse.move(target.x + target.width / 2, target.y + Math.min(100, target.height / 2), { steps: 20 }); await page.mouse.up()
    await page.waitForTimeout(500)
    console.log('DRAG_DIAGNOSTIC', JSON.stringify({from,target,writes,errors,apiErrors:mock.apiErrors,unknownGets:mock.unknownGets,dialogs:await page.getByRole('dialog').allTextContents(),live:await page.locator('[aria-live]').allTextContents(),handles:await page.getByRole('button',{name:/^Move MOVE-/}).evaluateAll(nodes=>nodes.map(el=>({label:el.getAttribute('aria-label'),pressed:el.getAttribute('aria-pressed'),rect:el.getBoundingClientRect().toJSON()}))),body:(await page.locator('body').innerText()).slice(-5000)}))
    await page.screenshot({path:'/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/specs/053-screen-completeness/evidence/checkpoint21-runtime/board-drag-diagnostic.png',fullPage:true})
    await moved; await header('In Progress').locator('[aria-label="2 cards shown"]').waitFor(); assert.equal(writes.length, 1)
    const reordered = page.waitForResponse(response => response.request().method() === 'PUT' && response.url().endsWith('/board-order'))
    await handle().focus(); await handle().press('Space'); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Space')
    await reordered
    assert.equal(writes.length, 2); assert.deepEqual(writes[1].body, { taskIds: [second.id, movable.id] })
    assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(mock.unknownGets, [])
    console.log(JSON.stringify({ result: 'PASS', lane: { cards: 5, hours: 12, overdue: 1 }, reviewDays: [4, 5, 6], groups: ['discipline', 'project'], flags: 4, loaded: [201, 500], total: 501, keyboard: true, normalTransition: true, manualOrder: true, widths: [375, 768, 1280] }))
  } finally { me.flags = flags; me.settings = originalSettings; project.status = originalStatus; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
