// S10-A actual-app/mock-API acceptance: existing page facts, exact Comments selection, no closed-row reads or seen writes.
const assert = require('node:assert/strict')
const { newMockPage, me, project, links, comments, panelIds } = require('./platform-fixtures.cjs')
const { taskRow, taskPage, taskDetail, taskPermissions, taskPermission, taskState } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { myWork, myWorkPerson, myWorkProject } = require('./my-work.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use a local preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const now = Date.parse('2026-10-03T12:00:00Z'), stamp = '2026-10-03T10:00:00Z', pat = 'Pat Civil Lead'
const rows = Array.from({ length: 101 }, (_, i) => taskRow({ id: id(5100 + i), projectId: project.id, key: `P-MOCK-T${String(i + 1).padStart(3, '0')}`, name: `Synthetic activity work ${i + 1}`,
  projectDisciplineId: project.disciplines[0].id, assigneeId: me.id, assigneeName: me.displayName, dueDate: '2026-10-03', lastActivityAt: stamp, lastActivityActorName: pat,
  commentCount: i === 40 || i === 100 ? 3 : 0, can: { assign: false, edit: false, changeDue: false, block: false }, state: taskState({ isUnassigned: false, isMissingDueDate: false }) }))
const assigned = rows[100], scrollTask = rows[40]
const shared = taskRow({ ...assigned, id: id(5301), key: 'P-MOCK-T-COL', name: 'Collaborator activity work', assigneeId: id(5401), assigneeName: 'Diane Production Owner', collaboratorIds: [me.id], commentCount: 1 })
const review = taskRow({ ...assigned, id: id(5302), key: 'P-MOCK-T-REV', name: 'Reviewer activity work', assigneeId: id(5401), assigneeName: 'Diane Production Owner', reviewerId: me.id, reviewerName: me.displayName, requiresReview: true, status: 'Ready for Review', lane: 'Review', commentCount: 2 })
const completed = taskRow({ ...assigned, id: id(5303), key: 'P-MOCK-T-DONE', name: 'Completed activity work', status: 'Complete', lane: 'Done', completedAt: stamp, progressPct: 100, commentCount: 1 })
const all = [...rows, shared, review, completed]
let personMode = false, partial = false
const rowFor = (row, workRoute = false) => {
  const value = { ...row }
  if (!(me.flags.work_screens || !workRoute && me.flags.small_ux)) delete value.lastActivityActorName
  if (partial && row.id === assigned.id) { value.lastActivityAt = null; value.lastActivityActorName = null }
  return value
}

;(async () => {
  const savedFlags = { ...me.flags }, savedCapabilities = { ...me.capabilities }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], listQueries = [], detailReads = [], commentReads = [], historyReads = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/') && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) writes.push(`${request.method()} ${new URL(request.url()).pathname}`) })
  try {
    me.flags = Object.fromEntries(Object.keys(savedFlags).map(key => [key, false])); me.flags.work_screens = true
    await page.addInitScript(value => { Date.now = () => value }, now)
    await page.route('**/api/v1/tasks?**', route => {
      const url = new URL(route.request().url()); listQueries.push(Object.fromEntries(url.searchParams))
      const size = Number(url.searchParams.get('pageSize')) || 200, index = Number(url.searchParams.get('page')) || 1
      const workView = url.searchParams.has('mine') || url.searchParams.has('assigneeId') || url.searchParams.has('createdBy')
      const cohort = url.searchParams.get('status') === 'Complete' ? [completed] : workView ? [assigned] : rows
      return route.fulfill({ json: taskPage(cohort.slice((index - 1) * size, index * size).map(row => rowFor(row)), { page: index, pageSize: size, totalCount: cohort.length, sort: url.searchParams.get('sort') }) })
    })
    await page.route(`**/api/v1/projects/${project.id}/tasks?**`, route => {
      const url = new URL(route.request().url()); listQueries.push(Object.fromEntries(url.searchParams))
      return route.fulfill({ json: taskPage([rowFor(assigned)], { page: 1, pageSize: 200, totalCount: 1 }) })
    })
    await page.route('**/api/v1/me/work?**', route => route.fulfill({ json: myWork({ person: myWorkPerson(personMode ? id(5402) : me.id, personMode ? 'Other scoped person' : me.displayName), readOnly: personMode,
      tasks: [assigned, shared].map(row => rowFor(row, true)), reviews: { tasks: [rowFor(review, true)], deliverables: [] }, completed: [rowFor(completed, true)],
      projects: [myWorkProject({ id: project.id, projectNumber: project.projectNumber, name: project.name })] }) }))
    await page.route('**/api/v1/me/work', route => route.fulfill({ json: myWork({ person: myWorkPerson(me.id, me.displayName), tasks: [assigned, shared].map(row => rowFor(row, true)),
      reviews: { tasks: [rowFor(review, true)], deliverables: [] }, completed: [rowFor(completed, true)], projects: [myWorkProject({ id: project.id, projectNumber: project.projectNumber, name: project.name })] }) }))
    await page.route(/\/api\/v1\/tasks\/[0-9a-f-]+(?:\/dependencies)?(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname, taskId = path.split('/')[4]
      if (path.endsWith('/dependencies')) return route.fulfill({ json: taskDependencies() })
      detailReads.push(taskId)
      const row = all.find(r => r.id === taskId)
      assert.ok(row, `exact known task requested: ${taskId}`)
      return route.fulfill({ json: taskDetail({ task: rowFor(row), description: Array.from({ length: 12 }, (_, i) => `Synthetic descriptive paragraph ${i + 1}.`).join('\n\n'),
        relatedDeliverables: [{ id: panelIds.deliverable, key: 'D-001', name: 'Synthetic deliverable', status: 'In Review' }],
        permissions: taskPermissions({ edit: taskPermission(false), assign: taskPermission(false), setReviewer: taskPermission(false), dueDate: taskPermission(false), block: taskPermission(false), delete: taskPermission(false), restore: false, comment: false, dependencies: false, enterTime: false }) }) })
    })
    await page.route(/\/api\/v1\/items\/Task\/[0-9a-f-]+\/(comments|links|activity)(?:\?.*)?$/, route => {
      const path = new URL(route.request().url()).pathname, taskId = path.split('/')[5], kind = path.split('/')[6]
      const row = all.find(r => r.id === taskId)
      assert.ok(row, `exact known discussion task requested: ${taskId}`)
      if (kind === 'links') return route.fulfill({ json: links })
      if (kind === 'activity') { historyReads.push(taskId); return route.fulfill({ json: { items: [], page: 1, pageSize: 100, totalCount: 0 } }) }
      commentReads.push(taskId)
      return route.fulfill({ json: { ...comments, canComment: false, commentReason: 'Synthetic readonly discussion', items: Array.from({ length: row.commentCount }, (_, i) => ({ ...comments.items[0], id: id(5500 + i), body: `Exact ${row.key} discussion ${i + 1}`, deleted: false, editedAt: null, canEdit: false, canDelete: false, mentions: [] })) } })
    })
    const sheet = () => page.getByRole('dialog').last()
    const count = row => page.locator(`[data-work-comments="${row.id}"]:visible`).first()
    const activity = row => page.locator(`[data-work-activity="${row.id}"]:visible`).first()
    const workspace = `${base}/tasks?projects=${project.id}&status=Not+Started&sort=key%3Aasc&page=1`
    const expectComments = async (row, source) => {
      await page.waitForURL(url => url.searchParams.get('panel') === `Task:${row.id}` && url.searchParams.get('taskSection') === 'comments')
      await sheet().getByText(`Exact ${row.key} discussion 1`, { exact: true }).waitFor()
      assert.equal(await sheet().getByRole('tab', { name: /^Comments/ }).getAttribute('aria-selected'), 'true')
      const section = sheet().locator(`[data-work-comments-section="${row.id}"]`)
      await section.waitFor()
      await page.waitForFunction(taskId => document.activeElement?.getAttribute('data-work-comments-section') === taskId, row.id)
      assert.ok(await section.evaluate(node => { const surface = node.closest('[data-slot="sheet-content"]'), box = node.getBoundingClientRect(), bounds = surface.getBoundingClientRect(); return box.top >= bounds.top && box.top < bounds.bottom }), 'Comments target is actually in view')
      const before = new URL(source), current = new URL(page.url())
      assert.equal(current.pathname, before.pathname)
      for (const [key, value] of before.searchParams) assert.equal(current.searchParams.get(key), value, `retained source ${key}`)
    }

    await page.goto(workspace); await count(scrollTask).waitFor()
    assert.match(await activity(scrollTask).innerText(), /2\s*h(?:ours?)?\s*ago\s*·\s*Pat Civil Lead/i)
    assert.equal(await page.locator(`[data-work-comments="${rows[0].id}"]`).count(), 0, 'zero-comment task has no invented action')
    assert.deepEqual(detailReads, []); assert.deepEqual(commentReads, []); assert.deepEqual(historyReads, [])
    await count(scrollTask).scrollIntoViewIfNeeded(); await count(scrollTask).focus()
    await count(scrollTask).evaluate(node => node.addEventListener('click', () => { window.__commentSource = { top: document.querySelector('main#content').scrollTop } }, { once: true, capture: true }))
    await page.keyboard.press('Enter'); await expectComments(scrollTask, workspace)
    assert.deepEqual(commentReads, [scrollTask.id], 'one normal discussion read only after opening')
    assert.deepEqual(historyReads, [])
    assert.ok((await page.evaluate(() => window.__commentSource.top)) > 0, 'the source workspace list actually scrolled')
    await sheet().getByRole('tab', { name: 'History', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('panel') === `Task:${scrollTask.id}` && !url.searchParams.has('taskSection'))
    await sheet().getByRole('tab', { name: /^Comments/ }).click()
    await sheet().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(workspace)
    await page.waitForFunction(() => document.querySelector('main#content').scrollTop === window.__commentSource.top)
    await page.waitForFunction(taskId => document.activeElement?.getAttribute('data-work-comments') === taskId, scrollTask.id)

    await page.getByRole('button', { name: 'Next', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('page') === '2')
    await count(assigned).waitFor(); assert.equal(listQueries.at(-1).pageSize, '100'); assert.equal(listQueries.at(-1).page, '2')
    const secondPage = page.url(); await count(assigned).click(); await expectComments(assigned, secondPage)
    await sheet().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(secondPage)
    assert.equal(new URL(page.url()).searchParams.has('taskSection'), false)

    // Every TaskQueries-backed MyWork stream, including read-only other-person views, carries the same page facts.
    await page.goto(`${base}/my-work?tab=overview`); await count(assigned).waitFor(); await activity(assigned).waitFor(); await count(shared).waitFor(); await count(review).waitFor()
    for (const tab of ['today', 'upcoming', 'overdue', 'completed']) {
      const row = tab === 'completed' ? completed : assigned
      await page.goto(`${base}/my-work?tab=${tab}`); await count(row).waitFor(); await activity(row).waitFor()
      const origin = page.url(); await count(row).click(); await expectComments(row, origin)
      await sheet().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    }
    personMode = true; await page.goto(`${base}/my-work?userId=${id(5402)}`); await count(assigned).waitFor()
    const personOrigin = page.url(); await count(assigned).click(); await expectComments(assigned, personOrigin)
    assert.equal(await sheet().locator(`[data-work-comments-section="${assigned.id}"] textarea`).count(), 0)
    assert.equal(await sheet().locator(`[data-work-comments-section="${assigned.id}"] form`).count(), 0)
    await sheet().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(personOrigin); personMode = false

    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 }); await page.goto(secondPage); await count(assigned).waitFor()
      assert.ok(await page.locator('main#content').evaluate(node => node.scrollWidth <= node.clientWidth + 1), `workspace activity stays inside its table/card surface at ${width}`)
      const origin = page.url(); await count(assigned).click(); await expectComments(assigned, origin)
      assert.ok(await sheet().evaluate(node => node.scrollWidth <= node.clientWidth + 1), `Comments panel reflows at ${width}`)
      await sheet().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(origin)
    }
    await page.setViewportSize({ width: 1280, height: 900 })
    const projectUrl = `${base}/projects/${project.projectNumber}/tasks?group=none&cols=key,name,lastActivity&status=Not+Started`
    await page.goto(projectUrl); await count(assigned).waitFor(); await count(assigned).click(); await expectComments(assigned, projectUrl)
    await sheet().getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(projectUrl)

    // An ordinary record open removes the ephemeral Comments key; the existing small_ux related-record button exercises the host seam.
    me.flags.small_ux = true; await page.goto(secondPage); await count(assigned).click(); await expectComments(assigned, secondPage)
    await sheet().getByRole('button', { name: 'D-001 Synthetic deliverable', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('panel') === `Deliverable:${panelIds.deliverable}` && !url.searchParams.has('taskSection'))
    await sheet().getByRole('button', { name: 'Close', exact: true }).click()

    partial = true; await page.goto(secondPage); await activity(assigned).getByText('Time unavailable', { exact: true }).waitFor(); await activity(assigned).getByText('Actor unavailable', { exact: true }).waitFor()
    assert.equal((await activity(assigned).innerText()).includes('System'), false); partial = false
    for (const [small, work] of [[false, false], [true, false], [false, true], [true, true]]) {
      me.flags.small_ux = small; me.flags.work_screens = work; await page.goto(secondPage)
      await page.getByRole('heading', { name: /Tasks/ }).first().waitFor()
      if (work) { await count(assigned).waitFor(); await activity(assigned).waitFor() }
      else { assert.equal(await page.locator('[data-work-activity]').count(), 0); assert.equal(await page.locator('[data-work-comments]').count(), 0) }
    }
    // Raw URL selection is ignored while off, preserving the legacy Comments/History DOM path.
    me.flags.work_screens = false; await page.goto(`${secondPage}&panel=Task%3A${assigned.id}&taskSection=comments`)
    await sheet().getByRole('tab', { name: /^Comments/ }).waitFor(); assert.equal(await sheet().locator('[data-work-comments-section]').count(), 0)
    assert.ok(listQueries.every(query => !('taskSection' in query)), 'section selection never enters task endpoint filters/query')
    assert.deepEqual(writes, []); assert.deepEqual(errors, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: page facts and 101-row paging; all TaskQueries-backed MyWork streams/person-readonly; exact keyboard Comments path+section+scroll+focus return; ordinary-open cleanup; unknown facts; phone/table and1280 reflow; four flag combinations; no closed-row reads or seen/task writes')
  } finally { me.flags = savedFlags; me.capabilities = savedCapabilities; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
