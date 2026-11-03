// S08 synthetic DTO browser acceptance; API tests separately verify current-role/permission/common-cohort logic.
const assert = require('node:assert/strict')
const { newMockPage, project } = require('./platform-fixtures.cjs')
const { homeRead, homeLane } = require('./workspace.fixture.cjs')
const { meView } = require('./me.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`

;(async () => {
  const { page, browser } = await newMockPage(base)
  page.setDefaultTimeout(8000)
  const errors = [], writes = [], reads = []
  page.on('pageerror', e => errors.push(e.message))
  let flags = { home_overview: true, visual_system: true }
  let savedPreset = null, projects = 2, compareRange = false
  const dto = url => {
    const code = url.searchParams.get('homePreset') || null
    const status = url.searchParams.get('projectStatus') || null
    const filtered = ['DisciplineLead', 'Supervisor'].includes(code)
    const metrics = [
      { id: 'ActiveProjects', state: 'Comparable', reason: null, current: 1, previous: 1, delta: 0, tone: 'Neutral' },
      { id: 'OpenTasks', state: 'Comparable', reason: null, current: 1, previous: 3, delta: -2, tone: 'Neutral' },
      { id: 'OverdueTasks', state: 'Comparable', reason: null, current: 1, previous: 3, delta: -2, tone: 'Good' },
      { id: 'DueWithin', state: 'Comparable', reason: null, current: 0, previous: 1, delta: -1, tone: 'Neutral' },
      { id: 'OnTrack', state: 'Comparable', reason: null, current: 100, previous: 0, delta: 100, tone: 'Good' },
    ].map(metric => filtered ? { ...metric, state: 'Unavailable', reason: 'FilteredScope', current: null, previous: null, delta: null, tone: 'Neutral' } : metric)
    if (compareRange && !filtered) for (const metric of metrics) {
      if (metric.id === 'ActiveProjects') { metric.current = 2; metric.previous = 2 }
      if (['OpenTasks', 'OverdueTasks'].includes(metric.id)) { metric.current = 2; metric.previous = 4 }
      if (metric.id === 'OnTrack') { metric.current = 50; metric.delta = 50 }
    }
    const lanes = [homeLane('To Do', 2), homeLane('In Progress', 0), homeLane('Review', 0), homeLane('Done', 2)]
    lanes[0].statuses = 'Not Started'; lanes[3].statuses = 'Complete'
    const value = homeRead({ projectId: project.id, today: '2026-10-31', from: '2026-10-31', to: '2026-11-13', asOf: '2026-10-31T13:00:00Z',
      metrics: { activeProjects: status && status !== 'Active' ? 0 : projects, openTasks: 2, overdueTasks: 2, onTrack: { green: 1, evaluated: 2, pct: 50 }, teamMembers: 4 },
      tasksByStatus: lanes, onHold: { status: 'On Hold', count: 0 },
      tasksByProject: Array.from({ length: projects }, (_, n) => ({ projectId: n ? id(100 + n) : project.id, projectNumber: n ? `P-S08-${n}` : 'P-MOCK', name: 'Synthetic scoped project', total: n < 2 ? 2 : 0, lanes: lanes.map(lane => ({ ...lane, count: n < 2 ? lane.count / 2 : 0 })) })),
      layout: ['metrics', 'tasksByProject', 'tasksByStatus', 'upcomingDeadlines'].map(id => ({ id, hidden: false })),
    })
    value.countContext = { projectsInScope: projects, projectIds: Array.from({ length: projects }, (_, n) => n ? id(100 + n) : project.id), projectStatuses: status ? status.split(',') : [],
      openTasksProject: { projectId: project.id, projectNumber: 'P-MOCK', count: 1 }, overdueTasksProject: { projectId: project.id, projectNumber: 'P-MOCK', count: 1 },
      dueWithinDays: 7, dueFrom: '2026-10-31', dueTo: '2026-11-07', dueWithinTasks: 0, dueWithinDeliverables: 0, dueWithinMilestones: 0, busiestDueDate: null, busiestDueCount: 0,
      openTasksWithoutDueDate: 0, onTrackCriticalProjects: [], homeDisciplineCount: 2 }
    value.preset = { code, savedPreset, available: ['ProjectManager', 'DisciplineLead'], projects, disciplines: filtered ? 1 : 0, people: 0, unownedMilestonesExcluded: filtered }
    value.trends = { targetDate: '2026-10-24', compareDays: 7, compareFrom: filtered ? null : '2026-10-24T13:00:00Z', compareThrough: filtered ? null : compareRange ? '2026-10-24T14:00:00Z' : '2026-10-24T13:00:00Z',
      excludedProjects: filtered ? projects : projects - (compareRange ? 2 : 1), projects: filtered ? [] : [{ projectNumber: 'P-MOCK', snapshotDate: '2026-10-24', capturedAt: '2026-10-24T13:00:00Z' }, ...(compareRange ? [{ projectNumber: 'P-S08-1', snapshotDate: '2026-10-24', capturedAt: '2026-10-24T14:00:00Z' }] : [])], metrics }
    return value // stale new payload is intentionally still sent in off-state checks
  }
  const openPreset = () => page.locator('[data-home-preset]').click()
  const choose = async code => { await openPreset(); await Promise.all([page.waitForResponse(response => response.url().endsWith('/api/v1/me/home-preset') && response.request().method() === 'PUT'), page.getByLabel('Scope preset', { exact: true }).selectOption(code)]) }
  const chip = metric => page.locator(`[data-trend-metric="${metric}"]`).first()
  try {
    await page.route('**/api/v1/me', route => route.fulfill({ json: meView({ id: id(2), displayName: 'Priya Nair', email: 'priya@hub.test', flags, settings: { today: '2026-10-31', orgTimeZone: 'America/Halifax' }, capabilities: { workload: false } }) }))
    await page.route('**/api/v1/home**', route => { const url = new URL(route.request().url()); reads.push(url.search); return route.fulfill({ json: dto(url) }) })
    await page.route('**/api/v1/me/home-preset', route => { savedPreset = route.request().postDataJSON().code; writes.push(savedPreset); return route.fulfill({ status: 204 }) })
    await page.goto(`${base}/home?projects=all`, { waitUntil: 'domcontentloaded' })
    await chip('OverdueTasks').waitFor()
    assert.equal(await chip('OverdueTasks').innerText(), '↓ 2')
    assert.equal(await chip('OverdueTasks').getAttribute('data-trend-tone'), 'Good')
    assert.equal(await chip('OpenTasks').getAttribute('data-trend-tone'), 'Neutral')
    assert.equal(await chip('ActiveProjects').innerText(), '± 0')
    assert.equal(await chip('OnTrack').innerText(), '↑ 100 pp')
    assert.equal(await page.locator('[data-trend-metric="TeamMembers"]').count(), 0)
    assert.equal(await page.locator('.visual-title-block').getByText('2026-10-24 10:00', { exact: true }).count(), 1)
    const overdue = page.getByRole('link', { name: /Overdue Tasks 2/ })
    await overdue.focus()
    await page.getByRole('tooltip').filter({ hasText: 'Comparison cohort: current 1, previous 3; 1 included projects and 1 excluded' }).waitFor()
    await page.getByRole('button', { name: 'Comparison details', exact: true }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('heading', { name: 'Comparison details', exact: true }).waitFor()
    const row = dialog.getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Overdue Tasks', exact: true }) })
    assert.deepEqual(await row.getByRole('cell').allTextContents(), ['1', '3', '↓ 2'])
    assert.equal(await dialog.getByRole('rowheader', { name: 'Team Members', exact: true }).count(), 0)
    assert.match(await dialog.innerText(), /excluded: 1/)
    assert.equal(await dialog.locator('time').getAttribute('datetime'), '2026-10-24T13:00:00Z')
    // Both read-only tables remain reachable and scroll horizontally by keyboard on a narrow screen.
    await page.setViewportSize({ width: 375, height: 812 })
    for (const [index, name] of ['Comparison details', 'Projects in the comparison'].entries()) {
      const region = dialog.getByRole('region', { name, exact: true })
      assert.equal(await region.getAttribute('tabindex'), '0')
      assert.equal(await region.evaluate(el => el.scrollWidth > el.clientWidth), true, `${name} actually overflows horizontally`)
      if (index === 0) await region.focus()
      else await page.keyboard.press('Tab')
      assert.equal(await region.evaluate(el => document.activeElement === el), true, `${name} receives keyboard focus`)
      const handle = await region.elementHandle()
      await region.press('ArrowRight')
      await page.waitForFunction(el => el.scrollLeft > 0, handle)
      await region.press('Home')
    }
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'tables overflow only within their named scroll regions')
    await dialog.locator('[data-slot="dialog-footer"]').getByRole('button', { name: 'Close', exact: true }).click()
    await page.setViewportSize({ width: 1440, height: 1000 })
    await openPreset()
    assert.deepEqual(await page.getByLabel('Scope preset', { exact: true }).locator('option').allTextContents(), ['No preset', 'Project Manager', 'Discipline Lead'])
    await Promise.all([page.waitForResponse(response => response.url().endsWith('/api/v1/me/home-preset') && response.request().method() === 'PUT'), page.getByLabel('Scope preset', { exact: true }).selectOption('ProjectManager')])
    await page.waitForFunction(() => new URL(location.href).searchParams.get('homePreset') === 'ProjectManager')
    await page.getByRole('button', { name: /Project scope: Projects I manage/ }).waitFor()
    await page.waitForFunction(() => document.querySelector('[data-home-preset]')?.textContent.includes('Project Manager'))
    assert.deepEqual(writes, ['ProjectManager'])
    // Every derived cohort stays server-side, even after a >50-project response.
    projects = 53
    await page.goto(`${base}/home?projects=mine&homePreset=ProjectManager&projectStatus=Active`, { waitUntil: 'domcontentloaded' })
    await chip('OverdueTasks').waitFor()
    const taskLink = new URL(await page.getByRole('link', { name: /Open Tasks 2/ }).getAttribute('href'), base)
    assert.equal(taskLink.searchParams.get('homePreset'), 'ProjectManager')
    assert.equal(taskLink.searchParams.get('homeProjectStatus'), 'Active')
    assert.equal(taskLink.searchParams.get('projectId'), null)
    assert.ok(taskLink.toString().length < 180)
    const projectLink = new URL(await page.getByRole('link', { name: /Active Projects 53/ }).getAttribute('href'), base)
    assert.equal(projectLink.searchParams.get('homeProjects'), 'mine')
    assert.equal(projectLink.searchParams.get('mine'), 'false')
    assert.equal(projectLink.searchParams.get('homePreset'), 'ProjectManager')
    assert.equal(projectLink.searchParams.get('ids'), null)
    const teamLink = new URL(await page.getByRole('link', { name: /Team Members 4/ }).getAttribute('href'), base)
    assert.equal(teamLink.searchParams.get('homeProjectStatus'), 'Active'); assert.equal(teamLink.searchParams.get('homePreset'), 'ProjectManager')
    await choose('DisciplineLead')
    await page.getByRole('button', { name: 'Led disciplines: 1', exact: true }).waitFor()
    assert.equal(await chip('OverdueTasks').innerText(), 'Trend unavailable for this scope')
    assert.equal(await page.getByText('Tasks, deliverables and deadlines are limited to the disciplines you lead. Milestones without a led discipline are excluded.', { exact: true }).count(), 1)
    assert.equal(await page.locator('.visual-title-block').getByText('Trends compare with', { exact: true }).count(), 0)
    // A clean visit restores only a previously chosen permitted preference; an explicit URL scope wins.
    await page.goto(`${base}/home`, { waitUntil: 'domcontentloaded' })
    await page.waitForFunction(() => new URL(location.href).searchParams.get('homePreset') === 'DisciplineLead')
    await page.goto(`${base}/home?projects=none`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Upcoming Deadlines', exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('homePreset'), null)
    await choose('ProjectManager')
    await choose('')
    await page.waitForFunction(() => !new URL(location.href).searchParams.has('homePreset'))
    assert.equal(writes.at(-1), null)
    // Historical source times remain visible without visual_system; stale new UI is hidden without home_overview.
    projects = 2; compareRange = true; flags = { home_overview: true, visual_system: false }
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.locator('[data-home-trend-reference]').waitFor()
    assert.match(await page.locator('[data-home-trend-reference]').innerText(), /2026-10-24 10:00 – 2026-10-24 11:00/)
    assert.equal(await chip('OverdueTasks').innerText(), '↓ 2')
    flags = { home_overview: false, visual_system: true }
    await page.goto(`${base}/home?projects=all&homePreset=DisciplineLead`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('heading', { name: 'Upcoming Deadlines', exact: true }).waitFor()
    assert.equal(await page.locator('[data-trend-metric]').count(), 0)
    assert.equal(await page.locator('[data-home-preset]').count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Comparison details', exact: true }).count(), 0)
    assert.ok(!reads.at(-1).includes('homePreset'))
    assert.equal(await page.locator('.count-card__top').count(), 0, 'off-state cards preserve the previous icon structure')
    assert.deepEqual(errors, [])
    console.log('PASS: S08 metric signs/semantics, scoped cohort values and times, no Team trend, held presets, saved restoration/URL precedence, scalar >50 links and independent off guards')
  } finally { await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
