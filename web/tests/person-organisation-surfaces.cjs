// Actual built SPA with synthetic API reads. The coordinator alone runs this suite.
// PersonPanelTests and the existing person-panel.cjs retain server authority/denial coverage.
const assert = require('node:assert/strict')
const { newMockPage, task } = require('./platform-fixtures.cjs')
const f = require('./person-organisation-surfaces.fixture.cjs')
const { dashboardView } = require('./dashboard.fixture.cjs')
const { emptyPortfolio } = require('./portfolio.fixture.cjs')
const { timeRow, timeView } = require('./time.fixture.cjs')
const { planningGrid, planningPerson, planningCell } = require('./planning.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the coordinator local preview')

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], writes = [], previews = [], personReads = [], capacityReads = [], directories = [], observations = []
  let denial = false
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => {
      const url = new URL(request.url())
      if (!url.pathname.startsWith('/api/') || ['GET', 'HEAD', 'OPTIONS'].includes(request.method())) return
      if (url.pathname === '/api/v1/me/sign-in') return // dev-session bootstrap only
      if (url.pathname === '/api/v1/planning/leave/import-preview' && request.method() === 'POST') previews.push(request.postDataJSON())
      else writes.push({ method: request.method(), path: url.pathname })
    })
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        if (path === 'planning/leave/import-preview' && request.method() === 'POST') return route.fulfill({ json: f.preview })
        return route.fulfill({ status: 405, json: { detail: 'No business writes in Person selection checks' } })
      }
      if (path === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: f.actor })
      if (path === 'reference') return route.fulfill({ json: f.reference })
      if (path === 'team') return route.fulfill({ json: f.workspace })
      if (path === 'organisation') return route.fulfill({ json: f.organisation })
      if (path === 'organisation/placements') return route.fulfill({ json: f.organisation.people.map(p => ({ id: p.id, unitId: p.unitId })) })
      if (path === 'admin/users') return route.fulfill({ json: f.adminUsers })
      if (path === 'admin/organisation/levels') return route.fulfill({ json: f.organisation.levels })
      if (path === 'admin/organisation/units') return route.fulfill({ json: f.organisation.units.map(u => ({ ...u, rowVersion: 1 })) })
      if (path === 'certification-tags') return route.fulfill({ json: [] })
      if (path === 'templates') return route.fulfill({ json: [] })
      if (path === 'users') { directories.push(url.href); return route.fulfill({ json: f.people.map(p => ({ ...p, isActive: true })) }) }
      if (path === 'projects') return route.fulfill({ json: f.projects })
      if (path === 'projects/' + f.sourceProject.id || path === 'projects/' + f.sourceProject.projectNumber) return route.fulfill({ json: f.sourceProject })
      if (path === 'projects/' + f.sourceProject.id + '/team') return route.fulfill({ json: f.team })
      if (path === 'projects/' + f.sourceProject.id + '/open-roles') return route.fulfill({ json: [] })
      if (path === 'projects/' + f.sourceProject.id + '/deliverables') return route.fulfill({ json: [] })
      if (path === 'projects/' + f.sourceProject.id + '/milestones') return route.fulfill({ json: [] })
      if (path === 'projects/' + f.sourceProject.id + '/attention') return route.fulfill({ json: { items: [], snoozed: 0, total: 0, truncated: false } })
      if (path === 'projects/' + f.sourceProject.id + '/dashboard') return route.fulfill({ json: dashboardView({
        dueThisWeek: { items: [{ ...task, assigneeId: f.ids.person, assigneeName: f.people[0].displayName }], total: 1, link: 'due=week' },
        blocked: { items: [{ ...task, assigneeId: f.ids.person, assigneeName: f.people[0].displayName }], total: 1, link: 'blocked=true' } }) })
      if (path === 'portfolio') return route.fulfill({ json: { ...emptyPortfolio(), projects: f.projects.items.map(row => ({ ...row, trend: [] })) } })
      if (path === 'planning/availability') return route.fulfill({ json: f.availability })
      if (path === 'workload') return route.fulfill({ json: f.workload })
      if (path === 'planning/hr-identities') return route.fulfill({ json: f.identities })
      if (path === 'planning/grid') {
        const ids = (url.searchParams.get('personIds') ?? '').split(',').filter(Boolean)
        capacityReads.push({ ids, from: url.searchParams.get('from'), weeks: url.searchParams.get('weeks') })
        return route.fulfill({ json: ids.length ? planningGrid({ weeks: [f.week], currentWeek: f.week, people: ids.map(id => planningPerson({ id,
          displayName: f.people.find(p => p.id === id)?.displayName ?? 'Synthetic scoped User', cells: [planningCell({ week: f.week })] })) }) : f.grid })
      }
      if (path === 'planning/entries') return route.fulfill({ json: { items: [], page: 1, pageSize: 50, totalCount: 0 } })
      if (path === 'time') return route.fulfill({ json: timeView('2026-10-26', '2026-11-01', [timeRow({ id: f.ids.preview, userId: f.ids.person, projectId: f.ids.project,
        taskId: task.id, person: f.people[0].displayName, projectNumber: f.sourceProject.projectNumber, workDate: '2026-10-26', canEdit: false })], true) })
      if (/^people\/[^/]+\/panel$/.test(path)) {
        const id = path.split('/')[1]
        personReads.push({ id, params: Object.fromEntries(url.searchParams) })
        if (denial) return route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Not found', status: 404, code: 'not_found', detail: 'Synthetic person is no longer visible' }) })
        return route.fulfill({ json: f.personRead(id, Number(url.searchParams.get('projectsPage') ?? 1), Number(url.searchParams.get('projectsPageSize') ?? 25)) })
      }
      return route.fallback()
    })
    const goto = path => page.goto(base + path)
    const person = id => page.locator('[data-person-panel="' + id + '"]')
    const sheet = () => page.getByRole('dialog').filter({ has: page.locator('[data-person-panel]') })
    const trigger = (id, scope = page) => scope.locator('[data-person-panel-trigger="' + id + '"]:visible').first()
    const close = () => sheet().getByRole('button', { name: 'Close', exact: true }).click()
    const unique = async label => {
      const result = await page.locator('[data-person-panel-trigger]').evaluateAll(nodes => ({
        ids: nodes.map(n => n.id), nested: nodes.filter(n => n.parentElement?.closest('button,a,[role="option"],summary')).map(n => n.id),
      }))
      assert.equal(new Set(result.ids).size, result.ids.length, label + ': every mounted trigger ID is unique')
      assert.deepEqual(result.nested, [], label + ': Person actions have no interactive ancestor')
    }
    const roundtrip = async (opener, id) => {
      const original = page.url(), focusId = await opener.getAttribute('id')
      await opener.focus(); await page.keyboard.press('Enter'); await person(id).waitFor()
      assert.equal(new URL(page.url()).searchParams.get('panel'), 'Person:' + id)
      await close(); await page.waitForURL(original)
      await page.waitForFunction(id => document.activeElement?.id === id, focusId)
    }
    const teamUrl = '/team?projects=' + f.ids.project + '&group=supervisor&officeId=' + f.ids.office
    await goto(teamUrl)
    for (const id of [f.ids.supervisor, f.ids.otherSupervisor]) {
      await page.locator('th[scope="rowgroup"] [data-person-panel-trigger="' + id + '"]').waitFor()
    }
    assert.equal(await page.locator('th[scope="rowgroup"] [data-person-panel-trigger]').count(), 2, 'Duplicate names form separate supervisor-ID groups')
    await unique('workspace Team table and hidden phone cards')
    // Person -> supervisor -> exact paged Person -> unchanged Team scope and focus.
    const source = trigger(f.ids.person), sourceId = await source.getAttribute('id')
    await source.click(); await person(f.ids.person).waitFor()
    await person(f.ids.person).getByRole('button', { name: 'Next', exact: true }).click()
    await person(f.ids.person).getByRole('link', { name: 'P-SYN-25 · Synthetic permitted project 25', exact: true }).waitFor()
    const nestedUrl = page.url(), supervisor = trigger(f.ids.supervisor, person(f.ids.person)), supervisorFocus = await supervisor.getAttribute('id')
    await sheet().evaluate(node => { node.scrollTop = 80 })
    const savedScroll = await sheet().evaluate(node => node.scrollTop)
    assert.ok(savedScroll > 0, 'Nested return starts from an actual nonzero sheet scroll')
    await supervisor.click(); await person(f.ids.supervisor).waitFor()
    await close(); await page.waitForURL(nestedUrl); await person(f.ids.person).waitFor()
    await page.waitForFunction(id => document.activeElement?.id === id, supervisorFocus)
    assert.equal(new URL(page.url()).searchParams.get('personProjectsPage'), '2')
    assert.ok(Math.abs(await sheet().evaluate(node => node.scrollTop) - savedScroll) <= 2, 'Nested return restores sheet scroll')
    await close(); await page.waitForURL(base + teamUrl)
    await page.waitForFunction(id => document.activeElement?.id === id, sourceId)
    assert.ok(personReads.every(r => !r.params.staffScope), 'Ordinary name actions grant no Staff scope')
    assert.ok(capacityReads.filter(r => r.ids.length).every(r => r.ids.length === 1 && r.from === f.week && r.weeks === '1'), 'Panel capacity reads only the known target/current week')
    observations.push('Team duplicate supervisor IDs, nested page/scope/scroll/focus return')

    // The custom member phone cards and generic workspace phone cards have distinct focus keys.
    await page.setViewportSize({ width: 375, height: 900 }); await goto(teamUrl)
    await roundtrip(trigger(f.ids.supervisor), f.ids.supervisor); await unique('workspace Team at 375')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Workspace cards fit 375px')
    await goto('/projects/' + f.sourceProject.projectNumber + '/team?group=supervisor')
    await roundtrip(trigger(f.ids.otherSupervisor), f.ids.otherSupervisor); await unique('project Team at 375')

    // Unit leader and placement names are separate from expansion and existing edit controls.
    await goto('/organisation')
    const unit = page.getByRole('button', { name: /Synthetic organisation unit/ })
    await unit.click(); assert.equal(await unit.getAttribute('aria-expanded'), 'true')
    const leader = unit.locator('..').getByText(/^Leader:/).locator('[data-person-panel-trigger="' + f.ids.person + '"]')
    await roundtrip(leader, f.ids.person); assert.equal(await unit.getAttribute('aria-expanded'), 'true')
    await roundtrip(trigger(f.ids.unplaced), f.ids.unplaced); await unique('organisation leader and placement')
    assert.equal(await page.locator('[data-person-panel-trigger="' + f.ids.office + '"]').count(), 0, 'Office IDs are never User links')
    assert.equal(await page.locator('[data-person-panel-trigger="' + f.ids.discipline + '"]').count(), 0, 'Discipline IDs are never User links')
    await page.setViewportSize({ width: 1440, height: 1000 }) // Administration keeps its established desktop/tablet boundary.
    await goto('/admin/structure')
    const placement = page.locator('select').filter({ has: page.locator('option[value="' + f.ids.unit + '"]') })
    await placement.first().waitFor(); const controlsBefore = await placement.count()
    await roundtrip(page.locator('[data-person-panel-trigger="' + f.ids.other + '"][id*="cycle"]'), f.ids.other)
    assert.equal(await placement.count(), controlsBefore, 'Existing placement/parent controls survive Person selection')
    await unique('admin structure warning and placement')
    await goto('/admin/users')
    const editButtons = page.getByRole('button', { name: 'Edit', exact: true })
    await editButtons.first().waitFor(); const editCount = await editButtons.count()
    await roundtrip(trigger(f.ids.supervisor), f.ids.supervisor)
    assert.equal(await editButtons.count(), editCount, 'Existing User editors are preserved')
    await unique('admin User and supervisor columns')
    observations.push('375px Team, Organisation expansion, leader/unplaced names and admin selectors')

    // A dirty import preview remains a preview; selecting its Person cannot map or import anything.
    await page.setViewportSize({ width: 375, height: 900 })
    await goto('/vacation?year=2026&month=10')
    const coverage = page.getByRole('region', { name: 'Coverage concerns', exact: true })
    const coverageRows = coverage.getByRole('row').filter({ has: page.locator('[data-person-panel-trigger="' + f.ids.person + '"]') })
    await coverageRows.nth(1).getByRole('cell', { name: 'Reviewer', exact: true }).waitFor()
    assert.equal(await coverageRows.count(), 2, 'Same Task/User has separate Assignee and Reviewer concerns')
    await coverageRows.nth(0).getByRole('cell', { name: 'Assignee', exact: true }).waitFor()
    const assigneeFocus = await trigger(f.ids.person, coverageRows.nth(0)).getAttribute('id')
    const reviewerOpener = trigger(f.ids.person, coverageRows.nth(1)), reviewerFocus = await reviewerOpener.getAttribute('id')
    assert.notEqual(reviewerFocus, assigneeFocus, 'Coverage role is part of the stable opener identity')
    await unique('Vacation same Task/User in two supported coverage roles')
    await roundtrip(reviewerOpener, f.ids.person)
    await page.waitForFunction(id => document.activeElement?.id === id, reviewerFocus)
    observations.push('Vacation same Task/User Assignee/Reviewer IDs and natural return to second Reviewer opener')
    await page.getByRole('button', { name: 'Working schedule', exact: true }).click()
    const schedule = page.getByRole('dialog').filter({ has: page.locator('#schedule-hours') })
    await schedule.locator('#schedule-hours').fill('41.5')
    await roundtrip(trigger(f.ids.person, schedule), f.ids.person)
    assert.equal(await schedule.locator('#schedule-hours').inputValue(), '41.5', 'Unsaved schedule hours survive Person return')
    await schedule.getByRole('button', { name: 'Cancel', exact: true }).click()
    await page.getByRole('button', { name: 'Import approved leave', exact: true }).click()
    const imported = page.getByRole('dialog', { name: 'Import approved leave', exact: true })
    const csv = 'source_system,external_id,person_key,status,start_date,end_date,hours_per_day,source_updated_at\nSynthetic HR,synthetic-preview-1,' + f.ids.person + ',Approved,2026-10-27,2026-10-28,,2026-10-01T00:00:00Z'
    await imported.locator('#leave-csv').fill(csv)
    await imported.getByRole('button', { name: 'Preview import', exact: true }).click()
    const previewPerson = imported.locator('.max-h-64 [data-person-panel-trigger="' + f.ids.person + '"]').last()
    await previewPerson.waitFor()
    const previewId = await previewPerson.getAttribute('id')
    await previewPerson.click(); await person(f.ids.person).waitFor(); await close()
    await imported.waitFor(); assert.equal(await imported.locator('#leave-csv').inputValue(), csv)
    assert.equal(await imported.getByRole('button', { name: 'Apply reviewed import', exact: true }).isEnabled(), true)
    await page.waitForFunction(id => document.activeElement?.id === id, previewId)
    assert.equal(previews.length, 1); assert.deepEqual(previews[0], { csv })
    assert.deepEqual(writes, [], 'Person selection never maps identities, imports, saves schedules or edits work')
    await unique('Vacation preview at 375')
    observations.push('Dirty Vacation CSV/preview and source focus retained; only explicit preview POST')
    await imported.locator('#leave-csv').fill('')
    await imported.getByRole('button', { name: 'Cancel', exact: true }).click()

    // PM popover and compact metadata are independently addressable; header geometry stays bounded.
    await goto('/projects/' + f.sourceProject.projectNumber + '/dashboard')
    const header = page.locator('[data-visual-project-header]')
    await header.waitFor()
    await page.evaluate(async () => { await document.fonts.ready; await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))) })
    const phoneHeight = await header.evaluate(node => node.getBoundingClientRect().height)
    assert.ok(phoneHeight >= 63 && phoneHeight <= 72, 'Phone header retains natural height at most 72px: ' + phoneHeight)
    await unique('Dashboard PM metadata and repeated task assignee')
    await roundtrip(header.locator('[data-person-panel-trigger="' + f.ids.person + '"]'), f.ids.person)
    await page.setViewportSize({ width: 1440, height: 1000 })
    await header.getByRole('button', { name: 'Project details and actions', exact: true }).click()
    const details = page.locator('[data-visual-project-details]')
    await details.waitFor()
    await roundtrip(details.locator('[data-person-panel-trigger="' + f.ids.person + '"]'), f.ids.person)
    await details.waitFor(); await unique('PM metadata and retained popover')
    await page.keyboard.press('Escape'); await details.waitFor({ state: 'hidden' })
    await page.evaluate(async () => { await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))) })
    assert.ok(Math.abs(await header.evaluate(node => node.getBoundingClientRect().height) - 64) <= 1, 'Desktop header stays 64px')
    observations.push('PM metadata/popover nested return, Escape dismissal, 375/1440 header bounds')

    // Planner grouping links the exact ID without merging names or changing subtotal row arithmetic.
    await goto('/planner?groupBy=supervisor&from=' + f.week + '&weeks=1')
    for (const id of [f.ids.supervisor, f.ids.otherSupervisor]) await page.locator('th[scope="rowgroup"] [data-person-panel-trigger="' + id + '"]').waitFor()
    assert.equal(await page.locator('th[scope="rowgroup"] [data-person-panel-trigger]').count(), 2)
    await unique('planner supervisor summary and heading')
    await roundtrip(page.locator('th[scope="rowgroup"] [data-person-panel-trigger="' + f.ids.otherSupervisor + '"]'), f.ids.otherSupervisor)
    await goto('/planner?groupBy=supervisor&zoom=months&from=' + f.week + '&weeks=1')
    await page.locator('[data-planner-month]').first().waitFor(); await unique('planner month grouping')
    await roundtrip(page.locator('th[scope="rowgroup"] [data-person-panel-trigger="' + f.ids.supervisor + '"]'), f.ids.supervisor)
    await goto('/workload?groupBy=supervisor&from=' + f.week)
    for (const id of [f.ids.supervisor, f.ids.otherSupervisor]) await page.locator('th[scope="rowgroup"] [data-person-panel-trigger="' + id + '"]').waitFor()
    assert.deepEqual(await page.locator('td[data-subtotal]').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-subtotal'))), ['8', '8'], 'Each typed supervisor bucket retains its own subtotal')
    await roundtrip(page.locator('th[scope="rowgroup"] [data-person-panel-trigger="' + f.ids.supervisor + '"]'), f.ids.supervisor)
    await unique('Workload duplicate supervisor grouping')

    // HTML PM links are distinct from the intentionally static print table; scoped Time names use entry IDs.
    f.actor.flags.project_screens = true
    await goto('/portfolio'); await roundtrip(trigger(f.ids.person), f.ids.person); await unique('portfolio screen and print')
    const printPm = page.locator('.portfolio-print-table td[data-portfolio-column="pm"]')
    await printPm.waitFor({ state: 'attached' })
    assert.ok((await printPm.textContent()).includes(f.people[0].displayName), 'Static print PM retains the full name beside avatar initials')
    assert.equal(await page.locator('.portfolio-print-table [data-person-panel-trigger]').count(), 0, 'Print PM is readable data, with no hidden duplicate trigger')
    f.actor.flags.project_screens = false
    await goto('/time?scope=all&from=2026-10-26&to=2026-11-01')
    await roundtrip(page.locator('table [data-person-panel-trigger="' + f.ids.person + '"]').first(), f.ids.person)

    // Restricted viewers see permitted facts but never request planning capacity.
    f.actor.capabilities.workload = false; f.actor.flags.capacity_screens = false
    const beforeRestricted = capacityReads.length
    await goto('/organisation'); await page.getByRole('button', { name: /Synthetic organisation unit/ }).click()
    await trigger(f.ids.person).click(); await person(f.ids.person).waitFor()
    assert.equal(await person(f.ids.person).locator('[data-capacity]').count(), 0)
    await close(); assert.equal(capacityReads.length, beforeRestricted, 'No forbidden planner read for restricted viewer')
    // Actual refreshed denied read: reload intentionally clears route state and receives the 404 answer.
    await trigger(f.ids.person).click(); await person(f.ids.person).waitFor(); denial = true
    const answered = page.waitForResponse(r => r.url().includes('/people/' + f.ids.person + '/panel') && r.status() === 404)
    await page.reload(); const deniedResponse = await answered
    assert.equal((await deniedResponse.json()).detail, 'Synthetic person is no longer visible')
    await page.getByRole('dialog', { name: 'Person', exact: true }).getByRole('alert').filter({ hasText: 'This page or item does not exist, or you cannot view it.' }).waitFor()
    assert.equal(await page.locator('[data-person-panel]').count(), 0, 'An answered denied read renders no profile')
    denial = false
    // Dark people flag uses original labels and exposes no Person actions/reads.
    f.actor.flags.people_screens = false
    const darkReads = personReads.length
    for (const path of [teamUrl, '/organisation', '/vacation?year=2026&month=10', '/admin/users', '/projects/' + f.sourceProject.projectNumber + '/dashboard']) {
      await goto(path)
      if (path === '/organisation') await page.getByRole('button', { name: /Synthetic organisation unit/ }).click()
      await page.getByText(f.people[0].displayName, { exact: true }).first().waitFor()
      assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0, 'Dark Person links: ' + path)
    }
    assert.equal(personReads.length, darkReads)
    assert.deepEqual(writes, []); assert.deepEqual(directories, [], 'No directory read added by passive labels')
    assert.deepEqual(errors, []); assert.deepEqual(ui.mock.unknownGets, []); assert.deepEqual(ui.mock.apiErrors, [])
    console.log(JSON.stringify({ result: 'PASS', observations, previewRequests: previews.length, businessWrites: writes.length, personReads: personReads.length }))
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
