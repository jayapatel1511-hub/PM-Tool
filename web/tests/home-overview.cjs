// 053 S06: Home overview and 052 CountCard browser acceptance against mocked API DTOs.
const assert = require('node:assert/strict')
const { newMockPage, project } = require('./platform-fixtures.cjs')
const { homeRead, homeLane, homeDeadline, widgetLayout } = require('./workspace.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const { meView } = require('./me.fixture.cjs')
const id = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`

;(async () => {
  const ui = await newMockPage(base)
  const { page, browser } = ui
  page.setDefaultTimeout(8000)
  const errors = []
  page.on('pageerror', e => errors.push(e.message))
  let flags = { visual_system: false, home_overview: false }
  const context = {
    projectsInScope: 2, projectIds: [project.id, id(12)], projectStatuses: [],
    openTasksProject: { projectId: project.id, projectNumber: 'P-MOCK', count: 5 },
    overdueTasksProject: { projectId: project.id, projectNumber: 'P-MOCK', count: 2 },
    dueWithinDays: 7, dueFrom: '2026-10-03', dueTo: '2026-10-10',
    dueWithinTasks: 2, dueWithinDeliverables: 1, dueWithinMilestones: 1,
    busiestDueDate: '2026-10-04', busiestDueCount: 2, openTasksWithoutDueDate: 0,
    onTrackCriticalProjects: ['P-MOCK'], homeDisciplineCount: 2,
  }
  let paginate = false
  let overdueCount = 2
  let onTrack = { green: 1, evaluated: 2, pct: 50 }
  const overview = () => homeRead({ projectId: project.id, layout: widgetLayout(), countContext: context,
    metrics: { activeProjects: 2, openTasks: 5, overdueTasks: overdueCount, onTrack, teamMembers: 4 },
    tasksByStatus: [homeLane('To Do', 2), homeLane('In Progress', 1), homeLane('Review', 0), homeLane('Done', 0)],
    onHold: { status: 'On Hold', count: 2 },
    upcomingDeadlines: { total: 4, truncated: false, items: [homeDeadline({ type: 'Task', id: id(31), projectId: project.id, key: 'P-MOCK-T0001', name: 'Bridge footing', date: '2026-10-03' })] },
  })
  try {
    await page.route('**/api/v1/me', async route => {
      const me = meView({ id: id(2), displayName: 'Alex Chen', email: 'alex@hub.test', systemRoles: ['StandardUser'], capabilities: { admin: true, createTask: true, workload: true }, settings: { today: '2026-10-03', restrictedProjectsEnabled: true }, flags })
      await route.fulfill({ json: me })
    })
    await page.route('**/api/v1/home**', route => {
      const dto = overview()
      const params = new URL(route.request().url()).searchParams
      if (paginate && params.has('deadlinePage')) {
        const pageNumber = Number(params.get('deadlinePage'))
        dto.upcomingDeadlines = { total: 105, truncated: true, page: pageNumber, pageSize: 100, totalPages: 2,
          items: Array.from({ length: pageNumber === 1 ? 100 : 5 }, (_, i) => homeDeadline({ type: 'Task', id: id(1000 + (pageNumber - 1) * 100 + i), projectId: project.id, key: `SYN-T${(pageNumber - 1) * 100 + i + 1}`, name: `Synthetic deadline ${(pageNumber - 1) * 100 + i + 1}`, date: '2026-10-03' })) }
      }
      return route.fulfill({ json: dto })
    })
    await page.route('**/api/v1/tasks**', route => route.fulfill({ json: { items: [], totalCount: 0, page: 1, pageSize: 200, totalPages: 0 } }))
    await page.route('**/api/v1/admin/settings**', route => route.fulfill({ json: [] }))

    // Flag independence: old UI has five tiles; visual alone adds CountCard styling but no overview-only due card.
    await page.goto(`${base}/home`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Upcoming Deadlines' }).waitFor()
    assert.equal(await page.locator('.count-card').count(), 0)
    flags = { visual_system: true, home_overview: false }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Upcoming Deadlines' }).waitFor()
    assert.equal(await page.locator('.count-card').count(), 5)
    assert.equal(await page.getByText('Due Within 7 Days').count(), 0)

    // Both flags: six cards, exact accessible name and definition tooltip on focus.
    flags = { visual_system: true, home_overview: true }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByText('Due Within 7 Days', { exact: true }).waitFor()
    assert.equal(await page.locator('.count-card').count(), 6)
    const overdue = page.getByRole('link', { name: /Overdue Tasks 2/ })
    assert.match(await overdue.getAttribute('aria-labelledby'), / /)
    await overdue.focus()
    await page.getByRole('tooltip').filter({ hasText: 'Open tasks past their due date.' }).waitFor({ state: 'visible', timeout: 3000 })
    const due = page.getByRole('link', { name: /Due Within 7 Days 4/ })
    await due.click()
    assert.match(page.url(), /\/home\?[^#]*from=2026-10-03[^#]*to=2026-10-10[^#]*view=deadlines#h-deadlines/)

    // On Track numerator/denominator links and zero-valued undated line remain visible.
    await page.goto(`${base}/home`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('link', { name: '1 Green', exact: true }).waitFor()
    assert.equal(await page.getByRole('link', { name: '1 Green', exact: true }).count(), 1)
    assert.ok(await page.getByRole('link', { name: '2 evaluated' }).count() >= 1)
    assert.equal(await page.getByText('0 open tasks have no due date').count(), 1)

    // Real status/date controls update URL state and the mocked read receives the same route.
    await page.getByRole('button', { name: /Project status/ }).click()
    await page.locator('#home-project-status').selectOption('Active')
    await page.waitForFunction(() => location.search.includes('projectStatus=Active'))
    await page.getByRole('button', { name: /Upcoming Deadlines:/ }).click()
    await page.locator('#home-scope-from').fill('2026-10-04')
    await page.waitForFunction(() => location.search.includes('from=2026-10-04'))

    // Phone layout: two columns and no horizontal overflow.
    await page.setViewportSize({ width: 375, height: 812 })
    await page.goto(`${base}/home`, { waitUntil: 'domcontentloaded' })
    await page.getByText('Due Within 7 Days', { exact: true }).waitFor()
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true)
    assert.equal(await page.locator('.count-card').count(), 6)
    // Home feature data is independent of the visual flag, and stale payloads stay hidden when off.
    flags = { visual_system: false, home_overview: true }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByText('Due Within 7 Days', { exact: true }).waitFor()
    assert.equal(await page.locator('.count-card').count(), 1)
    await page.getByText('5 in P-MOCK', { exact: true }).waitFor()
    await page.getByText('across 2 disciplines', { exact: true }).waitFor()
    flags = { visual_system: false, home_overview: false }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Upcoming Deadlines' }).waitFor()
    assert.equal(await page.getByText('Due Within 7 Days', { exact: true }).count(), 0)
    assert.equal(await page.getByText('across 2 disciplines', { exact: true }).count(), 0)
    flags = { visual_system: true, home_overview: true }
    onTrack = { green: 0, evaluated: 0, pct: null }; overdueCount = 0; context.overdueTasksProject = null
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('article', { name: /On Track Unavailable/ }).waitFor()
    assert.equal(await page.locator('.count-card--red [data-alert]').count(), 0, 'zero overdue is not an alert')
    // A large same-day cohort has a reachable second page, without dropping the exact date scope.
    paginate = true
    await page.goto(`${base}/home?view=deadlines&from=2026-10-03&to=2026-10-10`)
    await page.locator('#h-deadlines ul > li').nth(99).waitFor()
    assert.equal(await page.locator('#h-deadlines ul > li').count(), 100)
    await page.locator('#h-deadlines nav').getByRole('button', { name: 'Next' }).click()
    await page.waitForURL(/deadlinePage=2/)
    await page.getByText('Synthetic deadline 105', { exact: true }).waitFor()
    assert.equal(await page.locator('#h-deadlines ul > li').count(), 5)
    assert.equal(new URL(page.url()).searchParams.get('from'), '2026-10-03')
    await page.locator('#h-deadlines nav').getByRole('button', { name: 'Previous' }).click()
    await page.locator('#h-deadlines ul > li').nth(99).waitFor()
    await page.locator('.visual-topbar').getByRole('button', { name: 'New', exact: true }).waitFor()
    await page.locator('.visual-topbar').getByRole('button', { name: 'Notifications', exact: true }).waitFor()
    await page.locator('.visual-topbar').getByRole('button', { name: /Project scope:/ }).waitFor()
    await page.screenshot({ path: '/private/tmp/tuesday-home-overview-phone.png', fullPage: true })
    assert.deepEqual(errors, [])
    console.log('PASS: Home overview flag isolation, CountCard accessibility/links, due navigation, filters and mobile overflow')
  } catch (e) { console.error('Browser errors:', errors); console.error('Body:', await page.locator('body').innerText()); await page.screenshot({ path: '/private/tmp/tuesday-home-ui-failure.png' }); throw e } finally { await browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
