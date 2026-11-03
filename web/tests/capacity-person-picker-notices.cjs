// FR50 rendered synthetic acceptance. Real cohorts/permissions/calendar are covered by CapacityPersonPickerNoticeTests.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail, panelIds } = require('./platform-fixtures.cjs')
const { coordinationOptions, coordinationTask, coordinationSource, sourceRevisionRow } = require('./reviews.fixture.cjs')
const { weeklyCoordination } = require('./dashboard.fixture.cjs')
const { handoffOptions, handoffPage } = require('./handoffs.fixture.cjs')
const { projectTeam, teamMember } = require('./team.fixture.cjs')
const { adminUser } = require('./admin.fixture.cjs')
const { availabilityRead, hrIdentity } = require('./availability.fixture.cjs')
const { coordinationProjection } = require('./coordination.fixture.cjs')
const { issueRow, issueDetail } = require('./registers.fixture.cjs')
const { readinessWindow, readinessDetail, readinessAssessment } = require('./readiness.fixture.cjs')
const { commitmentList } = require('./weekly-commitments.fixture.cjs')
const { basisPage, basisEntryRow, basisVersionItem, basisDetail, basisUseRow } = require('./design-basis.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const jill = id(2401), owner = id(2402), unknown = id(2403), basisId = id(2410), versionId = id(2411), unitId = id(2412), assessmentId = id(2413)
const notice = { personType: 'Co-op', startDate: '2026-11-01', endDate: '2026-12-18', starts: true, ends: true }
const noNotice = { personType: null, startDate: null, endDate: null, starts: false, ends: false }
const people = [{ id: me.id, displayName: 'Alex Chen', personNotices: noNotice }, { id: owner, displayName: 'Recorded owner', personNotices: noNotice },
  { id: jill, displayName: 'Jill Martin', personNotices: notice }, { id: unknown, displayName: 'Unknown dates', personNotices: noNotice }]
const pickupPeople = [...people.filter(person => [jill, unknown].includes(person.id)), { id: id(2419), displayName: null, personNotices: noNotice }]
const suffix = 'Jill Martin · Co-op · Starts 2026-11-01 · Ends 2026-12-18'
const emptyPage = { items: [], page: 1, pageSize: 50, totalCount: 0 }
const projection = coordinationProjection()
const work = coordinationTask({ id: task.id, key: task.key, name: task.name, projectDisciplineId: project.disciplines[0].id, ownerId: owner, canManage: true })
const options = coordinationOptions({ actorId: me.id, canWrite: true, manageDisciplineIds: [project.disciplines[0].id],
  canCreate: { changes: true, reviews: true }, people, disciplines: [{ id: project.disciplines[0].id, name: 'Civil' }], tasks: [work],
  sources: [coordinationSource({ revision: sourceRevisionRow({ id: id(2420), sourceKey: 'S-SYNTHETIC', title: 'Synthetic source', revision: 'A', deliverableId: id(2421) }) })] })
const entry = basisEntryRow({ id: basisId, projectId: project.id, key: 'B-SYNTHETIC', title: 'Recorded assumption', kind: 'Assumption', ownerId: owner,
  projectDisciplineId: project.disciplines[0].id, currentVersionId: versionId })
const basis = basisDetail({ entry, versions: [basisVersionItem({ id: versionId, entryId: basisId, number: 1, status: 'Proposed' })],
  uses: [basisUseRow({ id: id(2414), versionId, targetId: task.id, ownerId: owner })], dispositions: [{ id: id(2415), versionId, ownerId: owner,
    approvedBy: me.id, scope: 'Site - watermain', expiresOn: '2026-12-31' }], canManage: true })

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], writes = [], directoryRequests = [], visited = []
  let identityVersion = 4, refreshes = 0, unitLeader = null
  try {
    page.setDefaultTimeout(12000); page.on('pageerror', error => errors.push(error.message))
    me.systemRoles = ['Admin']; me.capabilities = { ...me.capabilities, admin: true, workload: true, staff: true }
    me.settings.today = '2026-10-31'; me.settings.dateFormat = 'yyyy-MM-dd'; me.settings.allowSelfReview = false
    me.flags = { ...me.flags, live_events: false, onboarding: false, capacity_screens: true, people_screens: false, visual_system: false,
      screen_patterns: true, org_structure: true, task_pickup: true, allocation_preview: false, capacity_polish: false }
    project.permissions = { ...project.permissions, createBasisIn: [project.disciplines[0].id], assignBasisIn: [project.disciplines[0].id],
      createSubmission: { ok: true }, proposeAllocation: { ok: true } }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', ''), method = request.method()
      if (method !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        if (method === 'POST' && path === `projects/${project.id}/allocations/capacity-preview`) {
          const body = request.postDataJSON()
          return route.fulfill({ json: { personId: body.personId, fromDate: body.fromDate, throughDate: body.throughDate, state: 'Complete',
            includesProposal: false, unestimatedTasks: 0, undatedTasks: 0, weeks: [], asOf: '2026-10-31T12:00:00Z' } })
        }
        const body = request.postData() ? request.postDataJSON() : null; writes.push({ path, method, body, headers: request.headers() })
        if (path === `admin/organisation/units/${unitId}`) unitLeader = body.leaderId
        if (path === `planning/hr-identities/${jill}`) {
          assert.equal(body.rowVersion, 4, 'A notice refresh must not lend version 5 to old dirty mapping fields')
          return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic mapping changed. Reselect the person.' } })
        }
        return route.fulfill({ status: 200, json: { id: id(2499), rowVersion: 2 } })
      }
      if (path === 'users') { directoryRequests.push(url.href); return route.fulfill({ json: people }) }
      if (path === `projects/${project.id}/changes/options` || path === `projects/${project.id}/reviews/options`) return route.fulfill({ json: options })
      if (path === `projects/${project.id}/review-assignment-guidance`) return route.fulfill({ json: { excludedReviewerIds: [me.id, owner], authorIds: [me.id, owner], sourcesAvailable: true, allowSelfReview: false } })
      if (path === `projects/${project.id}/team`) return route.fulfill({ json: projectTeam({ members: people.map(person => teamMember({ userId: person.id,
        displayName: person.displayName, personFacts: { notices: person.personNotices, certifications: [] } })) }) })
      if (path === `projects/${project.id}/handoffs/options`) return route.fulfill({ json: handoffOptions({ people, canCreate: true }) })
      if (path === `tasks/${task.id}/pickup-offer`) return route.fulfill({ json: { id: id(2416), rowVersion: 1, reason: 'Synthetic pickup', openedAt: '2026-10-31T12:00:00Z',
        keepWatching: true, task: { key: task.key, name: task.name, projectId: project.id, projectDisciplineId: project.disciplines[0].id, dueDate: null },
        allowed: { assign: true, pickUp: false, withdraw: false }, candidates: pickupPeople } })
      if (path === `tasks/${task.id}`) return route.fulfill({ json: taskDetail })
      if (path === 'admin/users') return route.fulfill({ json: people.map(person => adminUser({ id: person.id, displayName: person.displayName,
        personFacts: { notices: person.personNotices, certifications: [] } })) })
      if (/^admin\/users\/[^/]+\/certifications$/.test(path)) return route.fulfill({ json: { userId: path.split('/')[2], rowVersion: 1, canEdit: true,
        today: '2026-10-31', choices: [], facts: { notices: noNotice, certifications: [] } } })
      if (path === 'certification-tags') return route.fulfill({ json: [] })
      if (path === 'organisation') return route.fulfill({ json: { levels: [{ id: id(2417), name: 'Team', sortOrder: 0 }],
        units: [{ id: unitId, name: 'Synthetic unit', levelId: id(2417), leaderId: unitLeader, rowVersion: 1, headCount: 0 }], people,
        managerCycles: [], includeIndirectReports: false } })
      if (path === 'organisation/placements') return route.fulfill({ json: [] })
      if (path === 'admin/organisation/levels') return route.fulfill({ json: [{ id: id(2417), name: 'Team', sortOrder: 0, rowVersion: 1 }] })
      if (path === 'admin/organisation/units') return route.fulfill({ json: [{ id: unitId, name: 'Synthetic unit', levelId: id(2417), leaderId: unitLeader, rowVersion: 1 }] })
      if (path === 'planning/availability') return route.fulfill({ json: availabilityRead({ canImport: true }) })
      if (path === 'planning/hr-identities') { refreshes++; return route.fulfill({ json: people.map(person => hrIdentity({ id: person.id,
        displayName: person.displayName, rowVersion: identityVersion, personNotices: person.personNotices })) }) }
      if (path === 'discipline-coordination') return route.fulfill({ json: { evaluatedAt: '2026-10-31T12:00:00Z', actorId: me.id, projects: [],
        projectChoices: [{ id: project.id, projectNumber: project.projectNumber, name: project.name, projectManagerId: me.id }], disciplines: [], owners: people, scope: 'Workspace' } })
      if (path === `projects/${project.id}/discipline-coordination`) return route.fulfill({ json: projection })
      if (path === `projects/${project.id}/coordination`) return route.fulfill({ json: weeklyCoordination() })
      if (path === `projects/${project.id}/attention`) return route.fulfill({ json: { items: [], snoozed: 0, total: 0, truncated: false } })
      if (path === `projects/${project.id}/design-basis/${basisId}`) return route.fulfill({ json: basis })
      if (path === `projects/${project.id}/design-basis`) return route.fulfill({ json: url.searchParams.has('AffectedWorkId') ? basisPage([{ ...entry, status: 'Proposed' }]) : emptyPage })
      if (path === `projects/${project.id}/readiness/Task/${task.id}`) return route.fulfill({ json: readinessDetail({ assessment: readinessAssessment({ id: assessmentId,
        projectId: project.id, targetId: task.id, ownerId: owner, rowVersion: 1 }) }) })
      if (path === `projects/${project.id}/readiness/window`) return route.fulfill({ json: readinessWindow() })
      if (path === `projects/${project.id}/weekly-commitments`) return route.fulfill({ json: commitmentList([]) })
      if (path.includes('/readiness/') && /\/(constraints|submission-prerequisites)$/.test(path)) return route.fulfill({ json: [] })
      if (path === `projects/${project.id}/readiness/link-options`) return route.fulfill({ json: [] })
      if (path === `projects/${project.id}/allocations/discipline-context`) return route.fulfill({ json: { weeks: [], disciplines: [], allocationCoverage: 'Complete', asOf: '2026-10-31T12:00:00Z' } })
      if (path === `projects/${project.id}/allocations/deadlines`) return route.fulfill({ json: { weeks: [], dated: [], undated: [], asOf: '2026-10-31T12:00:00Z' } })
      if (path === `projects/${project.id}/allocations/review-options`) return route.fulfill({ json: [] })
      if (path === `projects/${project.id}/open-roles` || path === `projects/${project.id}/milestones`) return route.fulfill({ json: [] })
      if (/^projects\/[^/]+\/(changes|reviews|submissions|allocations|input-uses)$/.test(path)) return route.fulfill({ json: emptyPage })
      if (path === `projects/${project.id}/handoffs`) return route.fulfill({ json: handoffPage([]) })
      if (path === `projects/${project.id}/issues`) return route.fulfill({ json: [] })
      if (path === `issues/${panelIds.issue}`) return route.fulfill({ json: issueDetail({ issue: issueRow({ id: panelIds.issue, projectId: project.id,
        key: 'I-SYNTHETIC', title: 'Synthetic verifier case', ownerId: owner, raisedById: me.id }) }) })
      if (/^issues\/[^/]+\/(locations|documents|verification|reference-impacts)$/.test(path)) return route.fulfill({ json: [] })
      return route.fallback()
    })
    const goto = async path => { visited.push(path); await page.goto(`${base}${path}`) }
    const openFilters = () => page.getByRole('button', { name: /^Filters/ }).first().click()
    const label = person => me.flags.capacity_screens && person.id === jill ? suffix : person.displayName ?? 'Unavailable'
    async function choices(control, expected = people, afterSelection) {
      await control.locator(`option[value="${expected[0].id}"]`).waitFor({ state: 'attached' })
      assert.deepEqual(await control.locator('option').evaluateAll(rows => rows.filter(row => row.value).map(row => ({ id: row.value, name: row.textContent }))),
        expected.map(person => ({ id: person.id, name: label(person) })))
      assert.equal(await control.locator('option').filter({ hasText: /Unknown dates.*Employee|Ended|Available/ }).count(), 0)
      if (expected.some(person => person.id === jill)) {
        const id = await control.getAttribute('id'), label = await control.getAttribute('aria-label')
        assert.ok(id || label, 'A native choice must have a stable accessible identifier')
        await control.selectOption(jill); if (afterSelection) await afterSelection()
        await page.waitForFunction(({ id, label, value }) => {
          const select = id ? document.getElementById(id) : [...document.querySelectorAll('select')].find(node => node.getAttribute('aria-label') === label)
          return select?.value === value
        }, { id, label, value: jill })
        assert.equal(await control.inputValue(), jill)
      }
      assert.equal(await page.locator('[data-person-certification]').count(), 0, 'Pickers do not display certifications')
    }
    const projectPath = tail => `/projects/${project.projectNumber}/${tail}`
    // All peopleChoices consumers: register filters and each independently mounted form.
    for (const [tab, filter, action, formLabel] of [['changes', 'Accountable owner', 'Register source revision', 'Accountable owner'],
      ['reviews', 'Coordinator', 'New review package', 'Coordinator'], ['submissions', 'Coordinator', 'New submission', 'Coordinator']]) {
      await goto(projectPath(tab)); await openFilters(); await choices(page.getByRole('combobox', { name: filter, exact: true }))
      assert.equal(new URL(page.url()).searchParams.get(tab === 'submissions' ? 'coordinatorId' : 'ownerId'), jill)
      await page.keyboard.press('Escape')
      await page.getByRole('button', { name: action, exact: true }).first().click()
      const dialog = page.getByRole('dialog'); await choices(dialog.getByRole('combobox', { name: formLabel, exact: true }))
      if (tab === 'reviews') {
        await dialog.getByRole('checkbox', { name: /S-SYNTHETIC/ }).check()
        await dialog.getByRole('checkbox', { name: 'Civil', exact: true }).check()
        await choices(dialog.getByRole('combobox', { name: 'Reviewer', exact: true }), people.filter(person => ![me.id, owner].includes(person.id)))
      }
      if (tab === 'submissions') { await dialog.getByRole('button', { name: 'Add optional check', exact: true }).click(); await choices(dialog.getByRole('combobox', { name: 'Accountable owner', exact: true })) }
    }
    await goto(projectPath('design-basis')); await page.getByRole('button', { name: 'New basis entry', exact: true }).click()
    await choices(page.getByRole('dialog').getByRole('combobox', { name: 'Accountable owner', exact: true }))
    await choices(page.getByRole('dialog').getByRole('combobox', { name: 'Independent approver', exact: true }))
    await goto(projectPath('allocations')); await openFilters(); await choices(page.getByRole('combobox', { name: 'Person', exact: true }).first()); await page.keyboard.press('Escape')
    await page.getByRole('button', { name: 'Propose allocation', exact: true }).first().click()
    await choices(page.getByRole('dialog').getByRole('combobox', { name: 'Person', exact: true }))
    await goto(projectPath('handoffs')); await page.getByRole('button', { name: 'New handoff', exact: true }).click()
    await choices(page.locator('#hf-sender')); await choices(page.locator('#hf-receiver'))
    await goto(projectPath(`tasks?panel=Task:${task.id}`)); await choices(page.getByRole('combobox', { name: 'Assign pickup offer', exact: true }), pickupPeople)
    const pickupWrite = page.waitForRequest(request => request.url().endsWith(`/tasks/${task.id}/pickup-offer/assign`) && request.method() === 'POST')
    await page.getByRole('button', { name: 'Assign pickup', exact: true }).click()
    assert.equal((await pickupWrite).postDataJSON().userId, jill); assert.equal(writes.at(-1).body.rowVersion, 1)
    await goto(projectPath('coordination')); await openFilters(); await choices(page.locator('#dcv-owner')); assert.equal(new URL(page.url()).searchParams.get('owner'), jill)
    await goto('/my-work?tab=coordination'); await openFilters()
    const ownerRead = page.waitForResponse(response => {
      const url = new URL(response.url()); return url.pathname === '/api/v1/discipline-coordination' && url.searchParams.get('ownerId') === jill && response.status() === 200
    })
    await choices(page.locator('#workspace-coordination-owner'), people, async () => {
      await page.waitForURL(url => url.searchParams.get('ownerId') === jill); assert.equal(await (await ownerRead).finished(), null)
      // The scoped query's loading state remounts FilterBar. Reopen it and verify the retained selection.
      await openFilters()
    })
    assert.equal(new URL(page.url()).searchParams.get('ownerId'), jill)
    // Removal-owner and exception-verifier exclusions are independent from recorded dates.
    await goto(projectPath(`readiness?inspect=1&inspectWork=Task:${task.id}`))
    await page.getByRole('button', { name: 'Record constraint', exact: true }).click()
    await choices(page.getByRole('dialog').getByRole('combobox', { name: 'Removal owner', exact: true }), people.filter(person => person.id !== owner))
    await goto(projectPath(`readiness?inspect=1&inspectWork=Task:${task.id}`))
    await page.getByRole('button', { name: 'Approve assumption exception', exact: true }).click()
    await choices(page.getByRole('dialog').getByRole('combobox', { name: 'Responsible verifier', exact: true }), people.filter(person => ![owner, me.id].includes(person.id)))
    await goto('/admin/users'); await page.getByRole('button', { name: 'Add local user', exact: true }).click()
    await choices(page.locator('#new-user-supervisor'))
    await goto('/admin/users'); await page.getByRole('row').filter({ hasText: 'Recorded owner' }).getByRole('button', { name: 'Edit', exact: true }).click()
    await choices(page.getByRole('dialog', { name: 'Recorded owner', exact: true }).getByRole('combobox', { name: 'Supervisor', exact: true }), people.filter(person => person.id !== owner))
    await goto('/admin/structure')
    const leaderWrite = page.waitForRequest(request => request.url().endsWith(`/admin/organisation/units/${unitId}`) && request.method() === 'PATCH')
    await choices(page.getByRole('combobox', { name: 'Unit leader: Synthetic unit', exact: true })); assert.equal((await leaderWrite).postDataJSON().leaderId, jill)
    // The only custom candidate PeoplePicker must use its supplied subset with no /users fill-in.
    for (const allowSelfReview of [false, true]) {
      me.settings.allowSelfReview = allowSelfReview
      await goto(projectPath(`issues?panel=Issue:${panelIds.issue}`))
      const before = directoryRequests.length
      await page.getByRole('button', { name: 'Verifier', exact: true }).click()
      const candidates = allowSelfReview ? people : people.filter(person => ![owner, me.id].includes(person.id))
      const verifierOptions = page.locator('[data-slot="command-list"]:visible').getByRole('option')
      await verifierOptions.filter({ hasText: 'Jill Martin' }).waitFor()
      const actual = await verifierOptions.allTextContents()
      assert.equal(actual.length, candidates.length); assert.ok(actual.some(text => text.includes('Jill Martin') && text.includes('Co-op') && text.includes('Ends 2026-12-18')))
      if (!allowSelfReview) { assert.ok(actual.every(text => !text.includes('Alex Chen') && !text.includes('Recorded owner'))) }
      assert.equal(directoryRequests.length, before, 'Custom verifier must not query the general directory')
      await verifierOptions.filter({ hasText: 'Jill Martin' }).click()
      await page.getByRole('textbox', { name: 'Appointment reason', exact: true }).fill('Synthetic independent verifier')
      const verificationWrite = page.waitForRequest(request => request.url().endsWith(`/issues/${panelIds.issue}/verification`) && request.method() === 'POST')
      await page.getByRole('button', { name: 'Propose verifier', exact: true }).click()
      assert.equal((await verificationWrite).postDataJSON().verifierId, jill)
    }
    // Native keyboard selection and 375px containment; unchanged synthetic on-data also reaches flag-off controls.
    await page.setViewportSize({ width: 375, height: 812 })
    await goto('/vacation'); await page.getByRole('button', { name: 'Import approved leave', exact: true }).click()
    await choices(page.locator('#hr-person'))
    // Native type-ahead commits in headless macOS Chrome; its OS-popup arrow keys do not.
    await page.locator('#hr-person').press('a')
    await page.waitForFunction(id => document.getElementById('hr-person')?.value === id, people[0].id)
    assert.equal(await page.locator('#hr-person').inputValue(), people[0].id)
    await page.locator('#hr-person').selectOption(jill); await page.locator('#hr-source').fill('Dirty HR source'); await page.locator('#hr-key').fill('dirty-old-key')
    await page.clock.install(); identityVersion = 5
    const readsBefore = refreshes; const refreshed = page.waitForResponse(response => response.url().includes('/planning/hr-identities') && response.request().method() === 'GET')
    await page.clock.fastForward(16000)
    const other = await ui.browser.newPage(); await other.bringToFront(); await page.bringToFront(); await refreshed; await other.close()
    assert.ok(refreshes > readsBefore); assert.equal(await page.locator('#hr-key').inputValue(), 'dirty-old-key')
    await page.getByRole('button', { name: 'Save HR mapping', exact: true }).click()
    await page.getByRole('alert').filter({ hasText: 'Synthetic mapping changed' }).waitFor()
    assert.equal(writes.at(-1).body.rowVersion, 4); assert.equal(writes.at(-1).body.personKey, 'dirty-old-key')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), '375px document containment')
    me.flags.capacity_screens = false; me.flags.people_screens = true
    await goto(projectPath('changes')); await openFilters(); await choices(page.getByRole('combobox', { name: 'Accountable owner', exact: true }))
    await goto('/vacation'); await page.getByRole('button', { name: 'Import approved leave', exact: true }).click(); await choices(page.locator('#hr-person'))
    assert.equal(errors.length, 0, errors.join('\n'))
    const allowedWrites = writes.filter(write => !write.path.endsWith('/pickup-offer/assign') && !write.path.endsWith('/verification') &&
      !write.path.startsWith('admin/organisation/units/') && !write.path.startsWith('planning/hr-identities/'))
    assert.deepEqual(allowedWrites, [], 'Rendering/filtering/choosing labels must not add writes')
    console.log(JSON.stringify({ status: 'PASS', evidence: 'rendered synthetic only', routes: visited.length, writePaths: writes.map(write => write.path),
      customDirectoryRequests: directoryRequests.length, errors, realApiPermissions: 'covered separately', nativeDevicePopup: 'not certified' }))
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
