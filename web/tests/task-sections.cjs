// Actual SPA with strict synthetic transport. Provider tests own server history cohort/privacy proof.
const assert = require('node:assert/strict')
const { join } = require('node:path')
const { newMockPage } = require('./platform-fixtures.cjs')
const { timeView } = require('./time.fixture.cjs')
const f = require('./task-sections.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
;(async () => {
  const ui = await newMockPage(base, { events: false, nativeVisibility: true }), { page } = ui, source = f.world()
  const errors = [], unexpected = [], writes = [], blockAttempts = [], transitionAttempts = [], taskReads = [], historyReads = [], actorReads = [], timeReads = [], observations = []
  let historyFailure = 0, actorFailure = 0, taskFailure = 0, deniedTaskId = f.ids.task
  const shell = new Set(['config', 'reference', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'])
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), name = url.pathname.replace('/api/v1/', ''), method = request.method()
      const problem = (status, detail, code = 'invalid') => route.fulfill({ status, contentType: 'application/problem+json', json: { status, code, detail } })
      if (method === 'POST' && name === 'me/sign-in') return route.fallback()
      if (method === 'GET') {
        if (name === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: source.actor })
        if (name === 'projects') return route.fulfill({ json: source.projects })
        if (name === 'tasks') return route.fulfill({ json: source.tasks() })
        if (name === 'tasks/' + f.ids.task || name === 'tasks/' + f.ids.secondTask) {
          const detail = source.detail(name.split('/')[1])
          taskReads.push({ name, status: name === 'tasks/' + deniedTaskId ? taskFailure || 200 : 200, version: detail.task.rowVersion, onHoldAllowed: detail.permissions.transitions.find(transition => transition.to === 'On Hold')?.allowed })
          if (name === 'tasks/' + deniedTaskId && taskFailure) return problem(taskFailure, 'Synthetic background Task denial ' + taskFailure)
          return route.fulfill({ json: detail })
        }
        if (name === 'projects/' + f.ids.project || name === 'projects/' + source.project.projectNumber) return route.fulfill({ json: source.project })
        if (name === 'projects/' + f.ids.project + '/tasks') return route.fulfill({ json: source.tasks() })
        if (name === 'projects/' + f.ids.project + '/deliverables' || name === 'projects/' + f.ids.project + '/milestones') return route.fulfill({ json: [] })
        if (name === 'tasks/' + f.ids.task + '/dependencies' || name === 'tasks/' + f.ids.secondTask + '/dependencies') return route.fulfill({ json: source.dependencies })
        if (name === 'items/Task/' + f.ids.task + '/comments' || name === 'items/Task/' + f.ids.secondTask + '/comments') return route.fulfill({ json: source.comments })
        if (name === 'items/Task/' + f.ids.task + '/links' || name === 'items/Task/' + f.ids.secondTask + '/links') return route.fulfill({ json: source.links })
        if (name === 'tasks/' + f.ids.task + '/activity/actors' || name === 'tasks/' + f.ids.secondTask + '/activity/actors') {
          actorReads.push({ name, status: actorFailure || 200 }); assert.equal(source.flags.work_screens, true)
          return actorFailure ? problem(actorFailure, 'Synthetic current History actors are unavailable') : route.fulfill({ json: f.actors })
        }
        if (name === 'items/Task/' + f.ids.task + '/activity' || name === 'items/Task/' + f.ids.secondTask + '/activity') {
          historyReads.push({ task: name.split('/')[2], query: Object.fromEntries(url.searchParams), status: historyFailure || 200 })
          if (historyFailure) return problem(historyFailure, 'Synthetic current History is unavailable')
          return route.fulfill({ json: f.historyPage(url.searchParams) })
        }
        if (name === 'people/' + f.ids.pat + '/panel') return route.fulfill({ json: source.person(f.ids.pat, 'Synthetic Pat Reviewer') })
        if (name === 'people/' + f.ids.viewer + '/panel') return route.fulfill({ json: source.person(f.ids.viewer, source.actor.displayName) })
        if (name === 'time') { timeReads.push(Object.fromEntries(url.searchParams)); assert.notEqual(url.searchParams.get('scope'), 'all'); assert.equal(url.searchParams.get('taskId'), f.ids.task); return route.fulfill({ json: timeView(url.searchParams.get('from'), url.searchParams.get('to'), []) }) }
        if (shell.has(name)) return route.fallback()
        unexpected.push(method + ' ' + name); return problem(501, 'Unexpected GET')
      }
      if (method === 'POST' && name === 'tasks/' + f.ids.task + '/block') {
        const body = request.postDataJSON(); blockAttempts.push({ method, name, body })
        assert.deepEqual(body, { type: 'Client', reason: 'Synthetic retained block reason', rowVersion: 17 })
        assert.ok(source.parent().parentVersion >= 18); assert.notEqual(body.rowVersion, source.parent().parentVersion)
        return problem(409, 'Synthetic original Block version is stale', 'concurrency_conflict')
      }
      if (method === 'POST' && name === 'tasks/' + f.ids.secondTask + '/transition') {
        const body = request.postDataJSON(); transitionAttempts.push({ method, name, body })
        assert.deepEqual(body, { toStatus: 'On Hold', rowVersion: 17, reason: 'Synthetic retained transition reason', comment: null, reviewerId: null })
        assert.equal(source.detail(f.ids.secondTask).task.rowVersion, 19)
        assert.equal(source.detail(f.ids.secondTask).permissions.transitions.find(transition => transition.to === 'On Hold').allowed, true)
        return problem(409, 'Synthetic original transition version is stale', 'concurrency_conflict')
      }
      writes.push({ method, name, body: request.postDataJSON(), version: request.headers()['if-match'] })
      if (method === 'PATCH' && name === 'tasks/' + f.ids.task) {
        assert.equal(request.headers()['if-match'], '"17"'); assert.equal(request.postDataJSON().name, 'Synthetic retained failed name')
        return route.fulfill({ status: 409, contentType: 'application/problem+json', json: { status: 409, code: 'concurrency_conflict', detail: 'Synthetic inline conflict', currentRowVersion: 18, changedBy: 'Synthetic Pat', changedAt: f.stamp } })
      }
      unexpected.push(method + ' ' + name); return problem(405, 'Unexpected command')
    })
    const capture = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: join(process.env.TUESDAY_EVIDENCE_DIR, name + '.png') }) }
    const nav = () => page.getByRole('navigation', { name: 'Task sections', exact: true })
    const target = name => page.locator('#task-' + f.ids.task + '-' + name)
    const strip = async name => { await nav().getByRole('button', { name, exact: true }).click(); await target(name.toLowerCase()).waitFor(); assert.equal(await target(name.toLowerCase()).evaluate(node => document.activeElement === node), true) }
    const inspectorUrl = '/tasks?projects=all&q=Synthetic&panel=Task:' + f.ids.task
    await page.goto(base + inspectorUrl)
    await nav().waitFor(); await target('history').getByText('Synthetic history event 100', { exact: false }).waitFor()
    assert.deepEqual(await nav().getByRole('button').allTextContents(), ['Details', 'Dependencies', 'Review', 'Time', 'Comments', 'Links', 'History'])
    assert.equal(await page.locator('[data-task-section]').count(), 7)
    await target('dependencies').getByRole('button', { name: /Synthetic preceding design input/ }).waitFor()
    await target('review').getByText('Requires review', { exact: true }).waitFor()
    await target('comments').getByText(source.comments.items[0].body, { exact: true }).waitFor()
    await target('links').getByRole('link', { name: /Synthetic design reference/ }).waitFor()
    await target('time').getByRole('button', { name: '12.5', exact: true }).waitFor()
    assert.equal(await target('time').getByRole('slider').count(), 0)
    assert.equal(await target('details').getByRole('slider').count(), 1)
    const realUrl = page.url()
    for (const name of ['Dependencies', 'Review', 'Time', 'Comments', 'Links', 'History', 'Details']) await strip(name)
    assert.equal(page.url(), realUrl, 'Section scroll/focus uses no replacement URL or source-filter mutation')
    observations.push('Seven real, simultaneously mounted section bodies with one reviewer, one estimate, separate progress and actual own-Time link')

    // A refused real inline edit remains mounted while History query state changes.
    await target('details').getByRole('button', { name: 'Synthetic section task', exact: true }).click()
    await target('details').getByRole('textbox', { name: 'Name', exact: true }).fill('Synthetic retained failed name')
    await target('details').getByRole('textbox', { name: 'Name', exact: true }).press('Enter')
    await target('details').getByRole('button', { name: 'Synthetic retained failed name', exact: true }).waitFor()
    await strip('History')
    const controls = target('history').locator('[data-task-history-controls]')
    await controls.getByRole('combobox', { name: 'History person', exact: true }).selectOption(f.ids.pat)
    await controls.getByText('Page 1 of 2 · 125 entries', { exact: true }).waitFor()
    await controls.getByRole('button', { name: 'Next', exact: true }).click(); await controls.getByText('Page 2 of 2 · 125 entries', { exact: true }).waitFor()
    assert.ok(historyReads.some(read => read.query.actorId === f.ids.pat && read.query.page === '2'))
    assert.equal(new URL(page.url()).searchParams.get('taskHistoryActor'), f.ids.task + ':' + f.ids.pat)
    assert.equal(new URL(page.url()).searchParams.get('taskHistoryPage'), f.ids.task + ':2')
    assert.equal(await target('details').getByRole('button', { name: 'Synthetic retained failed name', exact: true }).count(), 1)
    assert.equal(writes.length, 1); assert.equal(writes[0].version, '"17"')
    const actor = target('history').locator('[data-person-panel-trigger="' + f.ids.pat + '"]').first()
    await actor.scrollIntoViewIfNeeded(); const actorId = await actor.getAttribute('id'), returnUrl = page.url()
    const sheet = page.getByRole('dialog').filter({ has: nav() }), beforeScroll = await sheet.evaluate(node => node.scrollTop)
    await actor.click(); await page.locator('[data-person-panel="' + f.ids.pat + '"]').waitFor()
    await page.getByRole('dialog').filter({ has: page.locator('[data-person-panel]') }).getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(returnUrl); await page.locator('[id="' + actorId + '"]').waitFor()
    await page.waitForFunction(id => document.activeElement?.id === id, actorId)
    assert.ok(Math.abs((await sheet.evaluate(node => node.scrollTop)) - beforeScroll) <= 2)
    assert.equal(await target('details').getByRole('button', { name: 'Synthetic retained failed name', exact: true }).count(), 1)
    observations.push('Server person/page filtering and exact History actor -> Person -> return preserve original inline conflict, focus, scroll and URL')

    // Inactive actors are offered from the full cohort. All includes a real System event on the last page.
    await controls.getByRole('combobox', { name: 'History person', exact: true }).selectOption(f.ids.inactive)
    await controls.getByText('Page 1 of 1 · 79 entries', { exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('taskHistoryPage'), f.ids.task + ':1')
    await controls.getByRole('combobox', { name: 'History person', exact: true }).selectOption('')
    await controls.getByText('Page 1 of 3 · 205 entries', { exact: true }).waitFor()
    for (const number of [2, 3]) { await controls.getByRole('button', { name: 'Next', exact: true }).click(); await controls.getByText('Page ' + number + ' of 3 · 205 entries', { exact: true }).waitFor() }
    await target('history').getByText('Synthetic history event 205', { exact: false }).waitFor()
    // Revalidate this SAME populated History query, with its successful actor cohort still cached.
    // Production main.tsx sets 15s freshness. Real tab visibility after 15.1s triggers its actual stale-query read.
    const populatedUrl = page.url(), other = await page.context().newPage()
    historyFailure = 403
    try {
      await other.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'hidden')
      await new Promise(resolve => setTimeout(resolve, 15100))
      const denied = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/items/Task/' + f.ids.task + '/activity' && response.status() === 403)
      await page.bringToFront(); await denied
    } finally { await other.close() }
    const assertHistoryDenied = async () => {
      await target('history').getByRole('alert').waitFor()
      const picker = controls.getByRole('combobox', { name: 'History person', exact: true })
      assert.equal(await picker.isDisabled(), true)
      assert.deepEqual(await picker.locator('option').allTextContents(), ['Unavailable'])
      assert.equal(await target('history').getByText('Synthetic Pat Reviewer', { exact: false }).count(), 0)
      assert.equal(await target('history').getByText('Synthetic Former Designer', { exact: false }).count(), 0)
      assert.equal(await target('history').locator('ol > li').count(), 0)
      assert.equal(await target('history').locator('[data-person-panel-trigger]').count(), 0)
      assert.equal(await controls.getByText(/^Page \d+ of \d+ · \d+ entries$/).count(), 0)
      assert.equal(await target('history').getByText('Synthetic history event 205', { exact: false }).count(), 0)
      assert.equal(await target('details').getByRole('button', { name: 'Synthetic retained failed name', exact: true }).count(), 1)
      assert.equal(page.url(), populatedUrl); assert.equal(writes.length, 1); assert.equal(writes[0].version, '"17"')
    }
    await target('history').getByText('Synthetic current History is unavailable', { exact: true }).waitFor(); await assertHistoryDenied()
    assert.ok(actorReads.some(read => read.status === 200), 'Real populated actor cohort existed before the History denial')
    assert.ok(historyReads.some(read => read.query.page === '3' && !read.query.actorId && read.status === 200), 'The exact denied History query had already populated its five actual last-page rows')
    historyFailure = 404
    const notFound = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/items/Task/' + f.ids.task + '/activity' && response.status() === 404)
    await target('history').getByRole('button', { name: 'Check current state', exact: true }).click(); await notFound
    await target('history').getByText('This page or item does not exist, or you cannot view it.', { exact: true }).waitFor(); await assertHistoryDenied()
    historyFailure = 0; actorFailure = 403
    const actorDenied = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/tasks/' + f.ids.task + '/activity/actors' && response.status() === 403)
    await target('history').getByRole('button', { name: 'Check current state', exact: true }).click(); await actorDenied
    await target('history').getByText('Synthetic current History actors are unavailable', { exact: true }).waitFor(); await assertHistoryDenied()
    actorFailure = 0; await target('history').getByRole('button', { name: 'Check current state', exact: true }).click()
    await controls.getByText('Page 3 of 3 · 205 entries', { exact: true }).waitFor()
    await target('history').getByText('Synthetic history event 205', { exact: false }).waitFor()
    assert.equal(page.url(), populatedUrl)
    observations.push('Inactive/full-cohort choices and System retain real totals/paging; same populated History403/404 and actor-query403 hide ALL cached choices/names/rows/counts, preserve dirty inline17 and URL, and explicitly recover')

    await strip('Review'); const stableUrl = page.url()
    await page.keyboard.press('Tab')
    const keyboardFocus = await page.locator(':focus').elementHandle()
    assert.ok(keyboardFocus); assert.equal(await keyboardFocus.evaluate(node => node.closest('[data-task-section]')?.getAttribute('data-task-section') === 'review'), true)
    await page.mouse.move(1250, 800); await page.mouse.wheel(0, 900)
    await page.waitForFunction(id => document.querySelector('[data-task-section-navigation="' + id + '"] [aria-current="location"]')?.textContent !== 'Review', f.ids.task)
    assert.equal(page.url(), stableUrl); assert.equal(await keyboardFocus.evaluate(node => node.isConnected && document.activeElement === node), true)
    await page.setViewportSize({ width: 375, height: 800 }); await strip('Comments'); await capture('task-sections-phone')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    assert.ok(await sheet.evaluate(node => node.scrollWidth <= node.clientWidth + 1))
    observations.push('Natural wheel changes the active section without route writes/focus stealing; phone strip targets and layout remain usable')

    // Explicit initial target belongs to the actual standalone Task and another task ignores old scoped History keys.
    await strip('Details'); await target('details').getByRole('button', { name: 'Take theirs', exact: true }).click()
    // 052 S02 (FR-VSN-11): with the visual system the standalone Task opens under the band and still owns its sections.
    source.flags.visual_system = true
    await page.goto(base + '/tasks/' + f.ids.task + '?taskSection=comments')
    await page.locator('.visual-page-band').getByRole('heading', { level: 1 }).waitFor()
    await target('comments').waitFor(); await page.waitForFunction(id => document.activeElement?.id === 'task-' + id + '-comments', f.ids.task)
    await strip('Time'); assert.equal(await nav().getByRole('button', { name: 'Time', exact: true }).getAttribute('aria-current'), 'location')
    source.flags.visual_system = false
    await page.goto(base + '/tasks/' + f.ids.task + '?taskSection=comments')
    await target('comments').waitFor(); await page.waitForFunction(id => document.activeElement?.id === 'task-' + id + '-comments', f.ids.task)
    await strip('Time'); const timeLink = target('time').getByRole('link', { name: 'Open my time for this task', exact: true })
    assert.equal(await timeLink.getAttribute('href'), '/time?taskId=' + f.ids.task)
    await timeLink.click(); await page.waitForURL(url => url.pathname === '/time' && url.searchParams.get('taskId') === f.ids.task)
    await page.getByRole('heading', { name: /Time/ }).first().waitFor(); assert.equal(timeReads.length > 0, true)
    await page.goto(base + '/tasks/' + f.ids.secondTask + '?taskHistoryActor=' + f.ids.task + ':' + f.ids.pat + '&taskHistoryPage=' + f.ids.task + ':3')
    await page.locator('[data-task-history-controls="' + f.ids.secondTask + '"]').getByText('Page 1 of 3 · 205 entries', { exact: true }).waitFor()
    assert.equal(historyReads.at(-1).query.actorId, undefined)
    source.flags.people_screens = false; source.flags.small_ux = false
    await page.goto(base + '/tasks/' + f.ids.task + '?taskSection=history')
    await target('history').getByRole('combobox', { name: 'History person', exact: true }).waitFor()
    assert.equal(await target('history').locator('[data-person-panel-trigger]').count(), 0)
    const beforeOffActors = actorReads.length; source.flags.work_screens = false
    await page.reload(); await page.getByRole('tab', { name: /Comments/ }).waitFor()
    assert.equal(await nav().count(), 0); assert.equal(await page.locator('[data-task-section]').count(), 0); assert.equal(actorReads.length, beforeOffActors)
    observations.push('Standalone explicit target, own Time query, task-scoped History state and independent work/people/small flags preserve legacy off behavior')

    // Actual portals already open when the same Task background query becomes unavailable.
    source.flags.work_screens = true; source.flags.people_screens = true; source.portalCommands(true); source.version(17)
    await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto(base + '/tasks/' + f.ids.task)
    await nav().waitFor()
    const taskBody = page.locator('[data-task-detail-body="' + f.ids.task + '"]'), taskError = page.locator('[data-task-detail-error="' + f.ids.task + '"]')
    const currentTaskUrl = page.url()
    const revalidateTask = async (taskId, status) => {
      deniedTaskId = taskId; taskFailure = status === 200 ? 0 : status
      const before = taskReads.length, other = await page.context().newPage()
      try {
        await other.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'hidden')
        // The canonical 15s query freshness window must really elapse; no focus/cache/visibility emulation.
        await new Promise(resolve => setTimeout(resolve, 15100))
        const denied = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/tasks/' + taskId && response.status() === status)
        await page.bringToFront(); await denied
      } finally { await other.close() }
      assert.ok(taskReads.slice(before).some(read => read.name === 'tasks/' + taskId && read.status === status))
    }
    const denyBackgroundTask = async status => {
      await revalidateTask(f.ids.task, status); await taskError.getByRole('alert').waitFor()
      assert.equal(await taskBody.isHidden(), true); assert.equal(await taskBody.getAttribute('inert'), ''); assert.equal(await taskBody.getAttribute('aria-hidden'), 'true')
      await page.getByRole('menu').waitFor({ state: 'hidden' })
      await page.getByRole('dialog', { name: 'Block S16-T001', exact: true }).waitFor({ state: 'hidden' })
      assert.equal(await page.getByRole('menuitem', { name: 'On Hold', exact: true }).count(), 0)
      assert.equal(await page.getByRole('button', { name: 'Set block', exact: true }).count(), 0)
      assert.equal(await page.getByRole('textbox', { name: 'Reason', exact: true }).count(), 0)
      assert.equal(await page.getByRole('button', { name: /^Change status/ }).count(), 0)
      assert.equal(writes.length, 1); assert.equal(blockAttempts.length, 0); assert.equal(page.url(), currentTaskUrl)
    }
    const recoverTask = async () => {
      taskFailure = 0; await taskError.getByRole('button', { name: 'Check current state', exact: true }).click()
      await taskBody.waitFor({ state: 'visible' }); assert.equal(page.url(), currentTaskUrl)
      assert.equal(writes.length, 1); assert.equal(blockAttempts.length, 0)
    }
    for (const status of [403, 404, 503]) {
      await taskBody.getByRole('button', { name: /^Change status/ }).click()
      await page.getByRole('menuitem', { name: 'On Hold', exact: true }).waitFor()
      await denyBackgroundTask(status); await recoverTask()
      assert.equal(await page.getByRole('menu').count(), 0, 'Unavailable status menu is dismissed and never auto-reopens from stale requested state')
    }
    await taskBody.getByRole('button', { name: 'Set block', exact: true }).click()
    const block = page.getByRole('dialog', { name: 'Block S16-T001', exact: true })
    await block.getByRole('textbox', { name: 'Reason', exact: true }).fill('Synthetic retained block reason')
    for (const [index, status] of [403, 404, 503].entries()) {
      await block.getByRole('button', { name: 'Set block', exact: true }).waitFor()
      await denyBackgroundTask(status)
      source.version(18 + index); await recoverTask()
      await block.waitFor(); assert.equal(await block.getByRole('textbox', { name: 'Reason', exact: true }).inputValue(), 'Synthetic retained block reason')
      assert.equal(blockAttempts.length, 0, 'Recovery never automatically confirms or replaces original modal intent')
    }
    await block.getByRole('button', { name: 'Set block', exact: true }).click()
    await block.getByText('Synthetic original Block version is stale', { exact: true }).waitFor()
    assert.equal(blockAttempts.length, 1); assert.equal(blockAttempts[0].body.rowVersion, 17)
    assert.equal(source.parent().parentVersion, 20); assert.equal(await block.getByRole('textbox', { name: 'Reason', exact: true }).inputValue(), 'Synthetic retained block reason')
    await block.getByRole('button', { name: 'Cancel', exact: true }).click(); await block.waitFor({ state: 'hidden' })
    observations.push('Already-open status menu and dirty Block portal disappear on actual background Task403/404/503; error read remains usable, no automatic commands; recovery retains reason and original17 against current20 and explicit stale attempt refuses409')

    // A successful readable Task is not write authority for its now-refused retained action.
    source.secondVersion(17); source.transitionPermission(true)
    await page.goto(base + '/tasks/' + f.ids.secondTask)
    const secondBody = page.locator('[data-task-detail-body="' + f.ids.secondTask + '"]'), secondError = page.locator('[data-task-detail-error="' + f.ids.secondTask + '"]')
    await secondBody.getByRole('button', { name: /^Change status/ }).click()
    await page.getByRole('menuitem', { name: 'On Hold', exact: true }).click()
    const moving = page.getByRole('dialog', { name: 'Move S16-T002 to On Hold?', exact: true }), secondUrl = page.url()
    await moving.getByRole('textbox', { name: 'Reason', exact: true }).fill('Synthetic retained transition reason')
    await revalidateTask(f.ids.secondTask, 403); await secondError.getByRole('alert').waitFor(); await moving.waitFor({ state: 'hidden' })
    assert.equal(transitionAttempts.length, 0)
    source.secondVersion(18); source.transitionPermission(false); taskFailure = 0
    const readRevocation = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/tasks/' + f.ids.secondTask && response.status() === 200)
    await secondError.getByRole('button', { name: 'Check current state', exact: true }).click(); await readRevocation
    await secondBody.waitFor({ state: 'visible' }); await moving.waitFor({ state: 'hidden' })
    assert.ok(taskReads.some(read => read.name === 'tasks/' + f.ids.secondTask && read.status === 200 && read.version === 18 && read.onHoldAllowed === false))
    assert.equal(await page.getByRole('button', { name: 'On Hold', exact: true }).count(), 0)
    assert.equal(transitionAttempts.length, 0); assert.equal(page.url(), secondUrl)
    await nav().getByRole('button', { name: 'Review', exact: true }).click()
    assert.equal(await page.locator('#task-' + f.ids.secondTask + '-review').evaluate(node => document.activeElement === node), true)
    await page.mouse.move(1250, 800); await page.mouse.wheel(0, 900)
    await page.waitForFunction(id => document.querySelector('[data-task-section-navigation="' + id + '"] [aria-current="location"]')?.textContent !== 'Review', f.ids.secondTask)
    assert.equal(page.url(), secondUrl); assert.equal(transitionAttempts.length, 0)
    await secondBody.getByRole('button', { name: /^Change status/ }).click()
    assert.equal(await page.getByRole('menuitem', { name: /^On Hold/ }).isDisabled(), true)
    assert.equal(await page.getByRole('menuitem', { name: /^Cancelled/ }).isDisabled(), true)
    await page.keyboard.press('Escape'); await page.getByRole('menu').waitFor({ state: 'hidden' })
    source.secondVersion(19); source.transitionPermission(true)
    await revalidateTask(f.ids.secondTask, 200)
    await moving.waitFor(); assert.equal(await moving.getByRole('textbox', { name: 'Reason', exact: true }).inputValue(), 'Synthetic retained transition reason')
    assert.equal(transitionAttempts.length, 0); assert.equal(page.url(), secondUrl)
    await moving.getByRole('button', { name: 'On Hold', exact: true }).click()
    await moving.getByText('Synthetic original transition version is stale', { exact: true }).waitFor()
    assert.equal(transitionAttempts.length, 1); assert.equal(transitionAttempts[0].body.rowVersion, 17)
    assert.equal(await moving.getByRole('textbox', { name: 'Reason', exact: true }).inputValue(), 'Synthetic retained transition reason')
    await moving.getByRole('button', { name: 'Cancel', exact: true }).click(); await moving.waitFor({ state: 'hidden' })
    observations.push('Denied current read followed by a successful current read-only Task cannot restore its held command; section click and wheel tracking remain usable; actual rights restoration keeps original reason/version17 and explicit stale attempt409 against19')
    assert.deepEqual(errors, []); assert.deepEqual(unexpected, []); assert.deepEqual(ui.mock.unknownGets, []); assert.equal(writes.length, 1); assert.equal(blockAttempts.length, 1); assert.equal(transitionAttempts.length, 1)
    console.log(JSON.stringify({ status: 'PASS', suite: 'task-sections', evidence: 'synthetic actual-SPA; provider tests own canonical cohort/privacy', observations, historyReads, actorReads: actorReads.length, timeReads, writes, blockAttempts, transitionAttempts, taskReads }))
  } catch (error) { console.error(JSON.stringify({ status: 'FAIL', suite: 'task-sections', url: page.url(), observations, writes, blockAttempts, transitionAttempts, taskReads, historyReads, unexpected, errors })); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
