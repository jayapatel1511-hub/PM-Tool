// Packet 051 S07 (FR-BKL-06, FR-BKL-09, AC-BKL-04, AC-BKL-06): My Work -> No date, against a self-started production preview and the
// mocked platform API. Flag off: the view menu has no No date, ?tab=nodate opens Today and asks for no Backlog list. Flag on: No date sits
// between Overdue and Completed, lists the person's Backlog across projects with each project and the rule, asks the task list for
// mine + backlog, is a saved view like the others, and Today no longer lists a blocked Backlog task.
const assert = require('node:assert/strict')
const { startPreview, newMockPage, me, work, task, project } = require('./platform-fixtures.cjs')

const port = Number(process.env.BACKLOG_TEST_PORT ?? 7260)
const projects = ['P-ALPHA', 'P-BRAVO', 'P-CHARLIE'].map((projectNumber, i) => ({ id: `00000000-0000-0000-0000-0000000001${i}0`, projectNumber, name: `Synthetic ${projectNumber}` }))
const row = (id, name, extra = {}) => ({
  ...task, id: `00000000-0000-0000-0000-00000000020${id}`, key: `T-20${id}`, name, projectId: project.id, projectNumber: project.projectNumber, projectName: project.name,
  disciplineName: 'Civil', progressPct: 0, lastActivityAt: '2026-10-01T12:00:00Z', ...extra,
})
const backlogRows = projects.map((p, i) => row(i + 1, `Backlog item ${p.projectNumber}`, { projectId: p.id, projectNumber: p.projectNumber, projectName: p.name, backlog: true }))
const today = row(5, 'Due today task', { dueDate: '2026-10-03', backlog: false })
const blockedBacklog = row(6, 'Blocked backlog task', {
  backlog: true, state: { ...task.state, isBlocked: true, isWaiting: true, blockedBy: [{ type: 'manual', name: 'Other', reason: 'Waiting on the client' }] },
})

