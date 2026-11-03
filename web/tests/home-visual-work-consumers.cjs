// Narrow supplemental052 consumer acceptance. All records/commands are mocked synthetic fixtures.
const assert = require('node:assert/strict')
const { newMockPage, project, task, me, panelIds } = require('./platform-fixtures.cjs')
const { meView } = require('./me.fixture.cjs')
const { taskRow, taskState, taskPage, taskDetail, taskPermissions } = require('./tasks.fixture.cjs')
const { deliverableRow, deliverableState } = require('./deliverables.fixture.cjs')
const { milestoneRow } = require('./milestones.fixture.cjs')
const { projectTimeline, ganttPage } = require('./timeline.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const did = id(1300), mid = panelIds.milestone, pd = project.disciplines[0].id
const rows = [
  ['Not Started', 'To Do', 0, '2026-10-29'], ['In Progress', 'In Progress', 25, '2026-11-03'],
  ['Ready for Review', 'Review', 100, '2026-11-04'], ['Complete', 'Done', 100, '2026-10-29'],
  ['On Hold', 'On Hold', 40, '2026-10-29'], ['In Review', undefined, 100, '2026-11-06'], ['In Progress', 'Invented', 20, '2026-11-07'],
].map(([status, lane, progressPct, dueDate], n) => taskRow({ id: n === 0 ? task.id : id(1200 + n), projectId: project.id, key: `P-MOCK-T${String(n + 1).padStart(4, '0')}`,
  name: `Synthetic visual task ${n + 1}`, projectDisciplineId: pd, status, lane, progressPct, dueDate, startDate: '2026-10-20',
  originalStartDate: n === 1 ? '2026-10-18' : null, originalDueDate: n === 1 ? '2026-11-01' : null,
  completedAt: status === 'Complete' ? '2026-10-30T13:00:00Z' : null,
  assigneeId: me.id, assigneeName: 'Alex Chen', requiresReview: n === 2 || n === 5,
  reviewerId: n === 2 ? id(1400) : null, reviewerName: n === 2 ? 'Synthetic independent reviewer' : null,
  deliverableId: did, deliverableKey: 'P-MOCK-D0001', deliverableName: 'Synthetic visual deliverable',
  state: taskState({ isUnassigned: false, isMissingDueDate: false, isOverdue: n === 0, daysOverdue: n === 0 ? 2 : 0, isHeldPastDue: n === 4 }),
}))
const data = projectTimeline({ discipline: pd, task: task.id, deliverable: did, milestone: mid, handoff: id(1302) })
data.latestPlanned = null; data.handoffs = []
data.tasks = rows.map(row => ({ id: row.id, key: row.key, name: row.name, deliverableId: did, projectDisciplineId: pd, milestoneId: null,
  startDate: row.startDate, dueDate: row.dueDate, originalStartDate: row.originalStartDate, originalDueDate: row.originalDueDate,
  status: row.status, lane: row.lane, progressPct: row.progressPct, assignee: row.assigneeName, rowVersion: 1, createdOn: '2026-10-18',
  isOverdue: row.state.isOverdue, canEditDates: true, dueNeedsReason: false, slipShown: true }))
data.deliverables[0] = { ...data.deliverables[0], key: 'P-MOCK-D0001', name: 'Synthetic visual deliverable', startDate: '2026-10-20', dueDate: '2026-10-30',
  originalStartDate: '2026-10-18', originalDueDate: '2026-10-28', baselineStartDate: '2026-10-19', baselineDueDate: '2026-10-29', progressPct: 60, isOverdue: true }
const delivery = deliverableRow({ id: did, projectId: project.id, projectDisciplineId: pd, key: 'P-MOCK-D0001', name: data.deliverables[0].name,
  status: 'In Progress', startDate: '2026-10-20', dueDate: '2026-10-30', originalStartDate: '2026-10-18', originalDueDate: '2026-10-28',
  state: deliverableState({ progressPct: 60, isOverdue: true }) })
const ms = milestoneRow({ id: mid, projectId: project.id, key: 'P-MOCK-M0001', name: 'Synthetic visual milestone', date: '2026-11-01', originalDate: '2026-10-29', baselineDate: '2026-10-29', baselineSlipShown: true })
data.milestones = [{ ...data.milestones[0], key: ms.key, name: ms.name }]
data.dependencies = [{ id: id(1303), predecessorTaskId: rows[0].id, successorTaskId: rows[1].id, type: 'FinishToStart', lagDays: 0, highlighted: true }]

;(async () => {
  const { page, browser } = await newMockPage(base, { events: false })
  page.setDefaultTimeout(8000)
  const errors = [], patches = []
  page.on('pageerror', e => errors.push(e.message))
  let enabled = true
  const bar = row => page.locator(`[data-visual-timeline-bar="${row.id}"]`)
  const openLegend = async () => {
    await page.getByRole('button', { name: 'Legend', exact: true }).click()
    return page.locator('[data-slot="popover-content"][aria-label="Legend"]')
  }
  const closePanel = async () => {
    // SheetContent appends its built-in X after the panel children, including any footer Close action.
    await page.locator('[data-slot="sheet-content"]').getByRole('button', { name: 'Close', exact: true }).last().click()
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
  }
  const cancelDrag = async row => {
    await bar(row).scrollIntoViewIfNeeded()
    const before = await bar(row).evaluate(el => ({ left: el.style.left, width: el.style.width, height: el.style.height }))
    const box = await bar(row).boundingBox()
    await page.mouse.move(box.x + Math.min(40, box.width / 2), box.y + box.height / 2)
    await page.mouse.down(); await page.mouse.move(box.x + Math.min(40, box.width / 2) + 28, box.y + box.height / 2, { steps: 6 }); await page.mouse.up()
    const dialog = page.getByRole('dialog', { name: `Move ${row.key} ${row.name}?`, exact: true })
    await dialog.getByText('Moves by +1 days:', { exact: true }).waitFor()
    await dialog.getByText("Only this item's dates change. Successors and other items are not moved.", { exact: true }).waitFor()
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click()
    await dialog.waitFor({ state: 'hidden' })
    assert.deepEqual(await bar(row).evaluate(el => ({ left: el.style.left, width: el.style.width, height: el.style.height })), before)
    assert.deepEqual(patches, [], 'cancel never persists a date or reschedules another record')
  }
  const checkTimeline = async () => {
    const expected = ['rgb(148, 160, 180)', 'rgb(106, 82, 209)', 'rgb(240, 164, 43)', 'rgb(47, 111, 224)', 'rgb(148, 160, 180)', 'rgb(70, 80, 99)', 'rgb(70, 80, 99)']
    for (const [n, row] of rows.entries()) {
      await bar(row).waitFor()
      assert.equal(await bar(row).evaluate(el => getComputedStyle(el).borderColor), expected[n])
      assert.equal(await bar(row).getAttribute('data-task-lane'), n < 5 ? row.lane : 'unknown')
      if (n !== 4) assert.equal(await bar(row).evaluate(el => getComputedStyle(el).backgroundImage), 'none')
    }
    assert.match(await bar(rows[4]).evaluate(el => getComputedStyle(el).backgroundImage), /repeating-linear-gradient/)
    assert.match(await bar(rows[0]).getAttribute('aria-label'), /Overdue 2 days/)
    const overdue = page.locator(`[data-visual-overdue="${rows[0].id}"]`)
    assert.deepEqual(await overdue.evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, image: getComputedStyle(el).backgroundImage, border: getComputedStyle(el).borderColor })), { bg: 'rgb(253, 233, 236)', image: 'none', border: 'rgb(214, 51, 74)' })
    assert.equal(await bar(rows[1]).locator('[data-visual-progress]').evaluate(el => el.style.width), '25%')
    const legend = await openLegend()
    await legend.waitFor()
    assert.equal(await legend.locator('[data-task-lane-legend]').count(), 5)
    assert.equal(await legend.getByText('Task: Review and revision', { exact: true }).count(), 1)
    assert.equal(await legend.getByText('Deliverable: In Progress', { exact: true }).count(), 1)
    assert.deepEqual(await legend.locator('[data-visual-overdue-legend]').evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, image: getComputedStyle(el).backgroundImage })), { bg: 'rgb(253, 233, 236)', image: 'none' })
    assert.equal(await legend.getByText('Overdue to today', { exact: true }).count(), 1)
    assert.equal(await legend.locator('[data-item-mark]').count(), 3)
    // Printing with the screen popover still open must use only the dedicated paper legend.
    await page.emulateMedia({ media: 'print' })
    assert.equal(await page.locator('[data-task-lane-legend]:visible').count(), 5)
    assert.equal(await page.locator('[data-visual-overdue-legend]:visible').count(), 1)
    assert.deepEqual(await page.locator('[data-visual-overdue-legend]:visible').evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, image: getComputedStyle(el).backgroundImage })), { bg: 'rgb(253, 233, 236)', image: 'none' })
    await page.emulateMedia({ media: 'screen' })
    await legend.waitFor({ state: 'visible' })
    await page.keyboard.press('Escape')
    await legend.waitFor({ state: 'hidden' })
    return overdue.evaluate(el => ({ left: el.style.left, top: el.style.top, width: el.style.width, height: el.style.height }))
  }
  try {
    await page.route('**/api/v1/me', route => route.fulfill({ json: meView({ id: me.id, displayName: 'Alex Chen', email: 'alex@hub.test',
      flags: { visual_system: enabled, screen_patterns: true, schedule_baseline: true }, settings: { today: '2026-10-31', allowSelfReview: false } }) }))
    await page.route(`**/api/v1/projects/${project.id}/tasks*`, route => route.fulfill({ json: taskPage(rows.slice(0, 5)) }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: taskDetail({ task: rows[0], permissions: taskPermissions({ dependencies: false }) }) }))
    await page.route(`**/api/v1/projects/${project.id}/deliverables*`, route => route.fulfill({ json: [delivery] }))
    await page.route(`**/api/v1/projects/${project.id}/milestones*`, route => route.fulfill({ json: [ms] }))
    await page.route(`**/api/v1/projects/${project.id}/timeline*`, route => route.fulfill({ json: data }))
    await page.route('**/api/v1/timeline?*', route => route.fulfill({ json: ganttPage({ ...project, identityColour: '#1597cc' }, data) }))
    await page.route('**/api/v1/tasks/*', route => {
      if (route.request().method() === 'PATCH') { patches.push(route.request().postDataJSON()); return route.fulfill({ json: { id: rows[1].id, rowVersion: 2, dueDateChangeCount: 1, warnings: [] } }) }
      return route.fallback()
    })
    await page.goto(`${base}/projects/${project.projectNumber}/board?side=1`)
    const key = page.locator(`[data-visual-task-key="${rows[0].id}"]`)
    await key.waitFor()
    assert.equal(await page.locator('[data-visual-task-key] [data-item-kind="Task"]').count(), 5)
    assert.deepEqual(await key.locator('[data-item-mark]').evaluate(el => ({ width: getComputedStyle(el).width, height: getComputedStyle(el).height, fill: getComputedStyle(el).backgroundColor })), { width: '10px', height: '10px', fill: 'rgb(70, 80, 99)' })
    assert.match(await key.textContent(), /Task:.*P-MOCK-T0001/)
    assert.equal(await page.getByText('Synthetic independent reviewer', { exact: true }).count(), 1)
    assert.equal(await page.getByText('Independent reviewer', { exact: true }).count(), 1)
    const boardUrl = page.url()
    await page.getByRole('button', { name: rows[0].name, exact: true }).click()
    await page.locator('[data-slot="sheet-content"]').getByRole('heading', { name: rows[0].name, exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('panel'), `Task:${rows[0].id}`)
    await closePanel(); assert.equal(page.url(), boardUrl)
    enabled = false; await page.reload(); await page.getByRole('button', { name: rows[0].name, exact: true }).waitFor()
    assert.equal(await page.locator('[data-visual-task-key], [data-item-mark]').count(), 0)
    enabled = true

    await page.goto(`${base}/projects/${project.projectNumber}/timeline?zoom=week&from=2026-10-17&to=2026-11-15`)
    await page.getByRole('button', { name: `Show the ${rows.length} tasks of ${delivery.key}`, exact: true }).click()
    const projectGeometry = await checkTimeline()
    assert.ok(await page.locator('span[title^="Original dates"]').count() >= 2, 'original-date ghosts remain')
    assert.ok(await page.locator('svg path[marker-end]').count() >= 1, 'stored dependency edges remain')
    await cancelDrag(rows[1])
    const beforeOpen = page.url()
    await bar(rows[0]).click()
    await page.locator('[data-slot="sheet-content"]').getByRole('heading', { name: rows[0].name, exact: true }).waitFor()
    await closePanel(); assert.equal(page.url(), beforeOpen)
    enabled = false; await page.reload()
    await page.getByRole('button', { name: `Show the ${rows.length} tasks of ${delivery.key}`, exact: true }).click()
    const legacy = page.locator('span[title="Overdue 2 days"]')
    await legacy.waitFor()
    assert.match(await legacy.evaluate(el => getComputedStyle(el).backgroundImage), /repeating-linear-gradient/)
    assert.deepEqual(await legacy.evaluate(el => ({ left: el.style.left, top: el.style.top, width: el.style.width, height: el.style.height })), projectGeometry)
    assert.equal(await page.locator('[data-task-lane]').count(), 0)
    const oldLegend = await openLegend(); assert.equal(await oldLegend.locator('[data-task-lane-legend]').count(), 0)
    assert.match(await oldLegend.locator('li').filter({ hasText: 'Overdue to today' }).locator('span').evaluate(el => getComputedStyle(el).backgroundImage), /repeating-linear-gradient/)
    await page.keyboard.press('Escape')

    enabled = true
    await page.goto(`${base}/timeline?projects=all&zoom=week&start=2026-10-17`)
    const ganttGeometry = await checkTimeline()
    await cancelDrag(rows[1])
    await page.setViewportSize({ width: 375, height: 812 })
    const mobileLegend = await openLegend(); await mobileLegend.focus()
    assert.equal(await mobileLegend.getAttribute('tabindex'), '0')
    const bounds = await mobileLegend.boundingBox(); assert.ok(bounds.height <= 812 * .8 + 1)
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
    await page.keyboard.press('Escape'); await page.setViewportSize({ width: 1440, height: 1000 })
    enabled = false; await page.reload(); await legacy.waitFor()
    assert.match(await legacy.evaluate(el => getComputedStyle(el).backgroundImage), /repeating-linear-gradient/)
    assert.deepEqual(await legacy.evaluate(el => ({ left: el.style.left, top: el.style.top, width: el.style.width, height: el.style.height })), ganttGeometry)
    assert.equal(await page.locator('[data-task-lane]').count(), 0)
    assert.deepEqual(patches, []); assert.deepEqual(errors, [])
    console.log('PASS: Board neutral Task marks/review/open-return, canonical lane-only timeline roles/unknown fallback, solid overdue screen+print legends, ghost/dependency preservation, cancel-drag geometry and off stripe restoration')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
