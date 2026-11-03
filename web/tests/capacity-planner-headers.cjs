// S27 rendered acceptance: raw server cells feed bars and month/% views; mocks are explicitly synthetic.
const assert = require('node:assert/strict')
const { newMockPage, me, project, panelIds } = require('./platform-fixtures.cjs')
const { planningGrid, planningCell, planningPerson, planningEntry, cellDetail, entryPage } = require('./planning.fixture.cjs')
const { referenceData, referenceDiscipline } = require('./me.fixture.cjs')
const { allocationPage } = require('./allocations.fixture.cjs')
const { coordinationOptions } = require('./reviews.fixture.cjs')
const { contractExample: roleExample } = require('./open-roles.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = id(500), amy = id(501), ben = id(502), chen = id(503)
const weeks = ['2026-11-23', '2026-11-30', '2026-12-07']
const roles = [
  { ...roleExample, id: id(511), roleLabel: 'Counted open role A', status: 'Open', canManage: false },
  { ...roleExample, id: id(512), roleLabel: 'Counted open role B', status: 'Open', canManage: false },
  { ...roleExample, id: id(513), roleLabel: 'Excluded filled role', status: 'Filled', canManage: false },
  { ...roleExample, id: id(514), roleLabel: 'Excluded cancelled role', status: 'Cancelled', canManage: false },
]
const people = [
  planningPerson({ id: amy, displayName: 'Amy Boundary', homeDisciplineId: civil, cells: [
    planningCell({ week: weeks[0], confirmed: 16, expected: 16, possible: 8, remaining: 8 }),
    planningCell({ week: weeks[1], confirmed: 24, expected: 8, possible: 4, remaining: 8 }),
    planningCell({ week: weeks[2], capacity: 20, expected: 4, remaining: 16 }),
  ] }),
  planningPerson({ id: ben, displayName: 'Ben Partial', homeDisciplineId: civil, partial: true, cells: weeks.map(week => planningCell({ week })) }),
  planningPerson({ id: chen, displayName: 'Chen Zero', homeDisciplineId: civil, cells: weeks.map(week => planningCell({ week, capacity: 0, remaining: 0 })) }),
]
const deadlines = { weeks, asOf: '2026-11-23T13:00:00Z', dated: [
  { itemType: 'Milestone', id: panelIds.milestone, key: 'M-001', name: 'Boundary milestone', projectId: project.id, projectNumber: project.projectNumber, date: '2026-11-30' },
  { itemType: 'Deliverable', id: panelIds.deliverable, key: 'D-001', name: 'Header drawing one', projectId: project.id, projectNumber: project.projectNumber, date: '2026-12-01' },
  { itemType: 'Deliverable', id: id(509), key: 'D-002', name: 'Header drawing two', projectId: project.id, projectNumber: project.projectNumber, date: '2026-12-03' },
], undated: [{ itemType: 'Deliverable', id: id(510), key: 'D-003', name: 'Undated header drawing', projectId: project.id, projectNumber: project.projectNumber, date: null }] }
const grid = planningGrid({ weeks, currentWeek: weeks[0], people, partialView: true,
  settings: { horizonWeeks: 3, overPct: 105, underPct: 40, underWeeks: 1, staleDays: 28, maxHoursPerWeek: 80 } })

;(async () => {
  const ui = await newMockPage(base, { events: false })
  const { page, mock } = ui
  const errors = [], writes = [], reads = [], inspectedWeeks = [], allocationListReads = []
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...me.flags, live_events: false, capacity_screens: false, project_screens: false, home_overview: false }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') writes.push(path)
      if (path === 'reference') return route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')] }) })
      if (path === 'planning/grid') {
        reads.push(Object.fromEntries(url.searchParams))
        return route.fulfill({ json: { ...grid, ...(me.flags.capacity_screens ? { deadlines } : {}), ...(url.searchParams.get('indicator') === 'Above100' ? { people: [] } : {}) } })
      }
      if (path === 'planning/entries') return route.fulfill({ json: entryPage([planningEntry({ id: id(599), label: 'Synthetic list amount', personId: amy, personName: 'Amy', ownerId: me.id, hoursPerWeek: 8, startWeek: weeks[0], endWeek: weeks[0], sourceCategory: 'Training', confidence: 'Expected', visibility: 'Published', canEdit: false })]) })
      if (path.startsWith('planning/cells/')) {
        const [, , personId, week] = path.split('/')
        inspectedWeeks.push(week)
        return route.fulfill({ json: cellDetail({ personId, personName: 'Amy Boundary', week }) })
      }
      if (path === `projects/${project.id}/allocations/deadlines`) return route.fulfill({ json: deadlines })
      if (path === `projects/${project.id}/allocations/discipline-context`) return route.fulfill({ json: { weeks, allocationCoverage: 'Complete', disciplines: [], asOf: '2026-11-23T13:00:00Z' } })
      if (path === `projects/${project.id}/allocations`) { allocationListReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: allocationPage([]) }) }
      if (path === `projects/${project.id}/open-roles`) return route.fulfill({ json: roles })
      if (path === `projects/${project.id}/changes/options`) return route.fulfill({ json: coordinationOptions() })
      if (/^items\/(Milestone|Deliverable)\/[^/]+\/links$/.test(path)) return route.fulfill({ json: [] })
      if (/^deliverables\/[^/]+\/baselines$/.test(path)) return route.fulfill({ json: [] })
      return route.fallback()
    })
    await page.goto(`${base}/planner?from=${weeks[0]}&weeks=3&groupBy=homeDiscipline&unit=percent&zoom=months`)
    await page.getByRole('heading', { name: 'Team Planner', exact: true }).waitFor()
    assert.equal(await page.getByRole('button', { name: /^Units:/ }).count(), 0, 'off ignores the new display controls')
    assert.equal(await page.locator('[data-planner-month]').count(), 0)
    assert.equal(await page.locator('[data-planning-summary]').count(), 0)
    assert.equal(await page.locator('[data-deadline-markers]').count(), 0)
    me.flags.capacity_screens = true
    await page.goto(`${base}/planner?from=${weeks[0]}&weeks=3&groupBy=homeDiscipline`)
    await page.getByRole('button', { name: 'Units: Hours', exact: true }).waitFor()
    assert.equal(await page.locator('[data-planning-summary]').count(), 3, 'one summary bar per week for the three-person group')
    const firstBar = await page.locator('[data-planning-summary]').first().getAttribute('aria-label')
    assert.match(firstBar, /Confirmed confidence and approved allocations 16 h/)
    assert.match(firstBar, /Expected confidence 16 h/); assert.match(firstBar, /Possible confidence 8 h/); assert.match(firstBar, /capacity line 80 h/)
    assert.equal(await page.locator('td[data-subtotal="40"]').count(), 1, 'the original numeric subtotal row stays')
    const planningDetails = page.getByText('Team Planner · Details', { exact: true })
    assert.ok((await page.getByRole('grid').boundingBox()).y < (await planningDetails.boundingBox()).y, 'supporting method detail follows the working grid')
    await planningDetails.click()
    await page.getByText('Percentages show Confirmed plus Expected hours divided by effective capacity. Possible is separate. Partial figures describe visible plans only. Exports keep the weekly hours basis.', { exact: true }).waitFor()
    await planningDetails.click()
    const weekHead = page.getByRole('grid').locator('thead th').filter({ hasText: 'Nov 30' })
    await weekHead.getByRole('button', { name: 'Deliverable · 2 · 2026-12-01, 2026-12-03', exact: true }).waitFor()
    await weekHead.getByRole('button', { name: 'Milestone · 1 · 2026-11-30', exact: true }).waitFor()
    await weekHead.getByRole('button', { name: /^Deliverable · 2/ }).click()
    await page.getByRole('button', { name: /P-MOCK · D-001/ }).click()
    await page.waitForURL(url => url.searchParams.get('panel') === `Deliverable:${panelIds.deliverable}`)
    await page.getByRole('dialog').getByText('Synthetic deliverable', { exact: true }).first().waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(url => !url.searchParams.has('panel'))
    assert.equal(new URL(page.url()).searchParams.get('groupBy'), 'homeDiscipline')
    await page.getByText('No date · 1 linked deadlines', { exact: true }).click()
    await page.getByRole('button', { name: /Undated header drawing/ }).waitFor()

    await page.getByRole('button', { name: 'Units: Hours', exact: true }).click()
    await page.getByRole('menuitemradio', { name: '% of capacity', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('unit') === 'percent')
    const amyWeek = page.getByRole('grid').locator('tbody tr').filter({ has: page.getByRole('rowheader', { name: /Amy Boundary/ }) })
    await amyWeek.getByRole('button', { name: /80 %/ }).first().waitFor()
    await page.getByRole('button', { name: 'Zoom: Weeks', exact: true }).click()
    await page.getByRole('menuitemradio', { name: 'Months', exact: true }).click()
    await page.waitForURL(url => url.searchParams.get('zoom') === 'months')
    const monthGrid = page.getByRole('region', { name: 'Monthly planning totals', exact: true })
    await monthGrid.waitFor({ state: 'visible' })
    assert.equal(await monthGrid.locator('th[data-planner-month="2026-11"]').count(), 1)
    assert.equal(await monthGrid.locator('th[data-planner-month="2026-12"]').count(), 1)
    assert.match(await monthGrid.locator('th[data-planner-month="2026-12"]').innerText(), /Displayed weeks: 2/, 'November 30 belongs to the December ISO-week group')
    const amyMonth = monthGrid.locator('tbody tr').filter({ has: page.getByRole('rowheader', { name: 'Amy Boundary', exact: true }) })
    assert.deepEqual(await amyMonth.locator('[data-planner-month-value]').allTextContents(), ['80 %', '60 %'], 'month % is 36/60, not the 50% average of 80% and 20% weeks')
    assert.equal(await monthGrid.getByText('Percentage unavailable', { exact: true }).count(), 2, 'zero denominators stay unavailable')
    await amyMonth.getByRole('button', { name: /^Amy Boundary · Dec 2026 · choose a week · 60 %$/ }).click()
    await page.getByRole('button', { name: /^2026-11-30/ }).click()
    const contributions = page.getByRole('dialog', { name: 'View weekly allocations', exact: true })
    await contributions.waitFor(); assert.equal(inspectedWeeks.at(-1), '2026-11-30')
    await contributions.getByRole('button', { name: 'Close', exact: true }).click()
    await page.keyboard.press('Escape')
    await page.reload()
    await page.getByRole('button', { name: 'Units: % of capacity', exact: true }).waitFor()
    await page.getByRole('button', { name: 'Zoom: Months', exact: true }).waitFor()

    await page.goto(`${base}/projects/${project.projectNumber}/allocations?from=${weeks[0]}`)
    const allocationHeaders = page.getByRole('region', { name: 'Project deadlines by week', exact: true })
    await allocationHeaders.getByRole('button', { name: /^Deliverable · 2/ }).waitFor()
    await allocationHeaders.getByText('No date · 1 linked deadlines', { exact: true }).waitFor()
    const roleUrl = `${base}/projects/${project.projectNumber}/allocations?tab=roles&roleStatus=Open&status=Confirmed`
    await page.goto(roleUrl)
    await page.getByText('Counted open role A', { exact: true }).waitFor()
    assert.equal(await page.getByText('Excluded filled role', { exact: true }).count(), 1, 'project flag off ignores the additive role selector')
    assert.equal(await page.getByText('Excluded cancelled role', { exact: true }).count(), 1)
    me.flags.project_screens = true; me.flags.record_engine_additions = true
    await page.reload()
    await page.getByRole('button', { name: 'Remove filter: Role status', exact: true }).waitFor()
    assert.equal(await page.getByText('Counted open role A', { exact: true }).count(), 1)
    assert.equal(await page.getByText('Counted open role B', { exact: true }).count(), 1)
    assert.equal(await page.getByText('Excluded filled role', { exact: true }).count(), 0)
    assert.equal(await page.getByText('Excluded cancelled role', { exact: true }).count(), 0)
    assert.equal(allocationListReads.at(-1).status, 'Confirmed', 'roleStatus stays independent from allocation approval status')
    assert.equal(allocationListReads.at(-1).roleStatus, undefined, 'the role selector is not sent to the allocation API')
    await page.getByRole('button', { name: 'Export', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Open roles · all statuses · CSV (.csv)', exact: true }).waitFor()
    await page.keyboard.press('Escape')
    await page.getByRole('button', { name: 'Remove filter: Role status', exact: true }).click()
    await page.waitForURL(url => !url.searchParams.has('roleStatus'))
    await page.getByText('Excluded filled role', { exact: true }).waitFor()
    await page.getByText('Excluded cancelled role', { exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('status'), 'Confirmed')
    me.flags.project_screens = true; me.flags.home_overview = true
    await page.goto(`${base}/planner?from=${weeks[0]}&weeks=3&teamProjectId=${project.id}&homeDisciplineId=${civil}&indicator=Above100`)
    await page.getByText('Above 100% of capacity', { exact: true }).waitFor()
    await page.getByText('Project team', { exact: false }).first().waitFor()
    assert.equal(reads.at(-1).teamProjectId, project.id); assert.equal(reads.at(-1).homeDisciplineId, civil); assert.equal(reads.at(-1).indicator, 'Above100')
    // The actual Planner page's list component owns its visual flag hook, independently of the grid harness.
    me.flags.visual_system = true
    await page.goto(`${base}/planner?layout=list&from=${weeks[0]}`)
    await page.getByRole('button', { name: 'Synthetic list amount', exact: true }).waitFor()
    const amountCell = page.getByRole('row').filter({ hasText: 'Synthetic list amount' }).locator('td').nth(4)
    assert.equal(await amountCell.innerText(), '8 h')
    assert.equal(await amountCell.locator('.planner-semantic-quantity').count(), 1)
    assert.equal(await amountCell.locator('.planner-semantic-quantity').evaluate(element => getComputedStyle(element).fontSize), '19px')
    me.flags.visual_system = false; await page.reload()
    await page.getByRole('button', { name: 'Synthetic list amount', exact: true }).waitFor()
    assert.equal(await amountCell.innerText(), '8 h'); assert.equal(await amountCell.locator('.planner-semantic-quantity').count(), 0)
    assert.deepEqual(writes, [], 'header/unit/zoom/inspection controls never write domain data')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: synthetic planner subtotals/accessible bars, exact dated record open/return, no-date tray, URL units, Thursday month grouping, weighted percent, zero denominator, week inspection, allocation headers and scoped URL selectors')
  } catch (error) { console.error('planner-header-diagnostics', { url: page.url(), reads, inspectedWeeks, unknown: mock.unknownGets, errors }); console.error(await page.locator('body').innerText()); throw error } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