async function run(base, on) {
  const { browser, page, mock } = await newMockPage(base)
  const errors = []; const taskGets = []; const saved = []
  page.on('pageerror', error => errors.push(error.message))
  // The flag decides whether the server sends the backlog row field (FR-BKL-09), so the mock follows it.
  const strip = (r) => { const { backlog, ...rest } = r; return on ? r : rest }
  await page.route('**/api/v1/me', route => route.fulfill({ json: { ...me, flags: { ...me.flags, backlog: on } } }))
  await page.route('**/api/v1/me/work', route => route.fulfill({ json: { ...work, tasks: [today, blockedBacklog, ...backlogRows].map(strip) } }))
  await page.route(/\/api\/v1\/tasks\?/, route => {
    const url = new URL(route.request().url())
    taskGets.push(url.searchParams)
    route.fulfill({ json: { items: url.searchParams.get('backlog') === 'true' ? backlogRows : [], page: 1, pageSize: 200, totalCount: url.searchParams.get('backlog') === 'true' ? backlogRows.length : 0 } })
  })
  await page.route(/\/api\/v1\/views(\?|$)/, async route => {
    const request = route.request()
    if (request.method() === 'POST') { saved.push(request.postDataJSON()); return route.fulfill({ status: 201, json: { id: 'view-nodate' } }) }
    route.fulfill({ json: { canShare: false, views: saved.length ? [{ id: 'view-nodate', name: saved[0].name, scope: 'Personal', isDefault: false, rowVersion: 1, params: saved[0].params, dropped: [], canEdit: true }] : [] } })
  })
  const views = page.getByRole('button', { name: /^My Work views/ })
  const menu = async () => { await views.click(); const items = await page.getByRole('menuitemradio').allInnerTexts(); await page.keyboard.press('Escape'); return items.map(x => x.trim()) }
  try {
    // Today (My Tasks): the dated task is there; a blocked Backlog task is not (it belongs to No date), but with the flag off it still is.
    await page.goto(`${base}/my-work`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Due today task', { exact: true }).waitFor({ state: 'visible', timeout: 20000 })
    assert.equal(await page.getByText('Blocked backlog task', { exact: true }).count(), on ? 0 : 1, `flag ${on}: Today ${on ? 'leaves out' : 'still lists'} a blocked Backlog task`)
    assert.equal(await page.getByText('Backlog item P-ALPHA', { exact: true }).count(), 0, `flag ${on}: undated work is not in Today`)

    const items = await menu()
    const expected = ['Today', 'Upcoming', 'Overdue', ...(on ? ['No date'] : []), 'Completed', 'Inbox', 'Coordination', 'Overview']
    assert.deepEqual(items, expected, `flag ${on}: view menu`)

    if (!on) {
      // AC-BKL-06: the view does not exist; the unknown tab opens Today and nothing asks for a Backlog list.
      await page.goto(`${base}/my-work?tab=nodate`, { waitUntil: 'domcontentloaded' })
      await page.getByText('Due today task', { exact: true }).waitFor({ state: 'visible', timeout: 20000 })
      assert.equal(await page.getByText(/Backlog rule/).count(), 0, 'flag off: no rule text')
      assert.equal(taskGets.filter(p => p.get('backlog')).length, 0, 'flag off: no Backlog request')
      assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`)
      assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
      return { flag: on, requests: taskGets.length }
    }

    // AC-BKL-04: every Backlog item with its project, the rule once, and the same task list the other views read.
    await views.click()
    await page.getByRole('menuitemradio', { name: 'No date', exact: true }).click()
    await page.waitForURL(/tab=nodate/)
    for (const r of backlogRows) await page.getByText(r.name, { exact: true }).waitFor({ state: 'visible', timeout: 20000 })
    assert.equal(await page.locator('ul li').filter({ hasText: 'Backlog item' }).count(), 3, 'three Backlog items across three projects')
    for (const p of projects) assert.equal(await page.locator('li', { hasText: `Backlog item ${p.projectNumber}` }).getByText(p.projectNumber, { exact: true }).count(), 1, `${p.projectNumber} is shown on its row`)
    await page.getByText('Backlog rule: open tasks with no due date and no start date', { exact: false }).waitFor({ state: 'visible' })
    const request = taskGets.filter(p => p.get('backlog') === 'true').at(-1)
    assert.ok(request, 'the No date view asks the task list for the Backlog')
    assert.equal(request.get('mine'), 'true', 'My Tasks membership: assignee or collaborator')
    assert.equal(request.get('dueFrom'), null, 'no due-date window')
    assert.equal(await page.getByText('Due today task', { exact: true }).count(), 0, 'dated work is not in No date')
    assert.ok(await page.getByRole('heading', { name: 'No date' }).count() > 0, 'the section is titled No date')

    // The membership switch keeps the view: Assigned to Me asks for the assignee instead.
    await Promise.all([page.waitForResponse(res => /backlog=true/.test(res.url()) && /assigneeId=/.test(res.url())),
      page.getByRole('button', { name: 'Assigned to Me', exact: true }).click()])
    assert.match(page.url(), /tab=nodate/)
    assert.match(page.url(), /who=assigned/)

    // A saved view like the others (FR-BKL-06): saved with the tab, applied from the plain page, reading current data.
    await page.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: /Save current view/ }).click()
    await page.getByLabel('Name').fill('My undated work')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await page.waitForFunction(() => /view=view-nodate/.test(location.search))
    assert.equal(saved.length, 1)
    assert.equal(saved[0].listType, 'mywork')
    assert.equal(saved[0].params.tab, 'nodate', 'the saved view keeps the No date tab')
    const before = taskGets.length
    await page.goto(`${base}/my-work`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Due today task', { exact: true }).waitFor({ state: 'visible', timeout: 20000 })
    await page.getByRole('button', { name: 'Views', exact: true }).click()
    await page.getByRole('menuitem', { name: 'My undated work' }).click()
    await page.waitForURL(/tab=nodate/)
    await page.getByText('Backlog item P-ALPHA', { exact: true }).waitFor({ state: 'visible', timeout: 20000 })
    assert.ok(taskGets.length > before, 'opening the saved view reads the list again')
    assert.deepEqual(errors, [], `browser errors: ${errors.join('; ')}`)
    assert.deepEqual(mock.unknownGets, [], `unhandled mocked GETs: ${mock.unknownGets.join('; ')}`)
    return { flag: on, requests: taskGets.length }
  } finally { await browser.close() }
}

;(async () => {
  const preview = await startPreview(port)
  try {
    const results = [await run(preview.base, false), await run(preview.base, true)]
    console.log(JSON.stringify({ result: 'PASS', results }))
  } finally { preview.child.kill() }
})().catch(error => { console.error(error); process.exitCode = 1 })
