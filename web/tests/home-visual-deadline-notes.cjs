// FR/AC-SCR-13: synthetic read DTO presentation and exact record opens; real evidence is covered by API tests.
const assert = require('node:assert/strict')
const { newMockPage, project, task, panelIds, taskDetail } = require('./platform-fixtures.cjs')
const { homeRead, homeDeadline, homeLane } = require('./workspace.fixture.cjs')
const { meView } = require('./me.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))

;(async () => {
  const { page, browser } = await newMockPage(base)
  page.setDefaultTimeout(8000)
  let flags = { home_overview: true, visual_system: true }
  let mode = 'normal'
  const errors = []; page.on('pageerror', error => errors.push(error.message))
  const items = () => [
    homeDeadline({ type: 'Task', id: task.id, projectId: project.id, key: 'P-MOCK-T0001', name: 'Synthetic task', date: '2026-10-31', status: 'In Review',
      note: { kind: 'InReview', days: mode === 'single' ? 1 : mode === 'invalid' ? -1 : 5 }, baselineDays: 999 }),
    homeDeadline({ type: 'Milestone', id: panelIds.milestone, projectId: project.id, key: 'P-MOCK-M0001', name: 'Synthetic milestone', date: '2026-10-31',
      note: { kind: mode === 'invalid' ? 'UnsupportedNote' : 'LaterThanBaseline', days: mode === 'single' ? 1 : 3 } }),
    homeDeadline({ type: 'Deliverable', id: panelIds.deliverable, projectId: project.id, key: 'P-MOCK-D0001', name: 'Synthetic missing review evidence', date: '2026-10-31', status: 'In Review' }),
  ]
  try {
    await page.route('**/api/v1/me', route => route.fulfill({ json: meView({ id: taskDetail.task.assigneeId ?? '00000000-0000-0000-0000-000000000002', displayName: 'Synthetic note reviewer', email: 'notes@hub.test', flags,
      capabilities: { workload: false }, settings: { today: '2026-10-31' } }) }))
    await page.route('**/api/v1/home**', route => route.fulfill({ json: homeRead({ projectId: project.id, today: '2026-10-31', from: '2026-10-31', to: '2026-10-31',
      metrics: { activeProjects: 1, openTasks: 1, overdueTasks: 0, onTrack: { green: 0, evaluated: 0, pct: null }, teamMembers: 1 },
      tasksByStatus: [homeLane('To Do', 0), homeLane('In Progress', 0), homeLane('Review', 1), homeLane('Done', 0)], onHold: { status: 'On Hold', count: 0 },
      tasksByProject: [{ projectId: project.id, projectNumber: project.projectNumber, name: project.name, total: 1, lanes: [homeLane('Review', 1)] }],
      upcomingDeadlines: { total: 3, truncated: false, items: items() } }) }))
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: { ...taskDetail, task: { ...taskDetail.task, status: 'In Review', requiresReview: true, reviewerId: '00000000-0000-0000-0000-000000000004', reviewerName: 'Synthetic independent reviewer' } } }))
    await page.goto(`${base}/home?projects=all&from=2026-10-31&to=2026-10-31`, { waitUntil: 'domcontentloaded' })
    const section = page.locator('#h-deadlines')
    await section.getByText('in review 5 days', { exact: true }).waitFor()
    assert.equal(await section.locator('[data-home-deadline-note]').count(), 2)
    assert.equal(await section.getByText('3 days later than baseline', { exact: true }).count(), 1)
    assert.equal(await section.getByText(/999 days/).count(), 0)
    assert.equal(await section.locator('li').filter({ hasText: 'Synthetic missing review evidence' }).locator('[data-home-deadline-note]').count(), 0)
    for (const row of await section.locator('ul.divide-y > li').all()) assert.ok(await row.locator('[data-home-deadline-note]').count() <= 1)
    await section.locator('li').filter({ hasText: 'Synthetic task' }).getByRole('button').click()
    assert.equal(new URL(page.url()).searchParams.get('panel'), `Task:${task.id}`)
    await page.locator('[data-slot="sheet-content"]').getByRole('button', { name: 'Close', exact: true }).first().click()
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('panel'))
    assert.equal(new URL(page.url()).searchParams.get('from'), '2026-10-31')

    mode = 'single'; flags = { home_overview: true, visual_system: false }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await section.getByText('in review 1 day', { exact: true }).waitFor()
    assert.equal(await section.getByText('1 day later than baseline', { exact: true }).count(), 1)
    mode = 'invalid'
    await page.reload({ waitUntil: 'domcontentloaded' })
    await section.getByText('Synthetic task', { exact: true }).waitFor()
    assert.equal(await section.locator('[data-home-deadline-note]').count(), 0)
    mode = 'normal'; flags = { home_overview: false, visual_system: true }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await section.getByText('Synthetic task', { exact: true }).waitFor()
    assert.equal(await section.locator('[data-home-deadline-note]').count(), 0, 'stale notes stay hidden while Home feature is off')
    assert.deepEqual(errors, [])
    console.log('PASS: FR13 one evidence note, review/baseline copy, singular days, exact panel/return filter, independent flags and unknown-note guard')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
