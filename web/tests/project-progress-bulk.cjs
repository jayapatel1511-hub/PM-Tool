// S19 FR/AC-SCR-32/33: independent facts and per-row normal PATCH outcomes.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { projectRow, projectList } = require('./projects.fixture.cjs')
const { referenceData } = require('./me.fixture.cjs')
const { emptyPortfolio } = require('./portfolio.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
const ids = [project.id, ...[2, 3, 4, 5].map(i => `00000000-0000-0000-0008-${String(i).padStart(12, '0')}`)]
const numbers = ['P-MOCK', 'P-BULK-2', 'P-REFUSED', 'P-STALE', 'P-UNKNOWN']
const phaseId = '00000000-0000-0000-0008-000000000011', unitId = '00000000-0000-0000-0008-000000000012'
const rows = ids.map((id, i) => projectRow({ id, projectNumber: numbers[i], name: i === 0 ? 'Progress cohort' : `Synthetic project ${i + 1}`,
  startDate: '2026-01-05', targetCompletionDate: '2026-05-05', progressPct: i === 4 ? null : 50, rowVersion: i === 4 ? null : i + 1,
  progressFacts: { today: '2026-03-18', elapsedPct: i === 4 ? null : 60, progressPct: i === 4 ? null : 50 },
  staffing: { openRoles: 0, overCapacityPeople: 0, limitedScope: false, partialView: false, ownPlannerOnly: false, lookaheadWeeks: 4, from: '2026-03-16', through: '2026-04-12' }, span: { today: '2026-03-18', openMilestones: 0 } }))
const writes = [], errors = [], writeFaults = new Map()
;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  page.setDefaultTimeout(12000)
  page.on('pageerror', e => errors.push(e.message)); page.on('console', m => {
    if (m.type() !== 'error') return
    const faultedWrite = [...writeFaults.keys()].some(id => m.location().url.endsWith(`/api/v1/projects/${id}`))
    if (!/status of (403|409)/.test(m.text()) && !(faultedWrite && /status of 503|net::ERR_FAILED/.test(m.text()))) errors.push(m.text())
  })
  try {
    me.capabilities.portfolio = true
    await page.route('**/api/v1/projects?**', route => route.fulfill({ json: projectList(rows) }))
    await page.route('**/api/v1/reference', route => route.fulfill({ json: referenceData({ phases: [{ id: phaseId, name: 'Detailed design', isActive: true, sortOrder: 1 }] }) }))
    await page.route('**/api/v1/organisation', route => route.fulfill({ json: { units: [{ id: unitId, name: 'Civil operations', closedOn: null }] } }))
    await page.route('**/api/v1/portfolio**', route => route.fulfill({ json: { ...emptyPortfolio(), projects: rows.map(row => ({ ...row, trend: [] })) } }))
    await page.route('**/api/v1/projects/*', route => {
      if (route.request().method() !== 'PATCH') return route.fallback()
      const id = new URL(route.request().url()).pathname.split('/').pop(), body = route.request().postDataJSON()
      const version = Number((route.request().headers()['if-match'] ?? '').replaceAll('"', ''))
      writes.push({ id, body, version })
      const row = rows.find(r => r.id === id)
      if (id === ids[2]) return route.fulfill({ status: 403, json: { code: 'forbidden', detail: 'Only the project manager may edit this project.' } })
      if (version !== row.rowVersion) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Project changed after selection.' } })
      const fields = ['priority', 'phaseId', 'leadUnitId']
      const changed = fields.some(field => field in body && row[field] !== body[field])
      for (const field of fields) if (field in body) row[field] = body[field]
      if (changed) row.rowVersion++
      // Simulate a real save followed by a lost/unusable acknowledgement.
      if (writeFaults.get(id) === 'disconnect') return route.abort('failed')
      if (writeFaults.get(id) === 'server') return route.fulfill({ status: 503, json: { code: 'reply_failed', detail: 'Server reply failed after saving.' } })
      if (writeFaults.get(id) === 'malformed') return route.fulfill({ json: { id, rowVersion: null } })
      return route.fulfill({ json: { id, rowVersion: row.rowVersion } })
    })

    me.flags = { ...me.flags, project_screens: false, org_structure: false }
    await page.goto(`${base}/projects?mine=false`); await page.getByRole('link', { name: 'Progress cohort', exact: true }).first().waitFor()
    assert.equal(await page.locator('[data-project-progress-facts]:visible').count(), 0)
    assert.equal(await page.getByRole('checkbox', { name: 'Select P-MOCK', exact: true }).count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Set fields', exact: true }).count(), 0)

    me.flags.project_screens = true
    await page.reload()
    const firstFacts = page.locator('table tbody tr').filter({ has: page.getByRole('link', { name: 'Progress cohort', exact: true }) }).locator('[data-project-progress-facts]')
    await firstFacts.getByText('Elapsed 60 %', { exact: true }).waitFor(); await firstFacts.getByText('Progress 50 %', { exact: true }).waitFor()
    assert.equal(await firstFacts.locator('p').count(), 2); assert.equal(await firstFacts.locator('svg,progress,[role="progressbar"]').count(), 0)
    const colours = await firstFacts.locator('p').evaluateAll(nodes => nodes.map(node => getComputedStyle(node).color))
    assert.equal(colours[0], colours[1], 'the two plain facts share neutral text colour')
    assert.equal(await page.getByRole('checkbox', { name: 'Select P-UNKNOWN', exact: true }).isDisabled(), true, 'missing list version fails closed')

    await page.getByRole('checkbox', { name: 'Select P-STALE', exact: true }).click() // captures version 4
    rows[3].rowVersion = 99
    await page.getByRole('button', { name: /^Group by:/ }).click(); await page.getByRole('menuitemradio', { name: 'Office', exact: true }).click()
    await page.getByRole('checkbox', { name: 'Select shown projects', exact: true }).click() // keeps existing selected version 4
    await page.getByRole('button', { name: 'Set fields', exact: true }).click()
    const dialog = page.getByRole('dialog')
    assert.deepEqual(await dialog.getByLabel('Field', { exact: true }).locator('option').evaluateAll(options => options.map(option => option.value)), ['priority', 'phaseId'], 'Status and disabled Lead unit are absent')
    await dialog.getByRole('button', { name: 'Apply to selected projects', exact: true }).click()
    await dialog.getByText('2 applied · 0 unchanged · 2 refused · 0 unknown', { exact: true }).waitFor()
    await dialog.getByText('Only the project manager may edit this project.', { exact: true }).waitFor(); await dialog.getByText('Project changed after selection.', { exact: true }).waitFor()
    assert.equal(writes.length, 4)
    for (const [i, id] of ids.slice(0, 4).entries()) {
      const write = writes.find(w => w.id === id)
      assert.equal(write.version, i + 1); assert.equal(write.body.rowVersion, i + 1); assert.equal(write.body.priority, 'High'); assert.equal('status' in write.body, false)
    }
    assert.equal(rows[0].priority, 'High'); assert.equal(rows[1].priority, 'High'); assert.equal(rows[2].priority, 'Medium'); assert.equal(rows[3].priority, 'Medium')
    await dialog.getByRole('button', { name: 'Close', exact: true }).last().click()

    for (const name of ['P-MOCK', 'P-BULK-2']) await page.getByRole('checkbox', { name: `Select ${name}`, exact: true }).click()
    await page.getByRole('button', { name: 'Set fields', exact: true }).click()
    await dialog.getByLabel('Field', { exact: true }).selectOption('phaseId'); await dialog.getByLabel('Value', { exact: true }).selectOption(phaseId)
    await dialog.getByRole('button', { name: 'Apply to selected projects', exact: true }).click(); await dialog.getByText('2 applied · 0 unchanged · 0 refused · 0 unknown', { exact: true }).waitFor()
    assert.ok(writes.slice(-2).every(w => w.body.phaseId === phaseId && !('priority' in w.body) && !('status' in w.body)))
    await dialog.getByRole('button', { name: 'Close', exact: true }).last().click()

    me.flags.org_structure = true
    await page.reload(); await page.getByRole('checkbox', { name: 'Select P-MOCK', exact: true }).click()
    await page.getByRole('button', { name: 'Set fields', exact: true }).click()
    await dialog.getByLabel('Field', { exact: true }).selectOption('leadUnitId'); await dialog.getByLabel('Value', { exact: true }).selectOption(unitId)
    await dialog.getByRole('button', { name: 'Apply to selected projects', exact: true }).click(); await dialog.getByText('1 applied · 0 unchanged · 0 refused · 0 unknown', { exact: true }).waitFor()
    assert.equal(writes.at(-1).body.leadUnitId, unitId); assert.equal('status' in writes.at(-1).body, false)
    await dialog.getByRole('button', { name: 'Close', exact: true }).last().click()

    const uncertain = [rows[0], rows[1], rows[3]], selectedVersions = new Map(uncertain.map(row => [row.id, row.rowVersion]))
    writeFaults.set(ids[0], 'disconnect'); writeFaults.set(ids[1], 'server'); writeFaults.set(ids[3], 'malformed')
    for (const row of uncertain) await page.getByRole('checkbox', { name: `Select ${row.projectNumber}`, exact: true }).click()
    await page.getByRole('button', { name: 'Set fields', exact: true }).click()
    await dialog.getByLabel('Field', { exact: true }).selectOption('priority'); await dialog.getByLabel('Value', { exact: true }).selectOption('Low')
    const beforeUncertain = writes.length
    await dialog.getByRole('button', { name: 'Apply to selected projects', exact: true }).click()
    await dialog.getByText('0 applied · 0 unchanged · 0 refused · 3 unknown', { exact: true }).waitFor()
    assert.equal(await dialog.getByText('Outcome unknown', { exact: true }).count(), 3)
    await dialog.getByText('The server may have saved these changes. Refresh and check each project before selecting it again. No retry has been sent.', { exact: true }).waitFor()
    await dialog.getByText("The server response did not confirm this project's saved version.", { exact: true }).waitFor()
    assert.ok(uncertain.every(row => row.priority === 'Low'), 'all three simulated writes saved despite unconfirmed acknowledgements')
    assert.equal(writes.length, beforeUncertain + 3, 'each original write was sent exactly once')
    for (const write of writes.slice(beforeUncertain)) {
      assert.equal(write.version, selectedVersions.get(write.id)); assert.equal(write.body.rowVersion, selectedVersions.get(write.id))
    }
    const refresh = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/projects' && response.request().method() === 'GET')
    await dialog.getByRole('button', { name: 'Refresh project list', exact: true }).click(); await refresh
    assert.equal(writes.length, beforeUncertain + 3, 'refresh only reads; it never retries an uncertain PATCH')
    await dialog.getByText('0 applied · 0 unchanged · 0 refused · 3 unknown', { exact: true }).waitFor()
    assert.equal(await dialog.getByRole('button', { name: 'Apply to selected projects', exact: true }).count(), 0)
    await dialog.getByRole('button', { name: 'Close', exact: true }).last().click(); writeFaults.clear()

    // A new explicit selection after inspection uses the refreshed version; this is
    // an ordinary no-op command, not an automatic replay of an uncertain write.
    await page.getByRole('checkbox', { name: 'Select P-MOCK', exact: true }).click()
    await page.getByRole('button', { name: 'Set fields', exact: true }).click()
    await dialog.getByLabel('Value', { exact: true }).selectOption('Low')
    await dialog.getByRole('button', { name: 'Apply to selected projects', exact: true }).click()
    await dialog.getByText('0 applied · 1 unchanged · 0 refused · 0 unknown', { exact: true }).waitFor()
    assert.equal(writes.at(-1).version, rows[0].rowVersion)
    await dialog.getByRole('button', { name: 'Close', exact: true }).last().click()

    await page.goto(`${base}/portfolio`)
    const portfolioRow = page.locator('.portfolio-screen-table table tbody tr').filter({ has: page.getByRole('link', { name: /P-MOCK.*Progress cohort/ }) })
    const progressDetail = portfolioRow.locator('details').filter({ has: page.locator('summary').filter({ hasText: /^Dates and progress$/ }) })
    await progressDetail.locator('summary').click()
    await progressDetail.getByText('Elapsed 60 %', { exact: true }).waitFor(); await progressDetail.getByText('Progress 50 %', { exact: true }).waitFor()
    assert.equal(await progressDetail.locator('[data-project-progress-facts] p').count(), 2, 'the expanded on-screen detail contains exactly the two independent facts')
    me.flags.project_screens = false; await page.reload()
    assert.equal(await page.locator('summary').filter({ hasText: 'Dates and progress' }).count(), 0, 'Portfolio off-state retains its original details')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [])
    console.log('PASS: independent 60/50 facts and normal project PATCH permission/stale/field/version/unknown-outcome handling without retries')
  } catch (e) { console.error(await page.locator('body').innerText(), mock.unknownGets, writes, errors); throw e } finally { await ui.browser.close() }
})().catch(e => { console.error(e); process.exitCode = 1 })
