// S43 rendered synthetic contract. PersonPanelTests owns actual server authority/audit evidence.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail, panelIds } = require('./platform-fixtures.cjs')
const { teamMember, teamDiscipline, projectTeam } = require('./team.fixture.cjs')
const { planningGrid, planningPerson, planningCell, planningEntry, entryPage } = require('./planning.fixture.cjs')
const { myStaffPerson, myStaffList } = require('./my-work.fixture.cjs')
const { referenceData, referenceOffice, referenceDiscipline } = require('./me.fixture.cjs')
const { allocationRow, allocationPage, allocationDetail } = require('./allocations.fixture.cjs')
const { dashboardView, dashboardDiscipline } = require('./dashboard.fixture.cjs')
const { activityPage } = require('./activity.fixture.cjs')
const { projectRow, projectList } = require('./projects.fixture.cjs')
const { riskRow } = require('./registers.fixture.cjs')
const { coordinationOptions, reviewRow, reviewPage, reviewPackageRow, reviewRoundRow, reviewAssignmentRow, reviewDetail } = require('./reviews.fixture.cjs')
const { decisionDetail, externalParty } = require('./decisions.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the coordinator local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const alex = me.id, jill = id(5102), memberId = id(5110), officeId = id(5120)
const org = { homeDiscipline: { id: id(5121), name: 'Civil' }, office: { id: officeId, name: 'Halifax' }, supervisor: { id: id(5122), name: 'Sam Scope' }, unit: { id: id(5123), name: 'Current unit' } }
const facts = { notices: { personType: 'Co-op', startDate: '2026-09-01', endDate: '2026-12-18', starts: false, ends: true }, certifications: [
  { id: id(5124), tagId: id(5125), tagName: 'Recorded certification', tagActive: true, expiresOn: null, expiryState: 'Unknown', daysToExpiry: null, rowVersion: 1 },
] }
let note = 'checker', version = 7, forceConflict = false, denied = false, capacityMode = 'normal', manyProjects = false
let deniedResponseGate = null
const entry = planningEntry({ id: id(5601), personId: alex, personName: 'Alex Panel', ownerId: jill, ownerName: 'Jill Panel', label: 'Synthetic retained entry action', startWeek: '2026-10-26', endWeek: '2026-10-26', canEdit: false, canChangeVisibility: false })
const allocation = allocationRow({ id: id(5602), personId: alex, personName: 'Alex Panel', fromDate: '2026-10-26', throughDate: '2026-10-30' })
const risk = riskRow({ id: panelIds.risk, projectId: project.id, key: 'R-001', title: 'Synthetic shared Person register', ownerId: alex, ownerName: 'Alex Panel' })
const reviewIds = { package: id(5700), round: id(5701), assignment: id(5702) }
const review = reviewPackageRow({ id: reviewIds.package, projectId: project.id, key: 'RV-PERSON', title: 'Synthetic reviewer focus return', coordinatorId: alex, currentRoundId: reviewIds.round })
const recordContext = (overrides = {}) => ({ owner: { id: jill, name: 'Jill Panel', state: 'Active', kind: 'User' }, nextActors: [{ id: alex, name: 'Alex Panel', state: 'Active', kind: 'User' }],
  neededBy: null, nextAction: 'start', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [], available: true, ...overrides })
const membership = (n = 0) => ({ memberId: n ? id(5200 + n) : memberId, projectId: n ? id(5300 + n) : project.id,
  projectNumber: n ? `P-SYN-${n}` : project.projectNumber, projectName: n ? `Synthetic visible project ${n}` : project.name, projectStatus: 'Active', roles: ['TeamMember'],
  primaryDiscipline: { id: project.disciplines[0].id, name: 'Civil' }, leadOf: [], responsibilityNote: note })
const members = () => [teamMember({ id: memberId, userId: alex, displayName: 'Alex Panel', jobTitle: 'Civil designer', primaryDisciplineId: project.disciplines[0].id, rowVersion: version,
  organisationFacts: org, personFacts: facts, responsibility: { note, canEdit: !me.capabilities.readOnly, editReason: me.capabilities.readOnly ? 'Synthetic read only' : null } }),
  teamMember({ id: id(5111), userId: jill, displayName: 'Jill Panel', organisationFacts: org, responsibility: { note: null, canEdit: false, editReason: 'PM or primary lead only' } })]
const workspace = () => ({ projects: 1, people: members().map(member => ({ userId: member.userId, displayName: member.displayName, jobTitle: member.jobTitle, email: member.email,
  organisationFacts: org, personFacts: facts, projects: [{ projectId: project.id, projectNumber: project.projectNumber, projectName: project.name, roles: member.roles, leads: [], responsibilityNote: member.responsibility.note }] })) })

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], reads = [], gridReads = [], writes = [], directories = [], staffReads = [], projectListReads = []
  try {
    page.setDefaultTimeout(12000); page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...me.flags, people_screens: true, capacity_screens: true, org_structure: true, capacity_polish: false, visual_system: false, live_events: false, onboarding: false, home_overview: false, work_screens: true, data_entry_safety: true }
    me.capabilities = { ...me.capabilities, workload: true, readOnly: false }
    me.settings.today = '2026-10-31'; me.settings.dateFormat = 'yyyy-MM-dd'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        const body = request.postDataJSON(); writes.push({ path, method: request.method(), body, ifMatch: request.headers()['if-match'] })
        if (path === `projects/${project.id}/members/${memberId}/responsibility`) {
          if (forceConflict) { version = 8; note = 'Someone else saved'; forceConflict = false }
          if (body.rowVersion !== version) return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic membership changed', currentRowVersion: version } })
          note = body.responsibilityNote; version++
          return route.fulfill({ json: { id: memberId, rowVersion: version, responsibilityNote: note } })
        }
        return route.fulfill({ status: 400, json: { detail: 'Unexpected write' } })
      }
      if (/^people\/[^/]+\/panel$/.test(path)) {
        reads.push({ path, params: Object.fromEntries(url.searchParams) })
        if (denied) {
          if (deniedResponseGate) await deniedResponseGate
          return route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ type: 'https://hub.local/errors/not_found', title: 'Not found', status: 404,
            code: 'not_found', detail: 'Synthetic person is no longer visible', traceId: 'synthetic-person-denial' }) })
        }
        const userId = path.split('/')[1], page = Number(url.searchParams.get('projectsPage') ?? 1), pageSize = Number(url.searchParams.get('projectsPageSize') ?? 25)
        const all = Array.from({ length: manyProjects ? 26 : 1 }, (_, n) => membership(n))
        return route.fulfill({ json: { id: userId, displayName: userId === alex ? 'Alex Panel' : 'Jill Panel', jobTitle: 'Civil designer', isActive: true, organisationFacts: org,
          ...(me.flags.capacity_screens ? { personFacts: facts } : {}), today: '2026-10-31', week: '2026-10-26', asOf: '2026-11-01T00:30:00Z', projects: { items: all.slice((page - 1) * pageSize, page * pageSize), page, pageSize, total: all.length } } })
      }
      if (path === `projects/${project.id}/team`) return route.fulfill({ json: projectTeam({ members: members(), disciplines: [teamDiscipline({ id: project.disciplines[0].id, name: 'Civil' })] }) })
      if (path === 'team') return route.fulfill({ json: workspace() })
      if (path === 'projects') { projectListReads.push({ url: url.href, at: Date.now() }); return route.fulfill({ json: projectList([projectRow({ id: project.id, projectNumber: project.projectNumber, name: project.name, pm: { id: alex, displayName: 'Alex Panel' } })]) }) }
      if (path === 'reference') return route.fulfill({ json: referenceData({ offices: [referenceOffice(officeId, 'Halifax')], disciplines: [referenceDiscipline(org.homeDiscipline.id, 'Civil')] }) })
      if (path === 'organisation') return route.fulfill({ json: { includeIndirectReports: true, units: [{ id: org.unit.id, name: org.unit.name, leaderId: me.id, closedOn: null }] } })
      if (path === 'staff') {
        staffReads.push(Object.fromEntries(url.searchParams))
        const rows = [...Array.from({ length: 18 }, (_, n) => myStaffPerson({ id: id(5400 + n), displayName: `Synthetic Staff ${String(n).padStart(2, '0')}`, officeId, overdue: 1, blocked: 1, open: 2, reviewsStalled: 1 })),
          myStaffPerson({ id: alex, displayName: 'Alex Panel', jobTitle: 'Civil designer', officeId, overdue: 1, blocked: 1, open: 2, reviewsStalled: 1 })]
        return route.fulfill({ json: myStaffList(rows, { scope: url.searchParams.get('scope') ?? 'direct', canSeeAll: true, asOf: '2026-11-01T00:30:00Z' }) })
      }
      if (path === `projects/${project.id}/open-roles`) return route.fulfill({ json: [] })
      if (path === 'certification-tags') return route.fulfill({ json: [] })
      if (path === 'users') { directories.push(url.href); return route.fulfill({ json: members().map(member => ({ id: member.userId, displayName: member.displayName, isActive: true })) }) }
      if (path === 'workload') return route.fulfill({ json: { weeks: ['2026-10-26'], today: '2026-10-31', currentWeek: '2026-10-26', defaultCapacity: 40, thresholds: { overPct: 110, amberPct: 90, underPct: 40 }, unfilledRoles: [],
        people: [{ id: alex, displayName: 'Alex Panel', supervisorId: null, supervisorName: null, officeId, jobTitle: 'Civil designer', homeDisciplineId: org.homeDiscipline.id,
          capacity: 40, capacityOverride: false, noDueDate: 0, openTasks: 0, unestimated: 0, overdue: 0, projects: 1, overAssigned: false, underAssigned: false, cluster: false, canSetCapacity: false, partialScope: false, overWeek: null, indicator: 'ok',
          cells: [{ week: '2026-10-26', hours: 8, pct: 20, available: 40, confirmed: 8, proposed: 0, committed: 8, band: 'light', uncommitted: 32 }] }] } })
      if (path === `workload/${alex}/tasks`) return route.fulfill({ json: { weeks: ['2026-10-26'], projects: [] } })
      if (path === 'planning/entries') return route.fulfill({ json: entryPage([entry]) })
      if (path === `planning/entries/${entry.id}`) return route.fulfill({ json: entry })
      if (path === `projects/${project.id}/allocations`) return route.fulfill({ json: allocationPage([allocation]) })
      if (path === `projects/${project.id}/allocations/${allocation.id}`) return route.fulfill({ json: allocationDetail(allocation) })
      if (path === `projects/${project.id}/changes/options`) return route.fulfill({ json: coordinationOptions({ actorId: alex, people: [{ id: alex, displayName: 'Alex Panel' }, { id: jill, displayName: 'Jill Panel' }] }) })
      if (path === `projects/${project.id}/dashboard`) return route.fulfill({ json: dashboardView({
        disciplines: [dashboardDiscipline({ disciplineId: project.disciplines[0].id, name: 'Civil', leadId: alex })],
        activity: [...activityPage(id, { actorId: jill, itemId: task.id, itemKey: task.key, itemName: task.name }).items.slice(0, 1).map(row => ({ ...row, actorName: 'Jill Panel' })),
          { ...activityPage(id, { actorId: jill, itemId: task.id, itemKey: task.key, itemName: task.name }).items[1], actorId: null, actorName: 'Historical actor without an ID' }] }) })
      if (path === `projects/${project.id}/reviews`) return route.fulfill({ json: reviewPage([reviewRow({ id: review.id, key: review.key, title: review.title, coordinatorId: alex })]) })
      if (path === `projects/${project.id}/reviews/${review.id}`) return route.fulfill({ json: reviewDetail({ package: review, actorId: alex,
        rounds: [reviewRoundRow({ id: reviewIds.round, packageId: review.id, number: 1 })],
        assignments: [reviewAssignmentRow({ id: reviewIds.assignment, roundId: reviewIds.round, projectDisciplineId: project.disciplines[0].id, reviewerId: jill })],
        presentation: [{ roundId: reviewIds.round, guidance: { allowSelfReview: false, sourcesAvailable: true, authorIds: [], excludedReviewerIds: [], sources: [] },
          missingReviewerAssignmentIds: [], reviewers: [{ assignmentId: reviewIds.assignment, reviewer: { id: jill, name: 'Jill Panel', state: 'Active', kind: 'User' } }], startAllowed: false, startReason: null }] }) })
      if (path === `projects/${project.id}/risks`) return route.fulfill({ json: [risk] })
      if (path === `tasks/${task.id}`) return route.fulfill({ json: { ...taskDetail, context: recordContext() } })
      if (path === `decisions/${decisionDetail.decision.id}`) return route.fulfill({ json: { ...decisionDetail, permissions: { ...decisionDetail.permissions, edit: { ok: false } },
        context: recordContext({ owner: { id: externalParty.id, name: externalParty.name, kind: 'ExternalParty', state: 'Active' }, nextActors: [{ id: null, name: null, kind: 'User', state: 'Unknown' }], actorState: 'Unknown' }) } })
      if (path === 'planning/grid') {
        gridReads.push(Object.fromEntries(url.searchParams)); const target = (url.searchParams.get('personIds') ?? '').split(',')
        if (!url.searchParams.has('personIds')) return route.fulfill({ json: planningGrid({ weeks: ['2026-10-26'], currentWeek: '2026-10-26', people: [planningPerson({ id: alex, displayName: 'Alex Panel', cells: [planningCell({ week: '2026-10-26', remaining: 12 })], entries: [entry] })] }) })
        return route.fulfill({ json: planningGrid({ weeks: ['2026-10-26'], currentWeek: '2026-10-26', people: capacityMode === 'outside' ? [] : target.map(personId => planningPerson({ id: personId,
          displayName: 'Scoped person', partial: capacityMode === 'partial', cells: [planningCell({ week: '2026-10-26', remaining: 12, timeAway: 8, provisional: capacityMode === 'provisional' })] })) }) })
      }
      return route.fallback()
    })
    const goto = path => page.goto(`${base}${path}`)
    const panel = () => page.locator('[data-person-panel]')
    const sheet = () => page.getByRole('dialog').filter({ has: panel() })
    const close = () => sheet().getByRole('button', { name: 'Close', exact: true }).click()
    const trigger = person => page.locator(`[data-person-panel-trigger="${person}"]:visible`).first()
    const projectUrl = `/projects/${project.projectNumber}/team`
    const listUrl = `/team?projects=${project.id}&q=Alex&group=office&officeId=${officeId}`
    // Passive exact-ID name, keyboard activation, visible facts, canonical current week and focus/exact URL return.
    await goto(listUrl); await trigger(alex).waitFor(); const openerId = await trigger(alex).getAttribute('id')
    await trigger(alex).focus(); await page.keyboard.press('Enter'); await panel().waitFor()
    assert.equal(new URL(page.url()).searchParams.get('panel'), `Person:${alex}`)
    assert.ok((await panel().innerText()).includes('Current unit')); assert.ok((await panel().innerText()).includes('Recorded certification'))
    assert.ok((await panel().innerText()).includes('checker')); await panel().locator('[data-capacity]').waitFor()
    assert.ok((await panel().locator('[data-capacity]').innerText()).includes('12'))
    assert.ok(gridReads.every(read => read.from === '2026-10-26' && read.weeks === '1' && read.personIds === alex)); assert.equal(directories.length, 0)
    await close(); await page.waitForURL(`${base}${listUrl}`)
    await page.waitForFunction(id => document.activeElement?.id === id, openerId)
    // Nested origin chain: person -> project Team -> another person -> exact project Team while keeping its parent return.
    await trigger(alex).click(); await panel().waitFor()
    await panel().getByRole('link', { name: `${project.projectNumber} · ${project.name}`, exact: true }).click(); await page.waitForURL(`${base}${projectUrl}`)
    await trigger(jill).click(); await panel().waitFor(); assert.equal(new URL(page.url()).searchParams.get('panel'), `Person:${jill}`)
    await close(); await page.waitForURL(`${base}${projectUrl}`)
    const chain = await page.evaluate(() => history.state?.usr?.recordContextReturn)
    assert.ok(chain?.url.includes(`panel=Person%3A${alex}`)); assert.equal(chain?.parent?.url, listUrl)
    // Pager changes only its namespaced values, preserving the underlying list and return state.
    manyProjects = true; await goto(`${listUrl}&panel=Person:${alex}`); await panel().waitFor()
    await panel().getByRole('button', { name: 'Next', exact: true }).click(); await page.waitForURL(url => url.searchParams.get('personProjectsPage') === '2')
    await panel().getByText('P-SYN-25 · Synthetic visible project 25', { exact: true }).waitFor()
    assert.equal(new URL(page.url()).searchParams.get('q'), 'Alex'); assert.equal(new URL(page.url()).searchParams.get('officeId'), officeId)
    manyProjects = false
    // Direct-link Close clears exactly Person context, retains unrelated task/list params, never navigates to a directory.
    const direct = `${listUrl}&taskSection=activity&panel=Person:${alex}&personStaffScope=direct&personProjectsPage=1&personProjectsPageSize=25`
    await goto(direct); await panel().waitFor(); assert.equal(reads.at(-1).params.staffScope, 'direct'); await close()
    await page.waitForURL(url => !url.searchParams.has('panel'))
    const after = new URL(page.url()); for (const key of ['personStaffScope', 'personProjectsPage', 'personProjectsPageSize']) assert.equal(after.searchParams.has(key), false)
    assert.equal(after.searchParams.get('taskSection'), 'activity'); assert.equal(after.searchParams.get('officeId'), officeId)
    // main.tsx keeps successful queries fresh15s. Let actual time expire, then normally reopen the SAME key.
    // Hold only the network fixture response to observe cached facts before an answered404; never inject query/focus/clock state.
    await goto(`${projectUrl}?panel=Person:${alex}`); await panel().waitFor(); const successfulPersonObservedAt = Date.now()
    await close(); await page.waitForURL(url => !url.searchParams.has('panel')); denied = true
    let releaseDeniedResponse; deniedResponseGate = new Promise(resolve => { releaseDeniedResponse = resolve })
    await page.waitForFunction(notBefore => Date.now() >= notBefore, successfulPersonObservedAt + 15_001, { timeout: 20_000 })
    const denialPath = `/api/v1/people/${alex}/panel`, readsBeforeDenial = reads.length
    const deniedRequest = page.waitForRequest(request => request.method() === 'GET' && new URL(request.url()).pathname === denialPath)
    const answered404 = page.waitForResponse(response => response.request().method() === 'GET' && new URL(response.url()).pathname === denialPath && response.status() === 404)
    try {
      await trigger(alex).click(); const request = await deniedRequest
      assert.equal(new URL(request.url()).searchParams.get('projectsPage'), '1'); assert.equal(new URL(request.url()).searchParams.get('projectsPageSize'), '25')
      assert.equal(new URL(request.url()).searchParams.has('staffScope'), false)
      await panel().waitFor(); assert.ok((await panel().innerText()).includes('Alex Panel'), 'The actual previous query data is present until the held answer arrives')
      assert.ok(reads.length > readsBeforeDenial, 'A normal reopen actually starts a fresh API request after the cache lifetime')
    } finally { releaseDeniedResponse(); deniedResponseGate = null }
    const deniedResponse = await answered404, deniedProblem = await deniedResponse.json()
    assert.equal(deniedProblem.status, 404); assert.equal(deniedProblem.code, 'not_found'); assert.equal(deniedProblem.detail, 'Synthetic person is no longer visible')
    // ApiError retains detail, but ErrorBanner intentionally renders app.notFound for404.
    const refusedPersonDialog = page.getByRole('dialog', { name: 'Person', exact: true })
    await refusedPersonDialog.getByRole('alert').filter({ hasText: 'This page or item does not exist, or you cannot view it.' }).waitFor()
    assert.equal(await panel().count(), 0); assert.equal(await refusedPersonDialog.locator('[data-capacity]').count(), 0)
    console.log(JSON.stringify({ step: 'answered-cached-person404', requestObserved: true, httpStatus: deniedResponse.status(), problemCode: deniedProblem.code, cachedFactsRemoved: true }))
    denied = false
    // Dirty member-version remains7 after external version8 and after a second explicit retry; only Keep mine rebases.
    await goto(projectUrl); await page.locator(`[data-member-responsibility="${memberId}"]:visible`).getByRole('button', { name: 'Edit responsibility for Alex Panel', exact: true }).click()
    let editor = page.getByRole('dialog', { name: 'Edit responsibility for Alex Panel', exact: true })
    await editor.getByLabel('Responsibility', { exact: true }).fill('My dirty note'); forceConflict = true
    await editor.getByRole('button', { name: 'Save', exact: true }).click(); await editor.getByText('Someone else saved', { exact: false }).waitFor()
    assert.equal(await editor.getByLabel('Responsibility', { exact: true }).inputValue(), 'My dirty note'); assert.equal(writes.at(-1).body.rowVersion, 7)
    await Promise.all([page.waitForResponse(response => response.request().method() === 'PATCH' && response.url().endsWith('/responsibility') && response.status() === 409), editor.getByRole('button', { name: 'Save', exact: true }).click()]); await editor.getByText('Synthetic membership changed', { exact: false }).waitFor()
    assert.equal(writes.at(-1).body.rowVersion, 7); assert.equal(writes.at(-1).ifMatch, '"7"')
    await editor.getByRole('button', { name: 'Keep my note using this version', exact: true }).click(); await editor.getByRole('button', { name: 'Save', exact: true }).click()
    await editor.waitFor({ state: 'detached' }); assert.equal(writes.at(-1).body.rowVersion, 8); assert.equal(note, 'My dirty note'); assert.equal(version, 9)
    await trigger(alex).click(); await panel().getByText('My dirty note', { exact: false }).waitFor(); await close()
    // A picker still selects a person; it is not a nested panel trigger and makes no note/role command.
    await page.locator('#team-add-person').click(); await page.getByRole('option').filter({ hasText: 'Jill Panel' }).click(); assert.equal(await panel().count(), 0)
    // ReadOnly has no weekly request or edit button, while permitted facts still render on375px.
    me.capabilities.workload = false; me.capabilities.readOnly = true; const gridsBefore = gridReads.length
    await page.setViewportSize({ width: 375, height: 812 }); await goto(projectUrl); await trigger(alex).click(); await panel().waitFor()
    assert.equal(gridReads.length, gridsBefore); assert.equal(await panel().locator('[data-capacity]').count(), 0)
    assert.equal(await panel().getByRole('button', { name: /^Edit/ }).count(), 0)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    assert.ok(await panel().evaluate(element => element.scrollWidth <= element.clientWidth + 1))
    await close(); assert.equal(await page.getByRole('button', { name: 'Edit responsibility for Alex Panel', exact: true }).count(), 0)
    // Out-of-planner-scope is an absent figure, never invented zero. Partial and provisional hide unsafe free figures.
    me.capabilities.workload = true; me.capabilities.readOnly = false
    for (const mode of ['outside', 'partial', 'provisional']) {
      capacityMode = mode; const gridAnswered = page.waitForResponse(response => response.url().includes('/planning/grid')); await goto(`${projectUrl}?panel=Person:${alex}`); await panel().waitFor(); await gridAnswered
      if (mode === 'outside') { assert.equal(await panel().locator('[data-capacity]').count(), 0) }
      else { await panel().locator('[data-capacity]').waitFor(); const text = await panel().locator('[data-capacity]').innerText(); assert.ok(!text.includes('12')); assert.ok(!text.includes('8')) }
    }
    // Independent facts gate and completely dark panel/trigger additions.
    me.flags.capacity_screens = false; await goto(`${projectUrl}?panel=Person:${alex}`); await panel().waitFor(); assert.equal(await panel().getByText('Recorded certification', { exact: true }).count(), 0)
    me.flags.people_screens = false; const beforeDark = reads.length; await page.setViewportSize({ width: 1440, height: 1000 }); await goto(`${projectUrl}?panel=Person:${alex}&personStaffScope=all`)
    await page.getByRole('table', { name: 'Members', exact: true }).waitFor(); assert.equal(await panel().count(), 0); assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0); assert.equal(reads.length, beforeDark)
    // A-owned Staff implementation: desktop Info remains separate from My Work, phone passive name opens this panel.
    // Both retain the exact authoritative scope/filter URL, actual nonzero list scroll and keyboard focus on return.
    me.flags.people_screens = true; me.flags.visual_system = true; me.flags.org_structure = true
    me.capabilities.staff = true; me.capabilities.allStaff = true; me.capabilities.workload = false
    const staffGrids = gridReads.length, staffDirectoryReads = directories.length
    for (const [width, staffScope] of [[1440, 'all'], [375, `unit:${org.unit.id}`]]) {
      await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 })
      const staffUrl = `/staff?scope=${encodeURIComponent(staffScope)}&office=${officeId}&discipline=${org.homeDiscipline.id}&inactive=1&indicator=overdue&cardIndicators=overdue,blocked`
      await goto(staffUrl); await trigger(alex).waitFor()
      assert.equal(await page.locator('#staff-office').inputValue(), officeId); assert.equal(await page.locator('#staff-discipline').inputValue(), org.homeDiscipline.id)
      assert.equal(await page.getByRole('checkbox', { name: 'Show inactive', exact: true }).isChecked(), true)
      assert.deepEqual(staffReads.at(-1), { scope: staffScope, showInactive: 'true', officeId, disciplineId: org.homeDiscipline.id, indicator: 'overdue' })
      const personTrigger = trigger(alex)
      assert.deepEqual(await personTrigger.evaluate(element => ({ anchorAncestor: !!element.closest('a'), buttonAncestor: !!element.parentElement?.closest('button'), nested: element.querySelectorAll('a,button,input,select,textarea,[role="button"]').length })), { anchorAncestor: false, buttonAncestor: false, nested: 0 })
      if (width === 1440) {
        const workLink = page.getByRole('link', { name: 'Alex Panel', exact: true }); assert.equal(await workLink.getAttribute('href'), `/my-work?userId=${alex}`)
        assert.equal(await personTrigger.locator('svg').count(), 1, 'Desktop details uses separate Info icon')
      } else {
        assert.equal(await personTrigger.innerText(), 'Alex Panel', 'Phone passive name itself opens person details')
        assert.equal(await personTrigger.locator('svg').count(), 0)
      }
      await personTrigger.scrollIntoViewIfNeeded(); await personTrigger.focus()
      const origin = await personTrigger.evaluate(element => { const content = document.getElementById('content'); return { id: element.id, top: content.scrollTop, left: content.scrollLeft } })
      assert.ok(origin.top > 0, `${width}px Staff return must exercise real list scroll`)
      const personAnswered = page.waitForResponse(response => response.url().includes(`/people/${alex}/panel`))
      await page.keyboard.press('Enter'); await personAnswered; await panel().waitFor()
      assert.equal(new URL(page.url()).searchParams.get('personStaffScope'), staffScope)
      assert.deepEqual(reads.at(-1).params, { staffScope, projectsPage: '1', projectsPageSize: '25' })
      assert.equal(new URL(page.url()).searchParams.get('office'), officeId); assert.equal(new URL(page.url()).searchParams.get('cardIndicators'), 'overdue,blocked')
      await close(); await page.waitForURL(`${base}${staffUrl}`)
      await page.waitForFunction(({ id, top, left }) => { const content = document.getElementById('content'); return document.activeElement?.id === id && Math.abs(content.scrollTop - top) <= 1 && Math.abs(content.scrollLeft - left) <= 1 }, origin)
      assert.equal(await page.locator('#staff-office').inputValue(), officeId); assert.equal(await page.locator('#staff-discipline').inputValue(), org.homeDiscipline.id)
      assert.equal(await page.getByRole('checkbox', { name: 'Show inactive', exact: true }).isChecked(), true)
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${width}px Staff containment`)
      if (width === 1440) {
        await page.getByRole('link', { name: 'Alex Panel', exact: true }).click(); await page.waitForURL(`${base}/my-work?userId=${alex}`)
      }
    }
    assert.equal(gridReads.length, staffGrids, 'Staff and its person panel make no planner read without Workload')
    assert.equal(directories.length, staffDirectoryReads, 'Staff details never fill from a broader people directory')
    // Off on both actual Staff renderers: preserved original My Work name link/phone text and no Person GET, even a stale deep link.
    me.flags.people_screens = false; const staffDarkReads = reads.length
    for (const width of [1440, 375]) {
      await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 })
      await goto(`/staff?scope=all&office=${officeId}&panel=Person:${alex}&personStaffScope=all`)
      const row = page.locator(`[data-staff-person="${alex}"]:visible`); await row.waitFor()
      assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0); assert.equal(await panel().count(), 0); assert.equal(reads.length, staffDarkReads)
      if (width === 1440) assert.equal(await row.getByRole('link', { name: 'Alex Panel', exact: true }).getAttribute('href'), `/my-work?userId=${alex}`)
      else { assert.ok((await row.innerText()).includes('Alex Panel')); assert.equal(await row.getByRole('button', { name: /View details/ }).count(), 0) }
    }
    // Extended passive names use actual current Workload/Planner/Allocation/Dashboard fields and retain their adjacent actions.
    me.flags.people_screens = true; me.flags.visual_system = false; me.flags.org_structure = false; me.flags.capacity_screens = false; me.flags.screen_patterns = true
    me.capabilities.workload = true; me.capabilities.readOnly = false; capacityMode = 'normal'
    project.disciplines[0].leadUserId = alex; project.disciplines[0].leadName = 'Alex Panel'
    await page.setViewportSize({ width: 1440, height: 1000 })
    async function openPassive(person, expectedUrl, within = page) {
      const source = within.locator(`[data-person-panel-trigger="${person}"]:visible`).first(); await source.waitFor()
      assert.equal(await source.evaluate(element => !!element.closest('a') || !!element.parentElement?.closest('button') || !!element.querySelector('button,a,input,select')), false)
      const sourceUrl = page.url(), projectReadsBefore = projectListReads.length, focus = await source.getAttribute('id')
      await source.focus(); await page.keyboard.press('Enter'); await panel().waitFor()
      assert.equal(new URL(page.url()).searchParams.get('panel'), `Person:${person}`); assert.equal(reads.at(-1).path, `people/${person}/panel`)
      await close(); await page.waitForURL(`${base}${expectedUrl}`)
      try { await page.waitForFunction(id => document.activeElement?.id === id, focus) }
      catch (error) {
        // Read-only diagnostics: no focus, history, visibility, query or other application state is changed here.
        const state = await page.evaluate(({ expectedId, person }) => {
          const shown = element => !!element && element.getClientRects().length > 0 && !element.closest('[hidden],[aria-hidden="true"],[data-state="closed"]') && getComputedStyle(element).visibility !== 'hidden'
          const describe = element => element ? { tag: element.tagName, id: element.id, label: element.getAttribute('aria-label'), text: element.textContent?.trim().slice(0, 120), shown: shown(element) } : null
          const original = document.getElementById(expectedId), active = document.activeElement
          return { originalExists: !!original, original: describe(original), active: describe(active),
            currentPersonTriggers: [...document.querySelectorAll('[data-person-panel-trigger]')].filter(element => element.getAttribute('data-person-panel-trigger') === person).slice(0, 12).map(describe),
            foregroundDialogs: [...document.querySelectorAll('[role="dialog"]')].filter(shown).map(element => ({ ...describe(element), state: element.getAttribute('data-state'), modal: element.getAttribute('aria-modal') })) }
        }, { expectedId: focus, person })
        console.error(JSON.stringify({ step: 'person-passive-focus-return-failure', sourceUrl, originalTriggerId: focus, returnedUrl: page.url(), person,
          projectListReads: projectListReads.slice(projectReadsBefore), ...state }))
        throw error
      }
    }
    const workloadUrl = `/workload?from=2026-10-26&officeId=${officeId}&groupBy=none`
    await goto(workloadUrl); await openPassive(alex, workloadUrl)
    const tasksToggle = page.getByRole('button', { name: "Show Alex Panel's tasks", exact: true }); await tasksToggle.click(); assert.equal(await tasksToggle.getAttribute('aria-expanded'), 'true'); assert.equal(await panel().count(), 0)
    const plannerUrl = '/planner?from=2026-10-26&groupBy=none&sort=name'
    await goto(plannerUrl); await openPassive(alex, plannerUrl)
    await page.getByRole('button', { name: 'Expand planning row', exact: true }).click(); await page.getByRole('button', { name: 'Collapse planning row', exact: true }).waitFor(); assert.equal(await panel().count(), 0)
    // The existing monthly fixture uses the same known person; name opens details while the cell still chooses a week.
    me.flags.capacity_screens = true
    const monthUrl = '/planner?from=2026-10-26&zoom=months&groupBy=none'
    await goto(monthUrl); await openPassive(alex, monthUrl)
    await page.getByRole('button').filter({ has: page.locator('[data-planner-month-value]') }).first().click()
    await page.getByText('Choose a week to inspect contributions', { exact: true }).waitFor(); assert.equal(await panel().count(), 0); await page.keyboard.press('Escape')
    me.flags.capacity_screens = false
    const entriesUrl = '/planner?layout=list&from=2026-10-26&source=Other'
    await goto(entriesUrl); await openPassive(alex, entriesUrl); await openPassive(jill, entriesUrl)
    await page.getByRole('button', { name: entry.label, exact: true }).click(); await page.getByRole('dialog', { name: entry.label, exact: true }).waitFor(); assert.equal(await panel().count(), 0)
    const allocationUrl = `/projects/${project.projectNumber}/allocations?status=Proposed`
    await goto(allocationUrl); await page.getByRole('button', { name: 'Alex Panel', exact: true }).click()
    const allocationSheet = page.getByRole('dialog', { name: 'Allocation detail', exact: true }); await allocationSheet.waitFor(); assert.equal(await panel().count(), 0, 'Allocation name still opens the allocation')
    const allocationOrigin = new URL(page.url()); assert.equal(allocationOrigin.searchParams.get('allocation'), allocation.id)
    await openPassive(alex, `${allocationOrigin.pathname}${allocationOrigin.search}`, allocationSheet); await allocationSheet.waitFor()
    const dashUrl = `/projects/${project.projectNumber}/dashboard`
    await goto(dashUrl); await openPassive(alex, dashUrl); await openPassive(jill, dashUrl)
    const legacyActor = page.getByText('Historical actor without an ID', { exact: true }); await legacyActor.waitFor(); assert.equal(await legacyActor.locator('[data-person-panel-trigger]').count(), 0)
    // The actual clickable Projects DataTable row competes with PM details: Person wins; ordinary row/title navigation still works.
    me.flags.project_screens = false
    const projectsUrl = '/projects?layout=list&cols=pm'
    await goto(projectsUrl); await openPassive(alex, projectsUrl)
    assert.equal(new URL(page.url()).pathname, '/projects'); assert.equal(new URL(page.url()).searchParams.has('peek'), false)
    await page.getByRole('table', { name: 'Projects', exact: true }).getByText(project.projectNumber, { exact: true }).click(); await page.waitForURL(`${base}/projects/${project.projectNumber}/dashboard`)
    await goto(projectsUrl); await page.getByRole('table', { name: 'Projects', exact: true }).getByRole('link', { name: project.name, exact: true }).click(); await page.waitForURL(`${base}/projects/${project.projectNumber}/dashboard`)
    await page.setViewportSize({ width: 375, height: 812 }); await goto(projectsUrl); await openPassive(alex, projectsUrl)
    assert.ok((await page.locator('li:visible').filter({ has: page.getByRole('link', { name: project.name, exact: true }) }).first().innerText()).includes('Synthetic client'))
    await page.setViewportSize({ width: 1440, height: 1000 })
    // Shared register Person has an actual internal owner ID; row-title action remains independent.
    const riskUrl = `/projects/${project.projectNumber}/risks?status=Open`
    await goto(riskUrl); await openPassive(alex, riskUrl); await page.getByRole('button', { name: risk.title, exact: true }).click(); await page.waitForURL(url => url.searchParams.get('panel') === `Risk:${risk.id}`)
    // A real nested RecordContext owner/next actor opens Person and restores the Task source including focus.
    const taskOrigin = `/projects/${project.projectNumber}/tasks?status=Not%20Started&panel=Task:${task.id}`
    await goto(taskOrigin); let context = page.getByRole('region', { name: 'Record context', exact: true }); await context.waitFor()
    assert.equal(await context.locator(`[data-person-panel-trigger="${alex}"]`).count(), 1)
    await openPassive(jill, taskOrigin, context)
    context = page.getByRole('region', { name: 'Record context', exact: true }); await context.waitFor(); assert.equal(await context.locator(`[data-person-panel-trigger="${alex}"]`).count(), 1)
    // ReviewDetail is actually unmounted for Person. Its existing reviewer actor must regain the same ID/focus after remount.
    const reviewOrigin = `/projects/${project.projectNumber}/reviews?status=Draft&sort=round:asc&panel=ReviewPackage:${review.id}&round=${reviewIds.round}`
    await goto(reviewOrigin)
    const reviewDialog = () => page.getByRole('dialog', { name: `${review.key} · ${review.title}`, exact: true })
    await reviewDialog().waitFor(); const reviewerTrigger = reviewDialog().locator(`[data-person-panel-trigger="${jill}"]`)
    assert.equal(await reviewerTrigger.count(), 1); const reviewerFocusId = await reviewerTrigger.getAttribute('id')
    assert.ok(reviewerFocusId.startsWith('person-trigger-') && reviewerFocusId.length <= 200)
    const exactReviewOrigin = page.url(); await reviewerTrigger.focus(); await page.keyboard.press('Enter'); await panel().waitFor()
    assert.equal(new URL(page.url()).searchParams.get('panel'), `Person:${jill}`); assert.equal(reads.at(-1).path, `people/${jill}/panel`)
    await reviewDialog().waitFor({ state: 'detached' }); await close(); await page.waitForURL(exactReviewOrigin); await reviewDialog().waitFor()
    assert.equal(await reviewDialog().locator(`[data-person-panel-trigger="${jill}"]`).getAttribute('id'), reviewerFocusId)
    await page.waitForFunction(id => document.activeElement?.id === id, reviewerFocusId)
    assert.equal(new URL(page.url()).searchParams.get('status'), 'Draft'); assert.equal(new URL(page.url()).searchParams.get('round'), reviewIds.round)
    // External person and unknown/no-id actor labels never masquerade as internal-user panel triggers.
    const externalUrl = `/projects/${project.projectNumber}/decisions?panel=Decision:${decisionDetail.decision.id}`
    await goto(externalUrl); const externalContext = page.getByRole('region', { name: 'Record context', exact: true }); await externalContext.waitFor()
    assert.ok((await externalContext.innerText()).includes(externalParty.name)); assert.equal(await externalContext.locator('[data-person-panel-trigger]').count(), 0)
    const extReads = reads.length; await externalContext.getByText(externalParty.name, { exact: false }).click(); assert.equal(reads.length, extReads)
    // Dark behavior for newly loaned surfaces too, without weakening their original edit/open controls.
    me.flags.people_screens = false; const extendedDarkReads = reads.length
    for (const url of [projectsUrl, workloadUrl, plannerUrl, entriesUrl, `${allocationUrl}&allocation=${allocation.id}`, dashUrl, riskUrl, taskOrigin]) {
      await goto(url); await page.getByRole('heading').first().waitFor(); await page.waitForLoadState('networkidle')
      assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0); assert.equal(reads.length, extendedDarkReads)
    }
    assert.ok(writes.every(write => write.path === `projects/${project.id}/members/${memberId}/responsibility`)); assert.deepEqual(errors, [])
    console.log(JSON.stringify({ status: 'PASS', evidence: 'rendered synthetic only', panelReads: reads.length, gridReads: gridReads.length, noteAttempts: writes.length, staffReads: staffReads.length, staffWidths: [1440, 375], nativeDeviceAcceptance: 'UNPROVEN' }))
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
