// S42 rendered synthetic acceptance. TeamCompletenessTests owns normal-command cohort/privacy evidence.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { teamMember, teamDiscipline, projectTeam } = require('./team.fixture.cjs')
const { planningGrid, planningPerson, planningCell } = require('./planning.fixture.cjs')
const { savedView, viewList, viewSaved } = require('./views.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const homeA = id(4510), homeB = id(4511), halifax = id(4512), otherOffice = id(4513), supervisorA = id(4514), supervisorB = id(4515), unitA = id(4516), unitB = id(4517)
const reference = (id, name) => ({ id, name })
const cohort = [{ userId: me.id, displayName: 'Alex Team', home: reference(homeA, 'Civil'), office: reference(halifax, 'Halifax'), supervisor: reference(supervisorA, 'Same supervisor'), unit: reference(unitA, 'Current unit') },
  { userId: id(4502), displayName: 'Jill Team', home: reference(homeB, 'Civil'), office: reference(halifax, 'Halifax'), supervisor: reference(supervisorB, 'Same supervisor'), unit: reference(unitB, 'Current unit') },
  { userId: id(4503), displayName: 'Unknown Team', home: null, office: reference(halifax, 'Halifax'), supervisor: null, unit: null },
  { userId: id(4504), displayName: 'Other office person', home: reference(homeA, 'Civil'), office: reference(otherOffice, 'Dartmouth'), supervisor: reference(supervisorA, 'Same supervisor'), unit: reference(unitA, 'Current unit') }]
const notices = { personType: 'Co-op', startDate: '2026-11-01', endDate: '2026-12-18', starts: true, ends: true }
const facts = { notices, certifications: [] }
const org = row => ({ homeDiscipline: row.home, office: row.office, supervisor: row.supervisor, ...(me.flags.org_structure ? { unit: row.unit } : {}) })
const members = () => cohort.map((row, index) => teamMember({ id: id(4550 + index), userId: row.userId, displayName: row.displayName,
  organisationFacts: org(row), personFacts: row.userId === cohort[1].userId ? facts : null }))
const workspace = () => ({ projects: 1, people: cohort.map(row => ({ userId: row.userId, displayName: row.displayName, jobTitle: null, email: 'synthetic@example.test', organisationFacts: org(row),
  personFacts: row.userId === cohort[1].userId ? facts : null, projects: [{ projectId: project.id, projectNumber: project.projectNumber, projectName: project.name, roles: ['TeamMember'], leads: [] }] })) })
const roles = [0, 1, 2].map(index => ({ id: id(4560 + index), projectId: project.id, roleLabel: `Synthetic open role ${index + 1}`,
  projectDisciplineId: project.disciplines[0].id, disciplineName: 'Civil', fromDate: '2026-10-26', throughDate: '2026-10-30', plannedHours: index ? 8 : 12,
  status: index === 2 ? 'Filled' : 'Open', filledAllocationId: null, note: null, rowVersion: 0, canManage: true }))

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], writes = [], gridReads = [], directoryReads = [], teamReads = [], views = []
  let provisional = false
  try {
    page.setDefaultTimeout(12000); page.on('pageerror', error => errors.push(error.message))
    me.capabilities = { ...me.capabilities, admin: true, staff: true, workload: true, readOnly: false }
    me.settings.today = '2026-10-31'; me.settings.dateFormat = 'yyyy-MM-dd'
    me.flags = { ...me.flags, live_events: false, onboarding: false, people_screens: true, capacity_screens: true, capacity_polish: false, org_structure: true, visual_system: false, home_overview: false }
    project.permissions = { ...project.permissions, proposeAllocation: { ok: true } }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', ''), method = request.method()
      if (method !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        const body = request.postDataJSON(); writes.push({ path, method, body })
        if (path === 'views') {
          const view = savedView({ id: id(4580 + views.length), name: body.name, listType: body.listType, projectId: body.projectId ?? null, scope: body.scope, params: body.params })
          views.push(view); return route.fulfill({ status: 201, json: viewSaved(view.id, view.rowVersion) })
        }
        return route.fulfill({ json: { id: id(4599), rowVersion: 1 } })
      }
      if (path === 'views') return route.fulfill({ json: viewList(views.filter(view => view.listType === url.searchParams.get('listType'))) })
      if (path === 'team') { teamReads.push(url.href); return route.fulfill({ json: workspace() }) }
      if (path === `projects/${project.id}/team`) return route.fulfill({ json: projectTeam({ members: members(), canManage: true,
        disciplines: [teamDiscipline({ id: project.disciplines[0].id, name: 'Civil', leadUserId: me.id, leadName: me.displayName })] }) })
      if (path === `projects/${project.id}/open-roles`) return route.fulfill({ json: roles })
      if (path === 'certification-tags') return route.fulfill({ json: [] })
      if (path === 'users') { directoryReads.push(url.href); return route.fulfill({ json: cohort.map(row => ({ id: row.userId, displayName: row.displayName,
        ...(row.userId === cohort[1].userId ? { personNotices: notices } : {}) })) }) }
      if (path === 'planning/grid') {
        const requested = (url.searchParams.get('personIds') ?? '').split(','); gridReads.push({ from: url.searchParams.get('from'), weeks: url.searchParams.get('weeks'), requested })
        return route.fulfill({ json: planningGrid({ weeks: ['2026-10-26'], currentWeek: '2026-10-26', people: [
          planningPerson({ id: me.id, displayName: 'Alex Team', cells: [planningCell({ week: '2026-10-26', remaining: 12, timeAway: 8, provisional })] }),
          planningPerson({ id: cohort[1].userId, displayName: 'Jill Team', partial: true, cells: [planningCell({ week: '2026-10-26', remaining: 40 })] }),
        ].filter(person => requested.includes(person.id)) }) })
      }
      return route.fallback()
    })
    const projectUrl = `/projects/${project.projectNumber}/team`
    const goto = path => page.goto(`${base}${path}`)
    const table = name => page.getByRole('table', { name, exact: true })
    const desktopPeople = name => table(name).locator('tbody tr').filter({ has: page.locator('td') })
    const waitPeople = async (name, count) => {
      await table(name).waitFor()
      await page.waitForFunction(({ name, count }) => {
        const table = [...document.querySelectorAll('table')].find(element => element.caption?.textContent.trim() === name)
        return table && [...table.querySelectorAll('tbody tr')].filter(row => row.querySelector('td')).length === count
      }, { name, count })
    }
    async function selectFilter(prefix, facet, value) {
      await page.getByRole('button', { name: /^Filters/ }).last().click()
      await page.locator(`#${prefix}-${facet}`).selectOption(value); await page.keyboard.press('Escape')
      await page.waitForURL(url => url.searchParams.get(`${facet}Id`) === value)
    }
    async function save(name) {
      await page.getByRole('button', { name: 'Views', exact: true }).click(); await page.getByRole('menuitem', { name: 'Save current view…', exact: true }).click()
      const dialog = page.getByRole('dialog', { name: 'Save this view', exact: true }); await dialog.getByLabel('Name', { exact: true }).fill(name)
      const write = page.waitForRequest(request => new URL(request.url()).pathname.endsWith('/views') && request.method() === 'POST')
      await dialog.getByRole('button', { name: 'Save', exact: true }).click(); const body = (await write).postDataJSON(); await dialog.waitFor({ state: 'detached' }); return body
    }
    // Workspace scope is retained while grouping and filters remain presentation-only IDs.
    await goto(`/team?projects=${project.id}&group=homeDiscipline&officeId=${halifax}`)
    await table('Team').waitFor(); await waitPeople('Team', 3); assert.equal(await desktopPeople('Team').count(), 3)
    const groups = await table('Team').locator('th[scope="rowgroup"]').allTextContents()
    assert.equal(groups.length, 3); assert.ok(groups.some(text => text.includes(homeA))); assert.ok(groups.some(text => text.includes(homeB))); assert.ok(groups.some(text => text.includes('Not set')))
    assert.ok((await desktopPeople('Team').allTextContents()).every(text => !text.includes('Other office person')))
    assert.ok(teamReads.every(href => new URL(href).searchParams.get('projects') === project.id))
    await page.locator('#workspace-team-group').selectOption('supervisor')
    await table('Team').locator('th[scope="rowgroup"]').filter({ hasText: supervisorA }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('officeId'), halifax)
    const supervisors = await table('Team').locator('th[scope="rowgroup"]').allTextContents()
    assert.equal(supervisors.length, 3); assert.ok(supervisors.some(text => text.includes(supervisorA))); assert.ok(supervisors.some(text => text.includes(supervisorB)))
    await selectFilter('workspace-team', 'supervisor', supervisorB); await waitPeople('Team', 1); assert.equal(await desktopPeople('Team').count(), 1)
    assert.ok((await desktopPeople('Team').innerText()).includes('Jill Team'))
    const saved = await save('Synthetic Halifax supervisor view')
    assert.equal(saved.listType, 'workspace-team'); assert.equal(saved.params.projects, project.id); assert.equal(saved.params.group, 'supervisor')
    assert.equal(saved.params.officeId, halifax); assert.equal(saved.params.supervisorId, supervisorB); assert.ok(saved.params.cols.includes('homeDiscipline'))
    await page.reload(); await table('Team').waitFor(); await waitPeople('Team', 1); assert.equal(await desktopPeople('Team').count(), 1)
    await goto(`/team?projects=${project.id}&cols=homeDiscipline`); await table('Team').waitFor()
    assert.equal(await table('Team').getByRole('columnheader', { name: 'Office', exact: true }).count(), 0)
    await page.getByRole('button', { name: 'Columns', exact: true }).click()
    const officeColumn = page.getByRole('menuitemcheckbox', { name: 'Office', exact: true })
    assert.equal(await officeColumn.isChecked(), false, 'The column chooser must reflect the authoritative URL columns')
    await officeColumn.click(); await page.keyboard.press('Escape')
    await page.waitForURL(url => !url.searchParams.has('cols'))
    await table('Team').getByRole('columnheader', { name: 'Office', exact: true }).waitFor()
    assert.equal(await table('Team').getByRole('columnheader', { name: 'Office', exact: true }).count(), 1)
    // Project grouping retains exact member/lead controls and keeps role records outside the people table.
    await goto(`${projectUrl}?group=homeDiscipline&officeId=${halifax}`)
    await table('Members').waitFor(); await waitPeople('Members', 3); assert.equal(await desktopPeople('Members').count(), 3)
    assert.equal(await page.locator('[data-team-open-role]').count(), 2)
    assert.ok(!(await table('Members').innerText()).includes('Synthetic open role'))
    const fillLinks = page.getByRole('link', { name: /^Fill .*Synthetic open role/ })
    assert.equal(await fillLinks.count(), 2)
    assert.ok((await fillLinks.first().getAttribute('href')).endsWith(`allocations?fill=${roles[0].id}`))
    const projectSaved = await save('Synthetic project home groups')
    assert.equal(projectSaved.listType, 'project-team'); assert.equal(projectSaved.projectId, project.id); assert.equal(projectSaved.params.group, 'homeDiscipline')
    await page.locator('#project-team-group').selectOption('unit')
    await table('Members').locator('th[scope="rowgroup"]').filter({ hasText: unitA }).waitFor()
    assert.equal(await table('Members').locator('th[scope="rowgroup"]').count(), 3)
    await selectFilter('project-team', 'unit', unitB); await waitPeople('Members', 1); assert.equal(await desktopPeople('Members').count(), 1)
    // Capacity comes only from the current organisation-week candidate batch and existing scope rules.
    await page.locator('#team-add-person').click()
    const alex = page.getByRole('option').filter({ hasText: 'Alex Team' }), jill = page.getByRole('option').filter({ hasText: 'Jill Team' }), outside = page.getByRole('option').filter({ hasText: 'Unknown Team' })
    await alex.locator('[data-capacity]').waitFor(); assert.match(await alex.innerText(), /12 h free.*8 h away/s)
    assert.match(await jill.innerText(), /Partial view/); assert.ok(!(await jill.innerText()).includes('40 h free'))
    assert.equal(await outside.locator('[data-capacity]').count(), 0); assert.ok(!(await outside.innerText()).includes('0 h free'))
    assert.equal(gridReads.length, 1); assert.equal(gridReads[0].from, '2026-10-26'); assert.equal(gridReads[0].weeks, '1')
    assert.deepEqual(gridReads[0].requested.slice().sort(), cohort.map(row => row.userId).sort()); assert.equal(directoryReads.length, 1)
    assert.ok((await jill.innerText()).includes('Co-op')); assert.ok((await jill.innerText()).includes('Ends 2026-12-18'))
    await jill.click()
    const memberWrite = page.waitForRequest(request => request.url().endsWith(`/projects/${project.id}/members`) && request.method() === 'POST')
    await page.locator('#team-add-person').locator('xpath=ancestor::div[contains(@class,"space-y-3")][1]').getByRole('button', { name: 'Add', exact: true }).click()
    assert.equal((await memberWrite).postDataJSON().userId, cohort[1].userId)
    // Root-owned shared CapacityFigure integration must keep provisional supply honest without blocking selection.
    provisional = true; await goto(projectUrl); await page.locator('#team-add-person').click()
    await page.getByRole('option').filter({ hasText: 'Alex Team' }).locator('[data-capacity]').waitFor()
    assert.ok(!(await page.getByRole('option').filter({ hasText: 'Alex Team' }).innerText()).includes('12 h free'))
    await page.getByRole('option').filter({ hasText: 'Alex Team' }).click(); provisional = false
    // Refused Fill remains a disabled canonical action; ordinary users ask for no planner data.
    me.capabilities.readOnly = true; me.capabilities.workload = false; project.permissions.proposeAllocation = { ok: false, reason: 'Synthetic read only' }
    await goto(projectUrl); await page.locator('[data-team-open-role]').first().waitFor()
    assert.equal(await page.getByRole('link', { name: /^Fill .*Synthetic open role/ }).count(), 0)
    assert.equal(await page.getByRole('button', { name: /^Fill .*Synthetic open role/ }).count(), 2)
    assert.equal(await page.getByRole('button', { name: /^Fill .*Synthetic open role/ }).first().isDisabled(), true)
    const oldGridReads = gridReads.length; await page.locator('#team-add-person').click(); await page.getByRole('option').filter({ hasText: 'Unknown Team' }).waitFor()
    assert.equal(gridReads.length, oldGridReads); assert.equal(await page.getByRole('option').locator('[data-capacity]').count(), 0); await page.keyboard.press('Escape')
    // org_structure off ignores stale unit params, while the other grouping/filter choices remain.
    me.flags.org_structure = false; me.capabilities.readOnly = false
    await goto(`${projectUrl}?group=unit&unitId=${unitB}`); await table('Members').waitFor()
    await waitPeople('Members', 4); assert.equal(await desktopPeople('Members').count(), 4); assert.equal(await table('Members').locator('th[scope="rowgroup"]').count(), 0)
    assert.equal(await table('Members').getByRole('columnheader', { name: 'Current unit', exact: true }).count(), 0)
    assert.equal(await page.locator('#project-team-group option[value="unit"]').count(), 0)
    // Phone cards use the same ID-safe groups and keyboard-operable controls.
    me.flags.org_structure = true; await page.setViewportSize({ width: 375, height: 812 })
    await goto(`${projectUrl}?group=supervisor&officeId=${halifax}`)
    await page.locator('[data-team-group]').first().waitFor(); assert.equal(await page.locator('[data-team-group]').count(), 3)
    assert.equal(await page.locator('#project-team-group').inputValue(), 'supervisor')
    await page.locator('#project-team-group').focus(); await page.keyboard.press('n')
    await page.waitForFunction(() => document.querySelector('#project-team-group')?.value === 'none')
    assert.equal(await page.locator('#project-team-group').inputValue(), 'none')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), '375px document containment')
    // people_screens off ignores new view params and cached-shaped organisation facts; S29 notices remain independent.
    me.flags.people_screens = false; me.capabilities.workload = true; await page.setViewportSize({ width: 1440, height: 1000 })
    await goto(`${projectUrl}?group=homeDiscipline&officeId=${otherOffice}&cols=homeDiscipline`); await table('Members').waitFor()
    await waitPeople('Members', 4); assert.equal(await desktopPeople('Members').count(), 4); assert.equal(await page.locator('[data-team-open-role]').count(), 0)
    assert.equal(await page.locator('#project-team-group').count(), 0); assert.equal(await table('Members').getByRole('columnheader', { name: 'Home discipline', exact: true }).count(), 0)
    assert.ok((await table('Members').innerText()).includes('Co-op'))
    const offReads = gridReads.length; await page.locator('#team-add-person').click(); await page.getByRole('option').filter({ hasText: 'Alex Team' }).waitFor()
    assert.equal(gridReads.length, offReads); assert.equal(await page.getByRole('option').locator('[data-capacity]').count(), 0)
    assert.deepEqual(errors, []); assert.ok(writes.every(write => write.path === 'views' || write.path === `projects/${project.id}/members`), 'Grouping/filtering never adds writes')
    console.log(JSON.stringify({ status: 'PASS', evidence: 'rendered synthetic only', gridReads: gridReads.length, writes: writes.map(write => write.path), nativeDeviceAcceptance: 'unproven' }))
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
