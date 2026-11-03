// 053 S07/AC-SCR-07 and Home's 052 S03: synthetic API DTOs; no live project data.
const assert = require('node:assert/strict')
const { newMockPage, project, task } = require('./platform-fixtures.cjs')
const { homeRead, homeLane, widgetLayout, homeDeadline } = require('./workspace.fixture.cjs')
const { meView } = require('./me.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const overviewLayout = () => ['metrics', 'tasksByProject', 'tasksByStatus', 'upcomingDeadlines'].map(id => ({ id, hidden: false }))
const statuses = [homeLane('To Do', 2), homeLane('In Progress', 3), homeLane('Review', 1), homeLane('Done', 4)]
statuses[0].statuses = 'Not Started'; statuses[2].statuses = 'Ready for Review,In Review,Revision Required'; statuses[3].statuses = 'Complete'

;(async () => {
  const { page, browser } = await newMockPage(base)
  page.setDefaultTimeout(8000)
  const errors = []
  page.on('pageerror', e => errors.push(e.message))
  let flags = { home_overview: false, visual_system: false }
  let layout = null
  let singleLane = false
  let empty = false
  const dto = () => {
    const lanes = empty ? statuses.map(s => ({ ...s, count: 0 })) : singleLane ? statuses.map((s, i) => ({ ...s, count: i === 0 ? 12 : 0 })) : statuses
    const holds = empty || singleLane ? 0 : 2
    const openCount = empty ? 0 : singleLane ? 12 : 8
    const rows = [
      { projectId: project.id, projectNumber: 'P-MOCK', name: 'Synthetic live project', identityColour: '#1597cc', lanes: [...lanes, homeLane('On Hold', holds)], total: empty ? 0 : 12 },
      ...[11, 12, 13, 14].map(n => ({ projectId: id(n), projectNumber: `P-${n}`, name: `Synthetic empty project ${n}`, lanes: [...statuses.map(s => ({ ...s, count: 0 })), homeLane('On Hold', 0)], total: 0 })),
    ]
    const value = homeRead({ projectId: project.id, layout: layout ?? (flags.home_overview ? overviewLayout() : widgetLayout()), isDefault: layout == null,
      metrics: { activeProjects: 5, openTasks: openCount, overdueTasks: empty ? 0 : 1, onTrack: { green: 1, evaluated: 2, pct: 50 }, teamMembers: 4 },
      tasksByStatus: lanes, onHold: { status: 'On Hold', count: holds }, tasksByProject: rows,
      upcomingDeadlines: { total: empty ? 0 : 7, truncated: false, items: empty ? [] : Array.from({ length: 7 }, (_, i) => homeDeadline({ type: 'Task', id: i === 0 ? task.id : id(100 + i), projectId: project.id, key: `P-MOCK-T000${i + 1}`, name: 'Synthetic task', date: '2026-10-03' })) },
    })
    // Deliberately send stale feature payload even when off: the UI must still hide it.
    value.countContext = { projectsInScope: 5, projectIds: rows.map(p => p.projectId), projectStatuses: [], openTasksProject: empty ? null : { projectId: project.id, projectNumber: 'P-MOCK', count: openCount }, overdueTasksProject: empty ? null : { projectId: project.id, projectNumber: 'P-MOCK', count: 1 },
      dueWithinDays: 7, dueFrom: '2026-10-03', dueTo: '2026-10-10', dueWithinTasks: empty ? 0 : 7, dueWithinDeliverables: 0, dueWithinMilestones: 0, busiestDueDate: empty ? null : '2026-10-03', busiestDueCount: empty ? 0 : 7,
      openTasksWithoutDueDate: 0, onTrackCriticalProjects: [], homeDisciplineCount: 2 }
    value.runway = { from: '2026-10-03', to: '2026-10-16', truncatedRange: false,
      days: Array.from({ length: 14 }, (_, n) => ({ date: `2026-10-${String(n + 3).padStart(2, '0')}`, nonWorking: [0, 1, 7, 8].includes(n), total: !empty && n === 0 ? 7 : 0,
        items: !empty && n === 0 ? [0, 1, 2, 3].map(i => ({ type: 'Task', id: i === 0 ? task.id : id(100 + i), projectId: project.id, projectNumber: 'P-MOCK', key: `P-MOCK-T000${i + 1}`, name: 'Synthetic task', identityColour: '#1597cc' })) : [] })) }
    return value
  }
  try {
    await page.route('**/api/v1/me', route => route.fulfill({ json: meView({ id: id(2), displayName: 'Alex Chen', email: 'alex@hub.test', capabilities: { createTask: true, workload: true }, flags, settings: { today: '2026-10-03' } }) }))
    await page.route('**/api/v1/home**', route => route.fulfill({ json: dto() }))
    await page.route('**/api/v1/me/dashboard-layout', async route => {
      layout = route.request().method() === 'DELETE' ? null : route.request().postDataJSON().widgets
      return route.fulfill({ json: { layout: layout ?? overviewLayout(), isDefault: layout == null } })
    })
    await page.goto(`${base}/home?projects=all`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Tasks by Status', exact: true }).waitFor()
    assert.equal(await page.locator('.home-donut').count(), 0)
    assert.equal(await page.locator('[data-home-runway]').count(), 0)

    flags = { home_overview: true, visual_system: true }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.locator('.home-donut').waitFor()
    assert.equal(await page.locator('.count-card').count(), 6)
    const order = await page.locator('[data-home-widget]').evaluateAll(nodes => nodes.map(n => n.dataset.homeWidget))
    assert.deepEqual(order, ['metrics', 'tasksByProject', 'tasksByStatus', 'upcomingDeadlines'])
    const projectBox = await page.locator('[data-home-widget="tasksByProject"]').boundingBox()
    const statusBox = await page.locator('[data-home-widget="tasksByStatus"]').boundingBox()
    assert.ok(Math.abs(projectBox.y - statusBox.y) < 2, 'desktop charts share one row')
    assert.ok(projectBox.width > statusBox.width * 1.9, 'Projects is the primary two-thirds chart')
    assert.equal(await page.locator('.home-donut__total').innerText(), '12\nTotal tasks')
    assert.equal(await page.locator('.home-donut svg a').count(), 5)
    assert.match(await page.locator('[data-home-donut-lane="On Hold"]').getAttribute('fill'), /url\(#home-hold-/)
    assert.equal(await page.getByText('Open tasks: 2 + 3 + 1 + 2 on hold = 8', { exact: true }).count(), 1)
    assert.equal(await page.getByText('0 open tasks have no due date', { exact: true }).count(), 1)
    assert.match(await page.locator('.home-donut__total a').getAttribute('href'), /status=/)
    assert.match(await page.locator('#h-projects [data-home-lane="On Hold"]').first().getAttribute('aria-label'), /P-MOCK On Hold: 2/)
    assert.equal(await page.locator('.home-project-chart__axis').count(), 1)
    const mark = page.locator('[data-home-runway] .home-runway__day[data-today]')
    assert.equal(await mark.count(), 1)
    assert.equal(await page.locator('.home-runway__day').count(), 14)
    assert.equal(await mark.locator('li button').count(), 4)
    assert.equal(await mark.locator('.home-runway__more').innerText(), '+3')
    assert.equal(await mark.locator('.home-runway__date strong').innerText(), '7')
    assert.match(await mark.locator('.home-runway__more').getAttribute('href'), /from=2026-10-03&to=2026-10-03/)
    // ICU releases differ on the comma after a short en-GB weekday; the record/date facts stay exact.
    assert.match(await mark.locator('li button').first().getAttribute('aria-label'), /^P-MOCK, Task P-MOCK-T0001: Synthetic task, due Sat,?\s+3\s+Oct$/)
    await mark.locator('li button').first().click()
    await page.getByRole('heading', { name: 'Synthetic task', exact: true }).waitFor()
    assert.match(page.url(), /panel=Task/)

    // The charts stack under 1180 and the runway scrolls inside its own region on a phone.
    await page.setViewportSize({ width: 1100, height: 1000 })
    await page.goto(`${base}/home?projects=all`, { waitUntil: 'domcontentloaded' })
    await page.locator('.home-donut').waitFor()
    const stackedProject = await page.locator('[data-home-widget="tasksByProject"]').boundingBox()
    const stackedStatus = await page.locator('[data-home-widget="tasksByStatus"]').boundingBox()
    assert.ok(stackedStatus.y > stackedProject.y + stackedProject.height - 1)
    await page.setViewportSize({ width: 375, height: 812 })
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true)
    assert.equal(await page.locator('.home-runway__scroll').evaluate(el => el.scrollWidth > el.clientWidth), true)

    // Saved widget visibility continues to govern the new chart, without disturbing other widgets.
    await page.setViewportSize({ width: 1440, height: 1000 })
    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).click()
    await page.locator('[data-home-widget="tasksByStatus"]').getByRole('button', { name: 'Hide', exact: true }).click()
    await page.getByRole('button', { name: 'Done', exact: true }).click()
    assert.equal(await page.locator('.home-donut').count(), 0)
    assert.equal(await page.locator('[data-home-runway]').count(), 1)
    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).click()
    await page.getByRole('button', { name: 'Restore default', exact: true }).click()
    await page.getByRole('button', { name: 'Done', exact: true }).click()
    await page.locator('.home-donut').waitFor()

    // One lane fills the ring; zero lanes have no paths or NaN; Home and visual flags remain independent.
    singleLane = true
    flags = { home_overview: true, visual_system: false }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.locator('.home-donut').waitFor()
    assert.equal(await page.locator('.home-donut svg a').count(), 1)
    const path = await page.locator('.home-donut svg a path').getAttribute('d')
    assert.ok(!path.includes('NaN')); assert.ok((path.match(/ A/g) ?? []).length >= 4)
    assert.equal(await page.locator('[data-home-runway]').count(), 1)
    assert.equal(await page.locator('html').getAttribute('data-visual'), null)
    empty = true
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.locator('.home-donut').waitFor()
    assert.equal(await page.locator('.home-donut svg a').count(), 0)
    assert.equal(await page.locator('.home-donut__total').innerText(), '0\nTotal tasks')
    flags = { home_overview: false, visual_system: true }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Tasks by Status', exact: true }).waitFor()
    assert.equal(await page.locator('.home-donut').count(), 0)
    assert.equal(await page.locator('[data-home-runway]').count(), 0)
    assert.deepEqual(errors, [])
    console.log('PASS: S07 chart layout, exact totals/hold hatch, record links, runway overflow/today/containment, saved visibility and independent flags')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
