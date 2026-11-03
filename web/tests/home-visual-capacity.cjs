// 053 S09 / AC-SCR-11 and S07 widget layout. All API values are labelled synthetic fixtures.
const assert = require('node:assert/strict')
const { newMockPage, project } = require('./platform-fixtures.cjs')
const { homeRead, homeLane } = require('./workspace.fixture.cjs')
const { meView } = require('./me.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const defaultLayout = () => ['metrics', 'tasksByProject', 'tasksByStatus', 'weeklyCapacity', 'upcomingDeadlines'].map(id => ({ id, hidden: false }))
const plannerPath = '/planner?from=2026-09-14&weeks=1&includeMyDrafts=true&sort=name'
const totals = (extra = {}) => ({ grossHours: 648, availableHours: 612, plannedHours: 529, timeAwayHours: 36, possibleHours: 13, freeHours: 83, pct: 86,
  people: 16, partialPeople: 0, provisionalPeople: 0, missingWeeks: 0, above100People: 10, ...extra })

;(async () => {
  const { page, browser } = await newMockPage(base)
  page.setDefaultTimeout(8000)
  const errors = []
  page.on('pageerror', e => errors.push(e.message))
  let flags = { home_overview: true, visual_system: true }
  let workload = true
  let mode = 'normal'
  let layout = null
  const capacity = () => {
    const value = { week: '2026-09-14', through: '2026-09-20', asOf: '2026-09-14T13:00:00+00:00', totals: totals(), plannerPath, above100Path: `${plannerPath}&indicator=Above100`,
      disciplines: [
        { disciplineId: id(11), disciplineName: 'Civil', plannerPath: `${plannerPath}&homeDisciplineId=${id(11)}`, totals: totals({ people: 8, grossHours: 258, availableHours: 240, plannedHours: 300, timeAwayHours: 18, freeHours: -60, pct: 125, above100People: 5, possibleHours: 6 }) },
        { disciplineId: id(12), disciplineName: 'Electrical', plannerPath: `${plannerPath}&homeDisciplineId=${id(12)}`, totals: totals({ people: 8, grossHours: 390, availableHours: 372, plannedHours: 229, timeAwayHours: 18, freeHours: 143, pct: 62, above100People: 5, possibleHours: 7 }) },
      ] }
    if (mode === 'partial') {
      value.totals.partialPeople = 1; value.totals.freeHours = null
      value.disciplines[0].totals.partialPeople = 1; value.disciplines[0].totals.freeHours = null
    }
    if (mode === 'provisional') {
      value.totals = totals({ provisionalPeople: 1, grossHours: null, availableHours: null, freeHours: null, pct: null, above100People: 9 })
      Object.assign(value.disciplines[0].totals, { provisionalPeople: 1, grossHours: null, availableHours: null, freeHours: null, pct: null, above100People: 4 })
    }
    if (mode === 'zero') {
      value.totals = totals({ people: 1, grossHours: 0, availableHours: 0, plannedHours: 5, timeAwayHours: 0, freeHours: -5, pct: null, above100People: 1 })
      value.disciplines = [{ disciplineId: null, disciplineName: null, plannerPath: `${plannerPath}&homeDisciplineId=${id(0)}`, totals: value.totals }]
    }
    return value
  }
  try {
    await page.route('**/api/v1/me', route => route.fulfill({ json: meView({ id: id(2), displayName: 'Synthetic supervisor', email: 'capacity@hub.test', capabilities: { workload }, flags,
      settings: { today: '2040-01-01', orgTimeZone: 'America/Halifax' } }) }))
    await page.route('**/api/v1/home**', route => route.fulfill({ json: { ...homeRead({ projectId: project.id,
      today: '2026-09-14', from: '2026-09-14', to: '2026-09-27', metrics: { activeProjects: 0, openTasks: 0, overdueTasks: 0, onTrack: { green: 0, evaluated: 0, pct: null }, teamMembers: 0 },
      tasksByStatus: ['To Do', 'In Progress', 'Review', 'Done'].map(l => homeLane(l, 0)), onHold: { status: 'On Hold', count: 0 }, tasksByProject: [],
      countContext: { projectsInScope: 0, projectIds: [], projectStatuses: [], openTasksProject: null, overdueTasksProject: null,
        dueWithinDays: 7, dueFrom: '2026-09-14', dueTo: '2026-09-21', dueWithinTasks: 0, dueWithinDeliverables: 0, dueWithinMilestones: 0, busiestDueDate: null, busiestDueCount: 0, openTasksWithoutDueDate: 0, onTrackCriticalProjects: [], homeDisciplineCount: 0 } }),
      layout: layout ?? defaultLayout(), isDefault: layout == null, weeklyCapacity: capacity() } }))
    await page.route('**/api/v1/me/dashboard-layout', route => {
      layout = route.request().method() === 'DELETE' ? null : route.request().postDataJSON().widgets
      return route.fulfill({ json: { layout: layout ?? defaultLayout(), isDefault: layout == null } })
    })
    await page.goto(`${base}/home?projects=none`, { waitUntil: 'domcontentloaded' })
    const card = page.locator('[data-home-capacity]')
    await card.waitFor()
    assert.equal(await page.locator('.count-card').count(), 6)
    assert.equal(await card.locator('[data-capacity-percentage]').innerText(), '86%')
    assert.equal(await card.locator('[data-capacity-value="gross"] dd').innerText(), '648 h')
    assert.equal(await card.locator('[data-capacity-value="available"] dd').innerText(), '612 h')
    assert.equal(await card.locator('[data-capacity-value="planned"] dd').innerText(), '529 h')
    assert.equal(await card.locator('[data-capacity-value="away"] dd').innerText(), '36 h')
    assert.equal(await card.locator('[data-capacity-value="free"] dd').innerText(), '83 h')
    assert.equal(await card.locator('[data-capacity-value="possible"] dd').innerText(), '13 h')
    assert.match(await card.locator('.home-capacity__scope').innerText(), /People in your planner scope: 16, independent of Home's projects/)
    assert.match(await card.locator('.home-capacity__scope').innerText(), /2026-09-14.*2026-09-20/)
    assert.match(await card.locator('.home-capacity__asof').innerText(), /2026-09-14 10:00/)
    const above = new URL(await card.locator('.home-capacity__above a').getAttribute('href'), base)
    assert.equal(above.searchParams.get('indicator'), 'Above100'); assert.equal(above.searchParams.get('weeks'), '1')
    assert.equal(above.searchParams.get('includeMyDrafts'), 'true'); assert.equal(above.searchParams.has('projects'), false)
    const civil = card.locator('.home-capacity__discipline').filter({ hasText: 'Civil' })
    assert.equal(await civil.locator('.home-capacity__group-pct').innerText(), '125%')
    assert.match(await civil.locator('.home-capacity__group-notes').innerText(), /Free −60 h/)
    assert.ok(await civil.locator('.home-capacity__over').evaluate(el => el.getBoundingClientRect().width) > 0)
    assert.match(await civil.locator('.home-capacity__discipline-name').getAttribute('href'), /homeDisciplineId=/)
    assert.ok(await card.locator('.home-capacity__away').first().evaluate(el => getComputedStyle(el).backgroundImage) !== 'none')
    assert.deepEqual(await page.locator('[data-home-widget]').evaluateAll(nodes => nodes.map(n => n.dataset.homeWidget)), ['metrics', 'tasksByProject', 'tasksByStatus', 'weeklyCapacity', 'upcomingDeadlines'])

    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).click()
    await page.locator('[data-home-widget="weeklyCapacity"]').getByRole('button', { name: 'Hide', exact: true }).click()
    await page.getByRole('button', { name: 'Done', exact: true }).click()
    await card.waitFor({ state: 'hidden' })
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).click()
    await page.locator('[data-home-widget="weeklyCapacity"]').getByRole('button', { name: 'Show', exact: true }).click()
    await page.getByRole('button', { name: 'Done', exact: true }).click()
    await card.waitFor()
    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).click()
    await page.getByRole('button', { name: "Move This Week's Capacity up", exact: true }).click()
    await page.getByRole('button', { name: 'Done', exact: true }).click()
    assert.equal((await page.locator('[data-home-widget]').evaluateAll(nodes => nodes.map(n => n.dataset.homeWidget))).indexOf('weeklyCapacity'), 2)
    await page.reload({ waitUntil: 'domcontentloaded' })
    await card.waitFor()
    assert.equal((await page.locator('[data-home-widget]').evaluateAll(nodes => nodes.map(n => n.dataset.homeWidget))).indexOf('weeklyCapacity'), 2)

    mode = 'partial'
    await page.reload({ waitUntil: 'domcontentloaded' })
    await card.waitFor()
    assert.equal(await card.locator('[data-capacity-percentage]').innerText(), '86%')
    assert.equal(await card.locator('[data-capacity-value="free"] dd').innerText(), 'Unavailable')
    assert.match(await card.innerText(), /Partial view: totals include only projects you can see/)
    assert.match(await card.locator('.home-capacity__headline').innerText(), /visible view/)
    assert.ok(await card.locator('.home-capacity__unknown').count() > 0)
    assert.match(await card.locator('.home-capacity__bar').first().getAttribute('aria-label'), /Free Unavailable/)
    mode = 'provisional'
    await page.reload({ waitUntil: 'domcontentloaded' })
    await card.waitFor()
    assert.equal(await card.locator('[data-capacity-percentage]').innerText(), 'Percentage unavailable')
    assert.equal(await card.locator('[data-capacity-value="available"] dd').innerText(), 'Unavailable')
    assert.equal(await card.locator('[data-capacity-value="free"] dd').innerText(), 'Unavailable')
    assert.match(await card.innerText(), /People with unverified capacity: 1/)
    mode = 'zero'
    flags = { home_overview: true, visual_system: false }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await card.waitFor()
    assert.equal(await card.locator('[data-capacity-percentage]').innerText(), 'Percentage unavailable')
    assert.equal(await card.locator('[data-capacity-value="free"] dd').innerText(), '−5 h')
    assert.equal(await card.locator('.home-capacity__discipline-name').innerText(), 'Not set')
    assert.equal(new URL(await card.locator('.home-capacity__discipline-name').getAttribute('href'), base).searchParams.get('homeDisciplineId'), id(0))
    assert.equal(await card.locator('[style*="NaN"]').count(), 0)
    await page.setViewportSize({ width: 375, height: 812 })
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true)

    // Unauthorized/stale payload and the two feature flags are independently guarded in the real Home component.
    workload = false
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).waitFor()
    assert.equal(await card.count(), 0)
    assert.equal(await page.locator('[data-home-widget="weeklyCapacity"]').count(), 0)
    workload = true; flags = { home_overview: false, visual_system: true }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'Edit Dashboard', exact: true }).waitFor()
    assert.equal(await card.count(), 0)
    assert.deepEqual(errors, [])
    console.log('PASS: AC-SCR-11 648/36/529/612/86%/83 example, exact planner links, hold/show, partial/provisional/zero truth, independent flags and phone containment')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
