// S31 rendered interaction checks use synthetic API responses. CapacityAllocationShiftTests owns real command/rollback evidence.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { allocationRow, allocationPage } = require('./allocations.fixture.cjs')
const { coordinationOptions } = require('./reviews.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-11-01T13:00:00Z'
const addWeeks = (date, weeks) => { const value = new Date(`${date}T00:00:00Z`); value.setUTCDate(value.getUTCDate() + weeks * 7); return value.toISOString().slice(0, 10) }
const rows = [
  allocationRow({ id: id(801), personId: id(811), personName: 'Alex Chen', status: 'Confirmed', plannedHours: 12.345, rowVersion: 1, fromDate: '2026-11-09', throughDate: '2026-11-13' }),
  allocationRow({ id: id(802), personId: id(812), personName: 'Jill Partial', plannedHours: 8, rowVersion: 1, fromDate: '2026-11-09', throughDate: '2026-11-13' }),
  allocationRow({ id: id(803), personId: id(813), personName: 'Sam Offpage', plannedHours: 8, rowVersion: 1, fromDate: '2026-11-09', throughDate: '2026-11-13' }),
  allocationRow({ id: id(804), personId: id(814), personName: 'Terminal Row', status: 'Cancelled', plannedHours: 9, rowVersion: 1, fromDate: '2026-11-09', throughDate: '2026-11-13' }),
]
const eligible = row => ['Proposed', 'Confirmed'].includes(row.status)
const unchangedRole = { id: id(820), label: 'Civil design', fromDate: '2026-11-09', throughDate: '2026-11-13' }
const comparison = { weeks: ['2026-11-09'], allocationCoverage: 'Complete', disciplines: [], asOf: stamp }

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page, mock } = ui
  const errors = [], unexpectedWrites = [], previews = [], attempts = [], receipts = new Map()
  let listReads = 0, supportingReads = 0, moves = 0, refuseNext = false, loseNextResponse = false
  const allocationRoot = `projects/${project.id}/allocations`
  try {
    page.setDefaultTimeout(10000)
    page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...me.flags, live_events: false, capacity_screens: false, allocation_preview: false }
    project.permissions.proposeAllocation = { ok: true, reason: null }; project.permissions.isPm = true
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (path === `${allocationRoot}/shift-preview`) {
        const body = request.postDataJSON(); previews.push(body)
        const chosen = body.mode === 'WholeProject' ? rows.filter(eligible) : rows.filter(row => body.allocationIds.includes(row.id))
        return route.fulfill({ json: { mode: body.mode, weeks: body.weeks, asOf: stamp, allocations: chosen.map(row => ({
          id: row.id, rowVersion: row.rowVersion, personId: row.personId, personName: row.personName, status: row.status,
          fromDate: row.fromDate, throughDate: row.throughDate, newFromDate: addWeeks(row.fromDate, body.weeks), newThroughDate: addWeeks(row.throughDate, body.weeks),
          requiresConfirmation: row.status === 'Confirmed', unchangedRoles: row.id === id(801) ? [unchangedRole] : [],
        })) } })
      }
      if (path === `${allocationRoot}/shift`) {
        const body = request.postDataJSON(); attempts.push(body)
        if (refuseNext) { refuseNext = false; return route.fulfill({ status: 409, json: { code: 'conflict', detail: 'Synthetic row version changed. Preview again.' } }) }
        if (!receipts.has(body.requestId)) {
          const chosen = body.allocations.map(value => rows.find(row => row.id === value.id))
          assert.ok(chosen.every(Boolean)); assert.ok(chosen.every((row, index) => row.rowVersion === body.allocations[index].rowVersion))
          for (const row of chosen) { row.fromDate = addWeeks(row.fromDate, body.weeks); row.throughDate = addWeeks(row.throughDate, body.weeks); row.status = 'Proposed'; row.rowVersion++ }
          moves++; receipts.set(body.requestId, { id: chosen[0].id, rowVersion: chosen[0].rowVersion })
        }
        if (loseNextResponse) { loseNextResponse = false; return route.abort('failed') }
        return route.fulfill({ json: receipts.get(body.requestId) })
      }
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') unexpectedWrites.push(`${request.method()} ${path}`)
      if (path === allocationRoot) { listReads++; return route.fulfill({ json: allocationPage([rows[0], rows[1], rows[3]], { pageSize: 3, totalCount: 4 }) }) }
      if (path === `${allocationRoot}/discipline-context`) { supportingReads++; return route.fulfill({ json: comparison }) }
      if (path === `${allocationRoot}/deadlines`) { supportingReads++; return route.fulfill({ json: { weeks: ['2026-11-09'], dated: [], undated: [], asOf: stamp } }) }
      if (path === `projects/${project.id}/open-roles`) return route.fulfill({ json: [] })
      if (path === `projects/${project.id}/changes/options`) return route.fulfill({ json: coordinationOptions({ actorId: me.id, people: rows.map(row => ({ id: row.personId, displayName: row.personName })) }) })
      return route.fallback()
    })
    const url = `${base}/projects/${project.projectNumber}/allocations?from=2026-11-09&purpose=Production&page=1`
    const shiftAll = page.getByRole('button', { name: 'Shift all', exact: true })
    const dialog = page.getByRole('dialog', { name: 'Shift allocation dates', exact: true })
    const apply = dialog.getByRole('button', { name: 'Apply shift', exact: true })
    const previewButton = dialog.getByRole('button', { name: 'Preview shift', exact: true })
    const previewRegion = dialog.getByRole('region', { name: 'Allocation shift preview', exact: true })
    const inspect = async () => { await previewButton.click(); await previewRegion.waitFor() }
    const openSelected = async () => { await shiftAll.click(); await page.getByRole('menuitem', { name: 'Shift selected (2)', exact: true }).click(); await dialog.waitFor() }
    await page.goto(url)
    await page.getByRole('heading', { name: 'Allocations', exact: true }).waitFor()
    await page.getByRole('button', { name: 'Terminal Row', exact: true }).waitFor()
    assert.equal(await shiftAll.count(), 0); assert.equal(await page.getByRole('checkbox', { name: /^Select / }).count(), 0)
    assert.equal(supportingReads, 0); assert.equal(previews.length, 0); assert.equal(attempts.length, 0)
    me.flags.capacity_screens = true
    await page.reload(); await shiftAll.waitFor()
    await page.getByRole('checkbox', { name: /^Select Alex Chen / }).check()
    await page.getByRole('checkbox', { name: /^Select Jill Partial / }).check()
    assert.equal(await page.getByRole('checkbox', { name: /^Select Terminal Row / }).count(), 0, 'terminal allocations are not selectable')
    assert.equal(await page.getByRole('button', { name: 'Sam Offpage', exact: true }).count(), 0)
    await openSelected()
    assert.equal(await apply.isDisabled(), true)
    await dialog.getByLabel('Weeks to shift', { exact: true }).fill('0')
    assert.equal(await previewButton.isDisabled(), true)
    await dialog.getByLabel('Weeks to shift', { exact: true }).fill('2')
    await inspect()
    assert.deepEqual(previews.at(-1), { mode: 'Selected', weeks: 2, allocationIds: [id(801), id(802)] })
    assert.equal(await previewRegion.locator('[data-shift-preview-row]').count(), 2)
    await previewRegion.getByText('2026-11-23 – 2026-11-27', { exact: true }).first().waitFor()
    await previewRegion.getByText('Returns to Proposed · confirmation required', { exact: true }).waitFor()
    await previewRegion.getByText('Filled role Civil design keeps its dates: 2026-11-09–2026-11-13', { exact: true }).waitFor()
    await previewRegion.focus(); assert.equal(await previewRegion.evaluate(element => document.activeElement === element), true)
    assert.equal(await apply.isDisabled(), true, 'preview does not replace the required reason')
    await dialog.getByLabel('Reason', { exact: true }).fill('Synthetic shift review')
    assert.equal(await apply.isDisabled(), false)
    await dialog.getByLabel('Weeks to shift', { exact: true }).fill('3')
    assert.equal(await previewRegion.count(), 0); assert.equal(await apply.isDisabled(), true, 'changed dates invalidate the old preview immediately')
    await inspect(); await previewRegion.getByText('2026-11-30 – 2026-12-04', { exact: true }).first().waitFor()
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    assert.equal(attempts.length, 0); assert.equal(moves, 0)

    await openSelected(); await inspect()
    await dialog.getByLabel('Reason', { exact: true }).fill('Synthetic move after review')
    rows[0].rowVersion++; refuseNext = true
    await apply.click(); await dialog.getByText('Synthetic row version changed. Preview again.', { exact: true }).waitFor()
    assert.equal(await apply.isDisabled(), true); assert.equal(await previewRegion.count(), 0)
    assert.equal(moves, 0); assert.equal(rows[0].fromDate, '2026-11-09'); assert.equal(rows[1].fromDate, '2026-11-09')
    await inspect()
    const readsBefore = listReads
    loseNextResponse = true
    await apply.click(); await dialog.getByText('The result could not be confirmed. Retry with the same inputs to check the original request safely.', { exact: true }).waitFor()
    assert.equal(moves, 1); assert.equal(await apply.isDisabled(), false)
    await apply.click(); await dialog.waitFor({ state: 'hidden' })
    await page.waitForFunction(() => document.querySelectorAll('input[type="checkbox"]:checked').length === 0)
    assert.equal(attempts.length, 3); assert.equal(moves, 1)
    assert.deepEqual(attempts[1], attempts[2], 'unknown-result retry retains the exact body, versions and request receipt')
    assert.notEqual(attempts[0].requestId, attempts[1].requestId, 'new version preview gets a fresh request')
    assert.deepEqual(attempts[1].allocations, [{ id: id(801), rowVersion: 2 }, { id: id(802), rowVersion: 1 }])
    await page.getByRole('cell', { name: '2026-11-23 – 2026-11-27', exact: true }).first().waitFor()
    assert.ok(listReads > readsBefore, 'success refreshes the entire allocation query scope')
    assert.equal(page.url(), url, 'apply preserves list filters and paging')
    assert.equal(rows[0].status, 'Proposed'); assert.equal(rows[0].plannedHours, 12.345)
    assert.equal(rows[2].fromDate, '2026-11-09'); assert.equal(rows[3].fromDate, '2026-11-09')

    await shiftAll.click(); await page.getByRole('menuitem', { name: 'Shift whole project · Proposed and Confirmed', exact: true }).click(); await dialog.waitFor()
    assert.match(await dialog.innerText(), /across all pages and filters/)
    await dialog.getByLabel('Weeks to shift', { exact: true }).fill('-2')
    await inspect()
    assert.deepEqual(previews.at(-1), { mode: 'WholeProject', weeks: -2, allocationIds: [] })
    assert.equal(await previewRegion.locator('[data-shift-preview-row]').count(), 3)
    await previewRegion.getByRole('rowheader', { name: 'Sam Offpage', exact: true }).waitFor()
    await previewRegion.getByText('2026-10-26 – 2026-10-30', { exact: true }).waitFor()
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    assert.equal(attempts.length, 3); assert.equal(moves, 1, 'whole-project preview and cancel never apply')
    await page.setViewportSize({ width: 375, height: 812 })
    await page.getByText('Open allocations on a tablet or computer. Your own assignments are in My Work.', { exact: true }).waitFor()
    assert.equal(await shiftAll.count(), 0)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    await page.setViewportSize({ width: 1440, height: 1000 })
    me.flags.capacity_screens = false
    const readCount = supportingReads, previewCount = previews.length
    await page.reload(); await page.getByRole('button', { name: 'Terminal Row', exact: true }).waitFor()
    assert.equal(await shiftAll.count(), 0); assert.equal(await page.getByRole('checkbox', { name: /^Select / }).count(), 0)
    assert.equal(supportingReads, readCount); assert.equal(previews.length, previewCount); assert.equal(attempts.length, 3)
    assert.deepEqual(unexpectedWrites, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: synthetic S31 selected/whole-project scope, exact dates, stale preview/refusal, confirmation and role disclosure, cancel, receipt retry, refreshed filters, phone and dark controls')
  } catch (error) { console.error('capacity-shift-diagnostics', { url: page.url(), previews, attempts, moves, listReads, supportingReads, errors, unexpectedWrites, unknown: mock.unknownGets }); console.error(await page.locator('body').innerText()); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
