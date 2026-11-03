// 053 S26/S28 rendered workflow checks against a labelled synthetic API; backend truth is tested separately.
const assert = require('node:assert/strict')
const { newMockPage, me } = require('./platform-fixtures.cjs')
const { planningGrid, planningCell, planningPerson } = require('./planning.fixture.cjs')
const { referenceData, referenceDiscipline } = require('./me.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const civil = id(400), amy = id(401), zara = id(402), partial = id(403), provisional = id(404)
const weeks = ['2026-09-14', '2026-09-21', '2026-09-28', '2026-10-05']
const allWeeks = [...weeks, '2026-10-12', '2026-10-19', '2026-10-26', '2026-11-02']
const grid = planningGrid({ weeks, currentWeek: weeks[0], settings: { horizonWeeks: 4, overPct: 110, underPct: 40, underWeeks: 1, staleDays: 28, maxHoursPerWeek: 80 },
  people: [planningPerson({ id: amy, displayName: 'Amy synthetic', homeDisciplineId: civil, cells: weeks.map(week => planningCell({ week, confirmed: 32, remaining: 8 })) })] })
const roomResult = { weeks, hoursPerWeek: 8, horizonWeeks: 4, asOf: '2026-09-14T13:00:00Z', matches: [
  { id: amy, displayName: 'Amy synthetic', homeDisciplineId: civil, weeks: weeks.map(week => ({ week, freeHours: 8 })) },
  { id: zara, displayName: 'Zara synthetic', homeDisciplineId: civil, weeks: weeks.map(week => ({ week, freeHours: 32 })) },
], unavailable: [{ id: partial, displayName: 'Partial synthetic', reason: 'PartialView' }, { id: provisional, displayName: 'Provisional synthetic', reason: 'ProvisionalAvailability' }] }
function workload(scenario, enabled) {
  return { weeks: allWeeks, today: weeks[0], currentWeek: weeks[0], defaultCapacity: 40, thresholds: { overPct: 110, amberPct: 90, underPct: 40 },
    people: [{ id: amy, displayName: 'Amy synthetic', supervisorId: null, supervisorName: null, officeId: null, jobTitle: null, homeDisciplineId: civil,
      capacity: 40, capacityOverride: false, noDueDate: 0, openTasks: 1, unestimated: 0, overdue: 0, projects: 1, overAssigned: false, underAssigned: false,
      cluster: false, canSetCapacity: false, partialScope: false, overWeek: null, indicator: 'ok', cells: allWeeks.map((week, index) => ({ week,
        hours: index === 0 ? 8 : 0, pct: index === 0 ? (scenario ? 40 : 20) : 0, available: 40, confirmed: scenario && index === 0 ? 16 : 0,
        proposed: index === 0 ? 16 : 0, committed: index === 0 ? (scenario ? 16 : 8) : 0, band: 'light', uncommitted: index === 0 ? (scenario ? 24 : 32) : 40,
        ...(scenario && index === 0 ? { includesProposed: true } : {}) })) }], unfilledRoles: [],
    ...(enabled ? { ...(scenario ? { includesProposed: true } : {}), disciplineContext: [{ disciplineId: civil, disciplineName: 'Civil', people: 1,
      undatedUnassignedTasks: 1, weeks: allWeeks.map((week, index) => ({ week, freeHours: index === 1 ? null : 40, unavailablePeople: index === 1 ? 1 : 0,
        unfilledRoleHours: index === 0 ? 30 : 0, knownUnassignedTaskHours: index === 0 ? 20 : 0, unestimatedTasks: index === 1 ? 1 : 0,
        shortfallHours: index === 0 ? 10 : index === 1 ? null : 0 })) }] } : {}) }
}

;(async () => {
  const ui = await newMockPage(base, { events: false })
  const { page, mock } = ui
  const errors = [], searches = [], writes = [], scenarioReads = [], scenarioRequests = []
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...me.flags, live_events: false, capacity_screens: false }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), pathname = url.pathname.replace('/api/v1/', '')
      if (!['GET', 'HEAD'].includes(request.method()) && pathname !== 'me/sign-in') writes.push(pathname)
      if (pathname === 'reference') return route.fulfill({ json: referenceData({ disciplines: [referenceDiscipline(civil, 'Civil')] }) })
      if (pathname === 'planning/grid') return route.fulfill({ json: grid })
      if (pathname === 'planning/find-room') { searches.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: roomResult }) }
      if (pathname === 'workload') {
        const scenario = [...url.searchParams].some(([key, value]) => key.toLowerCase() === 'showproposedasconfirmed' && value === 'true')
        scenarioReads.push(scenario)
        scenarioRequests.push({ url: url.toString(), scenario, capacityScreens: me.flags.capacity_screens })
        return route.fulfill({ json: workload(scenario, me.flags.capacity_screens) })
      }
      return route.fallback()
    })
    await page.goto(`${base}/planner?from=${weeks[0]}`)
    await page.getByRole('heading', { name: 'Team Planner', exact: true }).waitFor()
    assert.equal(await page.getByRole('button', { name: 'Find room', exact: true }).count(), 0, 'dark planner has no new control')
    me.flags.capacity_screens = true
    await page.reload()
    await page.getByRole('button', { name: 'Find room', exact: true }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Home discipline', { exact: true }).selectOption(civil)
    await dialog.getByRole('button', { name: 'Find room', exact: true }).click()
    await dialog.getByText('2 matches with at least 8 h free in every one of 4 weeks', { exact: true }).waitFor()
    const names = await dialog.getByRole('table').locator('tbody th[scope="row"]').allTextContents()
    assert.deepEqual(names, ['Amy synthetic', 'Zara synthetic'])
    assert.equal(await dialog.getByRole('table').getByText('8 h free', { exact: true }).count(), 4)
    assert.equal(await dialog.getByRole('table').getByText('32 h free', { exact: true }).count(), 4)
    await dialog.getByRole('heading', { name: 'Availability not verified', exact: true }).waitFor()
    await dialog.getByText('Partial synthetic', { exact: true }).waitFor()
    await dialog.getByText('Provisional availability', { exact: true }).waitFor()
    assert.equal(searches.length, 1)
    assert.deepEqual(Object.fromEntries(Object.entries(searches[0]).map(([key, value]) => [key.toLowerCase(), value])), { from: weeks[0], hoursperweek: '8', weeks: '4', disciplineid: civil })
    await dialog.getByRole('button', { name: 'Close', exact: true }).click()

    await page.goto(`${base}/workload?from=${weeks[0]}`)
    const context = page.getByRole('region', { name: 'Capacity and demand by home discipline', exact: true })
    await context.waitFor()
    assert.equal(await context.getAttribute('tabindex'), '0', 'discipline demand table has a named keyboard scroll region')
    await context.getByText('Short by 10 h', { exact: true }).waitFor()
    await context.getByText('Free hours unavailable for 1 people', { exact: true }).waitFor()
    await context.getByText('1 unassigned tasks with no due date', { exact: true }).waitFor()
    const scenarioToggle = page.getByRole('checkbox', { name: 'Show proposed as confirmed', exact: true })
    const openScenarioFilters = async () => { if (!await scenarioToggle.isVisible()) await page.getByRole('button', { name: /^Filters/ }).click(); await scenarioToggle.waitFor() }
    const primaryGrid = page.getByRole('region', { name: 'Workload by person and week', exact: true })
    assert.ok((await primaryGrid.boundingBox()).y < (await context.boundingBox()).y, 'working person/week rows precede supporting discipline context')
    await openScenarioFilters()
    assert.equal(await scenarioToggle.isChecked(), false, 'normal view starts unchecked')
    await Promise.all([
      page.waitForURL(url => url.searchParams.get('showProposedAsConfirmed') === 'true'),
      scenarioToggle.click(),
    ])
    await page.waitForFunction(() => document.querySelector('[data-band]')?.textContent?.includes('Includes proposed'))
    assert.equal(await scenarioToggle.isChecked(), true, 'URL navigation leaves the scenario checked')
    assert.equal(scenarioReads.at(-1), true, 'the scenario read includes the proposed query parameter')
    assert.equal(await page.getByRole('status').filter({ hasText: /^Includes proposed$/ }).count(), 1, 'the view band states the scenario')
    assert.equal(await page.locator('[data-band]').filter({ hasText: 'Includes proposed' }).count(), 1, 'only changed cells are labelled')
    assert.equal(await page.locator('[data-band]').first().getByText(/not counted/).count(), 0)
    assert.match(await page.locator('[data-band]').first().locator('p').first().innerText(), /16 h/, 'the changed cell uses the scenario load')
    assert.match(new URL(page.url()).search, /showProposedAsConfirmed=true/)
    assert.equal(new URL(page.url()).searchParams.get('from'), weeks[0], 'the start-week filter survives the scenario')
    await Promise.all([
      page.waitForURL(url => !url.searchParams.has('showProposedAsConfirmed')),
      scenarioToggle.click(),
    ])
    await page.waitForFunction(() => !document.querySelector('[data-band]')?.textContent?.includes('Includes proposed'))
    assert.equal(await scenarioToggle.isChecked(), false, 'turning the scenario off leaves the checkbox unchecked')
    assert.match(await page.locator('[data-band]').first().locator('p').first().innerText(), /8 h/, 'normal load is restored')
    assert.equal(await page.getByRole('status').filter({ hasText: /^Includes proposed$/ }).count(), 0, 'the scenario band clears')
    // Restoring a fresh cached normal query need not fetch again. Reload the resulting URL to independently
    // verify that its next network read is normal too, rather than asserting a cache-policy side effect.
    const beforeNormalReload = scenarioReads.length
    await page.reload()
    await context.getByText('Short by 10 h', { exact: true }).waitFor()
    assert.ok(scenarioReads.length > beforeNormalReload, 'the normal URL reload performs a fresh Workload read')
    assert.equal(scenarioReads.at(-1), false, 'the restored URL reads without proposed-as-confirmed')
    await openScenarioFilters()
    assert.equal(await scenarioToggle.isChecked(), false)
    me.flags.capacity_screens = false
    await page.reload()
    assert.equal(await page.getByRole('checkbox', { name: 'Show proposed as confirmed', exact: true }).count(), 0)
    assert.equal(await page.getByRole('heading', { name: 'Capacity and demand by home discipline', exact: true }).count(), 0)
    assert.deepEqual(writes, [], 'both workflows stay read-only')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: synthetic Find room dialog, safe unavailable group, discipline context, reversible scenario, changed-cell labels and flag-off controls')
  } catch (error) {
    console.error('capacity-scenario-diagnostics', { url: page.url(), scenarioReads, scenarioRequests,
      checkboxes: await page.locator('input[type="checkbox"]').evaluateAll(inputs => inputs.map(input => ({ checked: input.checked, label: input.closest('label')?.textContent }))).catch(() => []) })
    console.error(await page.locator('body').innerText(), mock.unknownGets, errors)
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
