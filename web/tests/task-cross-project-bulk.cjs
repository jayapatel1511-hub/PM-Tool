// Actual SPA + synthetic API. Coordinator runs; provider tests own canonical transaction/notification/ACL evidence.
const assert = require('node:assert/strict')
const { join } = require('node:path')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./task-cross-project-bulk.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui, source = f.state()
  const errors = [], unexpected = [], previews = [], writes = [], legacy = [], taskReads = [], receipts = new Map(), observations = [], undos = []
  let enabled = true, firstApply = true, releaseFirst, signalHeld
  const firstGate = new Promise(resolve => { releaseFirst = resolve }), firstHeld = new Promise(resolve => { signalHeld = resolve })
  const labels = { assign: 'Assign', setDueDate: 'Set due date', shiftDueDates: 'Shift due dates', setPriority: 'Set priority', setDeliverable: 'Set deliverable', hold: 'Put on hold', cancel: 'Cancel tasks' }
  const shellGets = new Set(['config', 'reference', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'])
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), name = url.pathname.replace('/api/v1/', ''), method = request.method()
      const problem = (status, detail) => route.fulfill({ status, contentType: 'application/problem+json', json: { status, code: 'invalid', detail } })
      if (method === 'GET') {
        if (name === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: f.actor(enabled) })
        if (name === 'tasks') { taskReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: f.page(source, url.searchParams) }) }
        if (name === 'projects') return route.fulfill({ json: f.projectList() })
        if (name === 'users') return route.fulfill({ json: f.people.filter(person => person.displayName.toLowerCase().includes((url.searchParams.get('q') ?? '').toLowerCase())) })
        if (name === 'users/' + f.departing + '/open-work') return route.fulfill({ json: f.openWork })
        const project = f.projects.find(p => name === 'projects/' + p.id || name === 'projects/' + p.projectNumber)
        if (project) return route.fulfill({ json: project })
        const deliverables = f.projects.find(p => name === 'projects/' + p.id + '/deliverables')
        if (deliverables) return route.fulfill({ json: f.deliverables(deliverables.id) })
        if (f.projects.some(p => name === 'projects/' + p.id + '/milestones')) return route.fulfill({ json: [] })
        const tasks = f.projects.find(p => name === 'projects/' + p.id + '/tasks')
        if (tasks) return route.fulfill({ json: f.page(source, new URLSearchParams({ ...Object.fromEntries(url.searchParams), projectId: tasks.id })) })
        if (shellGets.has(name)) return route.fallback()
        unexpected.push(method + ' ' + name); return problem(501, 'Unexpected GET')
      }
      if (method === 'POST' && name === 'me/sign-in') return route.fallback()
      if (method === 'POST' && name === 'tasks/bulk/preview') {
        assert.equal(enabled, true); const body = request.postDataJSON(); previews.push(body)
        assert.equal(body.schemaVersion, 1); assert.equal(body.operation, body.params.kind); assert.ok(body.items.length <= 200)
        const rows = f.resultRows(source, body), counts = f.counts(rows)
        return route.fulfill({ json: { rows, counts: { eligible: rows.filter(row => row.outcome === 'eligible').length, unchanged: counts.unchanged, refused: counts.refused, unavailable: counts.unavailable } } })
      }
      if (method === 'POST' && name === 'tasks/bulk/commands') {
        const body = request.postDataJSON(); writes.push(body); assert.equal(enabled, true)
        const previous = receipts.get(body.requestId)
        if (previous) {
          assert.equal(JSON.stringify(body), previous.body, 'Uncertain retry retains exact original body/request/versions')
          const rows = previous.answer.rows.map(row => source.hidden.has(row.taskId) ? { index: row.index, outcome: 'unavailable' } : row)
          return route.fulfill({ json: { ...previous.answer, rows, counts: f.counts(rows) } })
        }
        if (firstApply) source.rows[0].rowVersion++ // Real API-fixture race after preview; no client/cache/version promotion.
        const rows = f.resultRows(source, body.intent, true); source.commands++
        const answer = { commandId: f.id(9700 + source.commands), completedAt: '2026-11-14T14:00:00Z', rows, counts: f.counts(rows), warnings: [] }
        receipts.set(body.requestId, { body: JSON.stringify(body), answer })
        if (firstApply) {
          firstApply = false; source.hidden.add(source.rows[1].id); signalHeld()
          await firstGate
          return problem(503, 'Synthetic reply lost after commit')
        }
        return route.fulfill({ json: answer })
      }
      if (method === 'POST' && /^tasks\/bulk\/commands\/[^/]+\/undo$/.test(name)) {
        const commandId = name.split('/')[3]; undos.push(commandId)
        const original = [...receipts.values()].find(receipt => receipt.answer.commandId === commandId)?.answer
        assert.ok(original, 'Undo names a real command receipt'); const rows = original.rows.filter(row => row.outcome === 'changed')
        return route.fulfill({ json: { commandId: f.id(9900 + undos.length), completedAt: '2026-11-14T14:00:05Z', rows, counts: f.counts(rows), warnings: [] } })
      }
      if (method === 'POST' && name === 'projects/' + f.projects[0].id + '/tasks/bulk') { legacy.push(request.postDataJSON()); return route.fulfill({ json: { updated: 1, skipped: [], warnings: [] } }) }
      unexpected.push(method + ' ' + name); return problem(405, 'Unexpected command')
    })
    const goto = path => page.goto(base + path), toolbar = () => page.locator('[data-task-bulk-toolbar]'), dialog = () => page.locator('[data-task-bulk-dialog]')
    const select = row => page.getByRole('checkbox', { name: 'Select ' + row.key, exact: true }).check()
    const taskPageReady = async number => {
      await page.waitForURL(url => url.pathname === '/tasks' && Number(url.searchParams.get('page') ?? 1) === number)
      const expected = f.page(source, new URL(page.url()).searchParams).items
      assert.ok(expected.length > 0, 'Pagination scenario expects real task rows')
      const first = page.getByRole('checkbox', { name: 'Select ' + expected[0].key, exact: true, checked: false })
      await first.waitFor({ state: 'visible' })
      await page.getByRole('checkbox', { name: 'Select ' + expected.at(-1).key, exact: true }).waitFor({ state: 'visible' })
      assert.deepEqual(await page.getByRole('checkbox', { name: /^Select BULK-/ }).evaluateAll(nodes => nodes.map(node => node.getAttribute('aria-label'))), expected.map(row => 'Select ' + row.key), 'Fresh page has exactly its own task selection controls')
      await page.getByRole('checkbox', { name: 'Select this page', exact: true, checked: false }).waitFor({ state: 'visible' })
      assert.equal(await first.isChecked(), false, 'Fresh page first row is unselected before the next action')
    }
    const paginate = async (direction, number) => { await page.getByRole('button', { name: direction, exact: true }).click(); await taskPageReady(number) }
    const capture = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: join(process.env.TUESDAY_EVIDENCE_DIR, name + '.png') }) }
    const previewCounts = async (eligible, refused = 0) => {
      const sentence = eligible + ' eligible; 0 unchanged; ' + refused + ' refused; 0 unavailable.'
      const hint = 'Permissions and versions are checked again when you apply.'
      const status = dialog().getByRole('status').filter({ hasText: new RegExp('^' + eligible + ' eligible; 0 unchanged; ' + refused + ' refused; 0 unavailable\\.\\s+Permissions and versions are checked again when you apply\\.$') })
      await status.waitFor({ state: 'visible' })
      assert.equal((await status.textContent()).replace(/\s+/g, ' ').trim(), sentence + ' ' + hint, 'Exact preview counts and canonical hint share one status paragraph')
    }
    const review = async (eligible = 1) => { await dialog().getByRole('button', { name: 'Review changes', exact: true }).click(); await previewCounts(eligible) }
    const closeResult = async (changed = 1) => { await dialog().getByText(changed + ' changed; 0 unchanged; 0 refused; 0 unavailable.', { exact: true }).waitFor(); await dialog().locator('[data-slot="dialog-footer"]').getByRole('button', { name: 'Close', exact: true }).click(); await dialog().waitFor({ state: 'hidden' }) }
    const scroll = () => page.locator('#content').evaluate(node => ({ top: node.scrollTop, max: Math.max(0, node.scrollHeight - node.clientHeight) }))
    const focus = () => page.evaluate(() => { const node = document.activeElement; return { row: node?.getAttribute('data-task-bulk-return') ?? node?.getAttribute('data-task-id'), list: node?.hasAttribute('data-task-bulk-list'), connected: node?.isConnected, visible: !!node?.getClientRects().length } })

    // Create history with the real scope control; an old unsubmitted preview must die on actual Back/Forward.
    await goto('/tasks?projects=mine')
    await page.getByRole('button', { name: /^Project scope:/ }).click(); await page.getByRole('menuitem', { name: 'All permitted projects', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('projects') === 'all')
    await select(source.rows[90]); await toolbar().getByRole('button', { name: labels.setPriority, exact: true }).click()
    await dialog().getByRole('combobox', { name: 'Priority', exact: true }).selectOption('High'); await review()
    await page.goBack(); await page.waitForURL(url => url.searchParams.get('projects') === 'mine'); await dialog().waitFor({ state: 'hidden' })
    assert.equal(writes.length, 0); assert.equal(source.applied, 0); assert.equal(await toolbar().count(), 0)
    await page.goForward(); await page.waitForURL(url => url.searchParams.get('projects') === 'all')
    assert.equal(await dialog().count(), 0); assert.equal(await toolbar().count(), 0)
    observations.push('Actual scope control + Back/Forward invalidate an unsubmitted old preview, with no stale Apply or command')
    const originalPreviews = previews.length

    await taskPageReady(1)
    await page.getByRole('checkbox', { name: 'Select this page', exact: true }).check(); await toolbar().getByText('100 selected; 0 outside this page.', { exact: true }).waitFor()
    await paginate('Next', 2)
    await page.getByRole('checkbox', { name: 'Select this page', exact: true }).check(); await toolbar().getByText('200 selected; 100 outside this page.', { exact: true }).waitFor()
    await paginate('Next', 3)
    await page.getByRole('checkbox', { name: 'Select this page', exact: true }).click(); await toolbar().getByText('200 selected; 200 outside this page.', { exact: true }).waitFor()
    assert.equal(await page.getByRole('checkbox', { name: 'Select this page', exact: true }).isChecked(), false)
    await toolbar().getByRole('button', { name: 'Clear', exact: true }).click(); await toolbar().waitFor({ state: 'hidden' })
    for (const expected of [2, 1]) await paginate('Previous', expected)
    for (const row of source.rows.slice(0, 6)) await select(row)
    await paginate('Next', 2)
    for (const row of source.rows.slice(100, 102)) await select(row)
    await toolbar().getByText('8 selected; 6 outside this page.', { exact: true }).waitFor()
    await toolbar().getByRole('button', { name: labels.setPriority, exact: true }).click()
    await dialog().getByRole('combobox', { name: 'Priority', exact: true }).selectOption('High')
    await dialog().getByRole('button', { name: 'Review changes', exact: true }).click(); await previewCounts(6, 2)
    assert.equal(source.applied, 0); assert.equal(new Set(previews[originalPreviews].items.map(item => item.projectId)).size, 3); assert.ok(previews[originalPreviews].items.every(item => item.rowVersion === 0), 'New tasks retain their real observed zero versions')
    await page.setViewportSize({ width: 375, height: 800 }); await capture('task-bulk-phone-preview')
    const pendingUrl = page.url()
    await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click()
    let timer
    try { await Promise.race([firstHeld, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('First fixture command never became pending')), 15000) })]) } finally { clearTimeout(timer) }
    await page.goBack(); await page.locator('[data-task-bulk-dialog][data-bulk-navigation-blocked="1"]').waitFor(); await page.waitForURL(pendingUrl)
    assert.equal(writes.length, 1); assert.equal(source.commands, 1)
    releaseFirst()
    await dialog().getByRole('alert').getByText('Synthetic reply lost after commit', { exact: true }).waitFor()
    await dialog().getByRole('button', { name: 'Check the same batch', exact: true }).waitFor()
    await page.goBack(); await page.locator('[data-task-bulk-dialog][data-bulk-navigation-blocked="2"]').waitFor(); await page.waitForURL(pendingUrl)
    assert.equal(await dialog().getByRole('combobox', { name: 'Priority', exact: true }).isDisabled(), true); assert.equal(writes.length, 1)
    await dialog().getByRole('button', { name: 'Check the same batch', exact: true }).click(); await dialog().getByText('4 changed; 0 unchanged; 3 refused; 1 unavailable.', { exact: true }).waitFor()
    assert.equal(await dialog().getByText(source.rows[1].key, { exact: true }).count(), 0)
    assert.equal(await dialog().locator('[data-task-bulk-outcome="refused"]').count(), 3); assert.equal(await dialog().locator('[data-task-bulk-outcome="unavailable"]').count(), 1)
    assert.equal(source.applied, 5); assert.equal(writes.length, 2); assert.deepEqual(writes[0], writes[1]); assert.equal(source.commands, 1)
    await dialog().locator('[data-task-bulk-undo]').getByText('Undo', { exact: true }).waitFor(); assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    await capture('task-bulk-phone-result'); await dialog().locator('[data-slot="dialog-footer"]').getByRole('button', { name: 'Close', exact: true }).click(); await dialog().waitFor({ state: 'hidden' })
    assert.equal((await focus()).connected, true); assert.equal((await focus()).visible, true)
    observations.push('Pending and uncertain actual Back are cancelled; original request survives and identical retry sanitizes a now-hidden outcome')
    await paginate('Previous', 1)
    const chosen = source.rows[2]
    for (const op of ['assign', 'setDueDate', 'shiftDueDates', 'setDeliverable', 'hold', 'cancel']) {
      await select(chosen); await toolbar().getByRole('button', { name: labels[op], exact: true }).click()
      if (op === 'assign') { await dialog().locator('button[id$="-assignee"]').click(); await page.getByRole('option', { name: new RegExp(f.people[0].displayName) }).click() }
      if (op === 'setDueDate') await dialog().locator('input[type="date"]').fill('2026-12-20')
      if (op === 'shiftDueDates') await dialog().getByRole('spinbutton').fill('3')
      if (op === 'setDeliverable') await dialog().getByRole('combobox', { name: 'Deliverable for ' + chosen.projectNumber, exact: true }).selectOption(f.deliverables(chosen.projectId)[0].id)
      if (['shiftDueDates', 'hold', 'cancel'].includes(op)) await dialog().getByRole('textbox', { name: 'Reason', exact: true }).fill('Synthetic bulk reason')
      await review(); await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click(); await closeResult()
    }
    assert.equal(previews.length - originalPreviews, 7); assert.equal(writes.length, 8); assert.equal(source.commands, 7); assert.equal(source.applied, 11)
    assert.equal(writes.find(write => write.intent.operation === 'assign').intent.params.assigneeId, f.people[0].id)
    observations.push('All seven operations are distinct; assignment chooses an actual picker candidate rather than only clearing')

    // FR-SCR-20: the result offers Undo for 10 seconds; it posts the command's own ID and shows the reversal's outcome.
    await select(chosen); await toolbar().getByRole('button', { name: labels.setPriority, exact: true }).click()
    await dialog().getByRole('combobox', { name: 'Priority', exact: true }).selectOption('Low')
    await review(); await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click()
    await dialog().getByText('1 changed; 0 unchanged; 0 refused; 0 unavailable.', { exact: true }).waitFor()
    const undoneCommand = [...receipts.values()].at(-1).answer.commandId
    await dialog().locator('[data-task-bulk-undo]').click()
    await dialog().locator('[data-task-bulk-undone]').getByText('Undone 1; 0 refused.', { exact: true }).waitFor()
    assert.deepEqual(undos, [undoneCommand]); assert.equal(await dialog().locator('[data-task-bulk-undo]').count(), 0)
    await dialog().locator('[data-slot="dialog-footer"]').getByRole('button', { name: 'Close', exact: true }).click(); await dialog().waitFor({ state: 'hidden' })
    observations.push('Result Undo posts the exact command receipt ID once and replaces the result with its own outcome')

    // Different project-local choices must survive the request, not one shared ID/name guessed across projects.
    const mapped = [source.rows[3], source.rows[4]]
    for (const row of mapped) await select(row)
    await toolbar().getByRole('button', { name: labels.setDeliverable, exact: true }).click()
    for (const [index, row] of mapped.entries()) await dialog().getByRole('combobox', { name: 'Deliverable for ' + row.projectNumber, exact: true }).selectOption(f.deliverables(row.projectId)[index].id)
    await review(2); await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click(); await closeResult(2)
    const targets = writes.at(-1).intent.params.targets
    assert.deepEqual(targets.map(target => [target.projectId, target.deliverableId]), mapped.map((row, index) => [row.projectId, f.deliverables(row.projectId)[index].id]))
    observations.push('Different deliverable mappings for two projects reach the command with exact local IDs')

    // Far-down row + sticky toolbar give a genuine nonzero scroll origin; Close itself restores keyboard focus.
    const distant = source.rows[90]
    await select(distant); const distantScroll = await scroll(); assert.ok(distantScroll.top > 0)
    await toolbar().getByRole('button', { name: labels.setPriority, exact: true }).click(); await dialog().getByRole('combobox', { name: 'Priority', exact: true }).selectOption('High')
    await review(); await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click(); await closeResult()
    await page.waitForFunction(id => document.activeElement?.getAttribute('data-task-bulk-return') === id || document.activeElement?.getAttribute('data-task-id') === id, distant.id)
    const returned = await focus(); assert.equal(returned.row, distant.id); assert.equal(returned.visible, true); assert.ok(Math.abs((await scroll()).top - distantScroll.top) <= 2)
    observations.push('Completed Close restores a connected visible task control and actual nonzero scroll after refetch/removing toolbar')

    // A changed task disappears under the active filter; focus falls back to the persistent real list region.
    await goto('/tasks?projects=all&priority=Low'); await select(source.rows[20]); const filteredScroll = await scroll()
    await toolbar().getByRole('button', { name: labels.setPriority, exact: true }).click(); await dialog().getByRole('combobox', { name: 'Priority', exact: true }).selectOption('High')
    await review(); await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click(); await closeResult()
    await page.waitForFunction(() => document.activeElement?.hasAttribute('data-task-bulk-list') === true)
    const emptyFocus = await focus(); assert.equal(emptyFocus.list, true); assert.equal(emptyFocus.connected, true); assert.equal(emptyFocus.visible, true)
    const emptyScroll = await scroll(); assert.ok(Math.abs(emptyScroll.top - Math.min(filteredScroll.top, emptyScroll.max)) <= 2)
    assert.equal(await page.getByRole('button', { name: source.rows[20].name, exact: true }).count(), 0)
    observations.push('Filtered-out changed row returns focus to persistent list region with clamped scroll, never detached toolbar/body')

    await goto('/projects/' + f.projects[0].projectNumber + '/tasks')
    const projectTask = source.rows[10], expectedVersion = projectTask.rowVersion
    await select(projectTask); await toolbar().getByRole('button', { name: labels.setPriority, exact: true }).click(); await dialog().getByRole('combobox', { name: 'Priority', exact: true }).selectOption('High')
    await review(); await dialog().getByRole('button', { name: 'Apply changes', exact: true }).click(); await closeResult()
    assert.equal(writes.at(-1).intent.items[0].projectId, f.projects[0].id); assert.equal(writes.at(-1).intent.items[0].rowVersion, expectedVersion); assert.equal(legacy.length, 0)
    observations.push('Flag-on project list uses new versioned preview/command path')

    await goto('/people/' + f.departing + '/reassign-work'); await page.getByText('Synthetic departure drawing', { exact: true }).waitFor()
    await page.getByRole('link', { name: 'Open assigned tasks', exact: true }).click(); await page.waitForURL(url => url.pathname === '/tasks' && url.searchParams.get('assigneeId') === f.departing && url.searchParams.get('open') === 'true' && url.searchParams.get('projects') === 'all')
    await page.getByRole('button', { name: 'Synthetic bulk task 8', exact: true }).waitFor()
    assert.equal(source.rows[8].status, 'Complete'); assert.equal(source.rows[8].assigneeId, f.departing)
    assert.equal(await page.getByRole('button', { name: 'Synthetic bulk task 9', exact: true }).count(), 0)
    assert.ok(taskReads.some(query => query.assigneeId === f.departing && query.open === 'true'))
    observations.push('Departure destination honors assignee+open, excludes an actual closed task, and preserves non-task work on departure tool')

    enabled = false; await goto('/tasks?projects=all'); await page.getByRole('heading', { name: 'Tasks', exact: true }).waitFor()
    assert.equal(await page.getByRole('checkbox', { name: /Select BULK-/ }).count(), 0)
    await goto('/projects/' + f.projects[0].projectNumber + '/tasks'); await select(source.rows[0]); await page.getByRole('region', { name: 'Bulk actions', exact: true }).getByRole('button', { name: 'Set priority', exact: true }).click()
    const old = page.getByRole('dialog', { name: 'Set priority', exact: true }); await old.getByRole('button', { name: 'Confirm', exact: true }).click(); await old.waitFor({ state: 'hidden' })
    assert.equal(legacy.length, 1); assert.equal(legacy[0].operation, 'setPriority'); assert.equal(legacy[0].items, undefined)
    assert.equal(previews.length, 13); assert.equal(writes.length, 13); assert.equal(source.commands, 12); assert.equal(source.applied, 17); assert.equal(undos.length, 1)
    assert.deepEqual(errors, []); assert.deepEqual(unexpected, []); assert.deepEqual(ui.mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'task-cross-project-bulk', evidence: 'synthetic actual-SPA; provider tests own canonical transaction/receipt/notification/ACL', observations, previews: previews.length, attempts: writes.length, logicalCommands: source.commands, changedTasks: source.applied, legacyCommands: legacy.length }))
  } catch (error) {
    const diagnostics = await Promise.allSettled([
      page.locator('[data-task-bulk-toolbar]').allTextContents().then(text => text.map(value => value.slice(0, 500))),
      page.locator('[data-task-bulk-return]').evaluateAll(nodes => nodes.filter(node => node.getClientRects().length > 0).slice(0, 8).map(node => node.getAttribute('data-task-bulk-return'))),
    ])
    console.error(JSON.stringify({ status: 'FAIL', suite: 'task-cross-project-bulk', url: page.url(), query: new URL(page.url()).search, diagnostics, previews: previews.length, writes, unexpected, errors, observations })); throw error
  }
  finally { releaseFirst?.(); await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
