// S30 rendered acceptance with synthetic API responses. Real calculations/rollback/privacy are covered by CapacityAllocationScreenTests.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { coordinationOptions, coordinationTask } = require('./reviews.fixture.cjs')
const { allocationPage } = require('./allocations.fixture.cjs')
const { contractExample: roleExample } = require('./open-roles.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const alex = id(710), outside = id(711), task = id(712), role = id(713)
const weeks = ['2026-11-09', '2026-11-16', '2026-11-23'], stamp = '2026-10-31T13:00:00Z'
const compare = { weeks: ['2026-11-16'], allocationCoverage: 'Complete', asOf: stamp, disciplines: [{
  projectDisciplineId: project.disciplines[0].id, disciplineName: 'Civil', unattributed: false, unattributedAllocations: 0, undatedTasks: 0,
  weeks: [{ week: '2026-11-16', confirmedHours: 60, knownConfirmedHours: 60, unavailableConfirmed: 0, proposedHours: 16, knownProposedHours: 16, unavailableProposed: 0, remainingEstimateHours: 90, knownRemainingEstimateHours: 90, unestimatedTasks: 0 }],
}] }
function capacity(body, mode = 'Complete') {
  if (body.personId === outside) return { personId: null, fromDate: body.fromDate, throughDate: body.throughDate, state: 'OutsideScope', includesProposal: false, unestimatedTasks: null, undatedTasks: null, weeks: [], asOf: stamp }
  return { personId: body.personId, fromDate: body.fromDate, throughDate: body.throughDate, state: mode, includesProposal: body.includeProposal,
    unestimatedTasks: mode === 'Partial' ? null : 0, undatedTasks: mode === 'Partial' ? null : 0, asOf: stamp,
    weeks: weeks.map((week, index) => ({ week, availableHours: mode === 'Provisional' && index === 0 ? null : 40,
      beforeHours: mode === 'Partial' ? null : [5, 36, 0][index], afterHours: mode === 'Partial' || !body.includeProposal ? null : [15, 46, 10][index],
      overCapacity: mode === 'Partial' || mode === 'Provisional' && index === 0 || !body.includeProposal ? null : index === 1,
      provisional: mode === 'Provisional' && index === 0 })),
  }
}

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page, mock } = ui
  const errors = [], writes = [], readBodies = [], commandPreviews = [], comparisonReads = []
  let mode = 'Complete'
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...me.flags, live_events: false, capacity_screens: false, allocation_preview: false }
    project.permissions.proposeAllocation = { ok: true, reason: null }
    const allocationRoot = `projects/${project.id}/allocations`
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (path === `${allocationRoot}/capacity-preview`) {
        const body = request.postDataJSON(); readBodies.push(body)
        return route.fulfill({ json: capacity(body, mode) })
      }
      if (request.method() === 'POST' && path === allocationRoot && url.searchParams.get('preview') === 'true') {
        const body = request.postDataJSON(); commandPreviews.push(body)
        const comparison = capacity({ ...body, includeProposal: true }, mode)
        return route.fulfill({ json: { preview: true, weeks: [], ...(me.flags.capacity_screens ? { comparison } : {}) } })
      }
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') writes.push(path)
      if (path === allocationRoot) return route.fulfill({ json: allocationPage([]) })
      if (path === `${allocationRoot}/discipline-context`) { comparisonReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: compare }) }
      if (path === `${allocationRoot}/deadlines`) return route.fulfill({ json: { weeks, dated: [], undated: [], asOf: stamp } })
      if (path === `${allocationRoot}/review-options`) return route.fulfill({ json: [] })
      if (path === `projects/${project.id}/open-roles`) return route.fulfill({ json: [{ ...roleExample, id: role, roleLabel: 'Three-week Civil role', fromDate: weeks[0], throughDate: '2026-11-27', plannedHours: 30 }] })
      if (path === `projects/${project.id}/changes/options`) return route.fulfill({ json: coordinationOptions({ actorId: me.id,
        people: [{ id: alex, displayName: 'Alex Chen' }, { id: outside, displayName: 'Outside planner scope' }],
        tasks: [coordinationTask({ id: task, key: 'T-710', name: 'Civil allocation work', ownerId: alex, projectDisciplineId: project.disciplines[0].id })],
      }) })
      return route.fallback()
    })
    const path = `${base}/projects/${project.projectNumber}/allocations?from=2026-11-16&q=nomatch&status=Cancelled&page=99`
    await page.goto(path)
    await page.getByRole('heading', { name: 'Allocations', exact: true }).waitFor()
    assert.equal(await page.getByRole('region', { name: 'Allocations beside task estimates', exact: true }).count(), 0)
    assert.equal(comparisonReads.length, 0)
    me.flags.capacity_screens = true
    await page.reload()
    const comparison = page.getByRole('region', { name: 'Allocations beside task estimates', exact: true })
    await comparison.waitFor()
    // `has` is relative to each row: do not include the outer region again in its inner locator.
    const civil = comparison.getByRole('row').filter({ has: page.getByRole('rowheader', { name: 'Civil', exact: true }) })
    await Promise.all(['60 h', '16 h', '90 h'].map(value => civil.getByText(value, { exact: true }).waitFor()))
    assert.deepEqual(await civil.locator('[data-allocation-confirmed], [data-allocation-proposed], [data-allocation-estimate]').allTextContents(), ['60 h', '16 h', '90 h'])
    assert.doesNotMatch(await comparison.innerText(), /176/)
    assert.equal(await comparison.getAttribute('tabindex'), '0')
    assert.ok((await page.getByText('No matching allocations', { exact: true }).boundingBox()).y < (await comparison.boundingBox()).y, 'primary allocation result precedes the supporting comparison')
    assert.deepEqual(comparisonReads.at(-1), { from: '2026-11-16' }, 'comparison does not inherit list search, approval filter or paging')

    const fillUrl = `${base}/projects/${project.projectNumber}/allocations?from=2026-11-16&fill=${role}`
    await page.goto(fillUrl)
    const dialog = page.getByRole('dialog', { name: 'Fill Three-week Civil role', exact: true })
    await dialog.waitFor()
    await dialog.getByLabel('Person', { exact: true }).selectOption(alex)
    const preview = dialog.getByRole('region', { name: 'Weekly capacity before and after', exact: true })
    await preview.waitFor()
    assert.equal(await preview.locator('[data-allocation-capacity-week]').count(), 3)
    assert.deepEqual(await preview.locator('[data-capacity-available]').allTextContents(), ['40 h', '40 h', '40 h'])
    assert.deepEqual(await preview.locator('[data-capacity-after]').allTextContents(), ['—', '—', '—'])
    assert.equal(readBodies.at(-1).includeProposal, false)
    await dialog.getByLabel('Linked task 1', { exact: true }).selectOption(task)
    await page.waitForFunction(() => document.querySelector('[data-allocation-capacity-week="2026-11-16"] [data-capacity-after]')?.textContent === '46 h')
    assert.deepEqual(await preview.locator('[data-capacity-before]').allTextContents(), ['5 h', '36 h', '0 h'])
    assert.deepEqual(await preview.locator('[data-capacity-after]').allTextContents(), ['15 h', '46 h', '10 h'])
    await preview.getByText('Over capacity', { exact: true }).waitFor()
    assert.equal(readBodies.at(-1).includeProposal, true)
    assert.equal(commandPreviews.length, 0, 'section52 off uses the pure read')
    await preview.focus(); assert.equal(await preview.evaluate(element => document.activeElement === element), true)
    const outsideResponse = page.waitForResponse(response => response.url().includes('/allocations/capacity-preview') && response.request().postDataJSON()?.personId === outside)
    await dialog.getByLabel('Person', { exact: true }).selectOption(outside)
    assert.equal(await dialog.locator('[data-allocation-capacity-week]').count(), 0, 'old-person figures clear immediately on input change')
    assert.equal((await (await outsideResponse).json()).state, 'OutsideScope')
    await page.waitForFunction(() => document.querySelector('[data-allocation-capacity]') === null)

    // The full form uses the existing rolled-back command preview when §52 is enabled.
    me.flags.allocation_preview = true
    await page.reload(); await dialog.waitFor()
    await dialog.getByLabel('Person', { exact: true }).selectOption(alex)
    await dialog.getByLabel('Linked task 1', { exact: true }).selectOption(task)
    await page.waitForFunction(() => document.querySelector('[data-allocation-capacity-week="2026-11-16"] [data-capacity-after]')?.textContent === '46 h')
    assert.equal(commandPreviews.at(-1).openRoleId, role)
    assert.equal(commandPreviews.at(-1).fromDate, weeks[0]); assert.equal(commandPreviews.at(-1).throughDate, '2026-11-27')
    mode = 'Partial'
    await dialog.getByLabel('Planned hours', { exact: true }).fill('31')
    await dialog.getByText(/Some work is outside your project access/).waitFor()
    assert.deepEqual(await preview.locator('[data-capacity-before]').allTextContents(), ['Unavailable', 'Unavailable', 'Unavailable'])
    assert.deepEqual(await preview.locator('[data-capacity-after]').allTextContents(), ['Unavailable', 'Unavailable', 'Unavailable'])
    mode = 'Provisional'
    await dialog.getByLabel('Planned hours', { exact: true }).fill('32')
    await dialog.getByText(/Some availability is provisional/).waitFor()
    assert.equal(await preview.locator('[data-capacity-available]').first().innerText(), 'Unavailable')
    assert.equal(await preview.locator('[data-capacity-assessment]').first().innerText(), 'Unavailable')
    await page.setViewportSize({ width: 375, height: 812 })
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), 'mobile overflow stays inside the named keyboard scroll regions')
    await page.keyboard.press('Escape')
    me.flags.capacity_screens = false; me.flags.allocation_preview = false
    const count = comparisonReads.length, readCount = readBodies.length
    await page.goto(`${base}/projects/${project.projectNumber}/allocations?from=2026-11-16`)
    await page.getByRole('heading', { name: 'Allocations', exact: true }).waitFor()
    assert.equal(await page.locator('[data-allocation-confirmed], [data-allocation-capacity]').count(), 0)
    assert.equal(comparisonReads.length, count); assert.equal(readBodies.length, readCount)
    assert.deepEqual(writes, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: synthetic S30 60/16/90 separation, exact three-week Fill baseline/before/after, canonical preview selection, stale-person clearing, scope/partial/provisional truth, keyboard scroll, mobile and dark controls')
  } catch (error) { console.error('capacity-allocation-diagnostics', { url: page.url(), comparisonReads, readBodies, commandPreviews, writes, errors, unknown: mock.unknownGets }); console.error(await page.locator('body').innerText()); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
