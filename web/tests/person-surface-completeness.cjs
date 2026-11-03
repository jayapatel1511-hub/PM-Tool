// Synthetic source-class acceptance. Never start a preview/API; the coordinator supplies its verified build.
const assert = require('node:assert/strict')
const { newMockPage, me, project, links } = require('./platform-fixtures.cjs')
const { taskRow, taskDetail, taskPage, taskPermissions, taskPermission } = require('./tasks.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const { riskRow, riskDetail } = require('./registers.fixture.cjs')
const { submissionListRow, submissionPage } = require('./submissions.fixture.cjs')
const { planningEntry, planningGrid, planningPerson, planningCell, entryPage } = require('./planning.fixture.cjs')
const { allocationRow, allocationPage } = require('./allocations.fixture.cjs')
const { coordinationOptions, reviewPackageRow, reviewRoundRow, reviewRow, reviewPage, reviewDetail } = require('./reviews.fixture.cjs')
const { id, stamp, week, surfacePerson, surfaceComments, surfaceHistory, surfaceShift, surfaceBlockerSubmission } = require('./person-surface-completeness.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use only the coordinator local preview')
const people = { alex: me.id, jill: id(7101), historical: id(7102), external: id(7103) }
const names = new Map([[people.alex, 'Alex Surface'], [people.jill, 'Jill Surface']])
const work = taskRow({ id: id(7110), projectId: project.id, key: 'P-MOCK-T-SURFACE', name: 'Synthetic person surface task',
  projectNumber: project.projectNumber, projectName: project.name, projectDisciplineId: project.disciplines[0].id,
  assigneeId: people.alex, assigneeName: names.get(people.alex), reviewerId: people.jill, reviewerName: names.get(people.jill),
  can: { assign: false, edit: false, changeDue: false, block: false }, commentCount: 24, estimatedHours: 8, dueDate: week })
const permissions = taskPermissions({ edit: taskPermission(false), assign: taskPermission(false), setReviewer: taskPermission(false),
  dueDate: taskPermission(false), block: taskPermission(false), delete: taskPermission(false), restore: false, dependencies: false, enterTime: false })
const detail = () => taskDetail({ task: work, permissions, description: 'Synthetic description',
  collaborators: [{ id: people.jill, displayName: names.get(people.jill), isActive: true }],
  watchers: [{ id: people.jill, displayName: names.get(people.jill), isActive: true }] })
const entry = planningEntry({ id: id(7120), personId: people.alex, personName: names.get(people.alex), ownerId: people.jill,
  ownerName: names.get(people.jill), label: 'Synthetic editable retained plan', startWeek: week, endWeek: week })
const allocation = allocationRow({ id: id(7130), personId: people.jill, personName: names.get(people.jill), fromDate: week, throughDate: '2026-10-30', status: 'Proposed' })
const review = reviewPackageRow({ id: id(7140), key: 'RV-SURFACE', title: 'Synthetic unmounted coordinator', projectId: project.id, coordinatorId: people.jill, currentRoundId: id(7141) })
const risk = riskRow({ id: id(7160), projectId: project.id, key: 'R-SURFACE', title: 'Synthetic scoped source tab', ownerId: people.jill, ownerName: names.get(people.jill) })
const blockerSubmission = surfaceBlockerSubmission(project.id, people.alex, people.jill)
const discussion = surfaceComments({ author: people.jill, authorName: names.get(people.jill), mention: people.alex,
  mentionName: names.get(people.alex), historical: people.historical })

;(async () => {
  const savedFlags = { ...me.flags }, savedCapabilities = { ...me.capabilities }, savedProjectPermissions = { ...project.permissions }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], personalVisits = [], previews = [], panelReads = [], gridReads = [], historyReads = [], unexpectedGets = [], cases = []
  let previewArmed = false
  page.setDefaultTimeout(15000)
  page.on('pageerror', error => errors.push(error.message))
  try {
    me.flags = Object.fromEntries(Object.keys(savedFlags).map(key => [key, false]))
    Object.assign(me.flags, { people_screens: true, capacity_screens: true, work_screens: true, small_ux: true,
      data_entry_safety: true, my_work_follow_up: true })
    me.capabilities = { ...savedCapabilities, workload: true, readOnly: false }
    project.permissions.isPm = true; project.permissions.proposeAllocation = { ok: true, reason: null }
    me.settings.today = '2026-10-31'; me.settings.dateFormat = 'yyyy-MM-dd'
    const allocationRoot = `projects/${project.id}/allocations`
    await page.route('**/api/**', async route => {
      const req = route.request(), url = new URL(req.url()), path = url.pathname.replace('/api/v1/', '')
      if (req.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback() // only the existing synthetic authentication handshake
        if (path === 'me/work/visits' && req.method() === 'POST') {
          const body = req.postDataJSON()
          assert.deepEqual(Object.keys(body), ['requestId']); assert.match(body.requestId, /^[0-9a-f-]{36}$/i)
          personalVisits.push(body)
          return route.fulfill({ json: { requestId: body.requestId, committedAt: stamp, revision: personalVisits.length,
            firstVisitAt: stamp, previousVisitAt: null, lastVisitAt: stamp, lastMarkAllAt: null } })
        }
        if (path === `${allocationRoot}/shift-preview` && req.method() === 'POST' && previewArmed) {
          previewArmed = false
          const body = req.postDataJSON(); previews.push(body)
          assert.equal(body.mode, 'WholeProject'); assert.equal(body.weeks, 1)
          return route.fulfill({ json: surfaceShift(allocation, body) })
        }
        writes.push({ method: req.method(), path, body: req.postData() })
        return route.fulfill({ status: 409, json: { code: 'unexpected_surface_write', detail: 'No business mutation is authorized by this read-only test' } })
      }
      if (/^people\/[^/]+\/panel$/.test(path)) {
        const user = path.split('/')[1]; panelReads.push({ user, query: Object.fromEntries(url.searchParams) })
        if (!names.has(user)) return route.fulfill({ status: 404, contentType: 'application/problem+json', json: {
          type: 'https://hub.local/errors/not_found', title: 'Not found', status: 404, code: 'not_found', detail: 'Historical person is outside narrow scope', traceId: 'synthetic-surface404' } })
        return route.fulfill({ json: surfacePerson(user, names.get(user)) })
      }
      if (path === 'planning/grid') {
        gridReads.push(Object.fromEntries(url.searchParams))
        return route.fulfill({ json: planningGrid({ weeks: [week], currentWeek: week, people: [planningPerson({ id: people.alex,
          displayName: names.get(people.alex), cells: [planningCell({ week })], entries: [entry] }), planningPerson({ id: people.jill,
          displayName: names.get(people.jill), cells: [planningCell({ week })] })] }) })
      }
      if (path === 'planning/entries') return route.fulfill({ json: entryPage([entry]) })
      if (path === `planning/entries/${entry.id}`) return route.fulfill({ json: entry })
      if (path === `planning/entries/${entry.id}/activity`) return route.fulfill({ json: surfaceHistory(people.jill, names.get(people.jill), entry.id) })
      if (path === `risks/${risk.id}`) return route.fulfill({ json: riskDetail({ risk, permissions: { edit: { ok: false, reason: 'Synthetic read only' }, transitions: [], comment: true } }) })
      if (path === `items/Risk/${risk.id}/comments`) return route.fulfill({ json: discussion })
      if (path === `items/Risk/${risk.id}/activity`) {
        historyReads.push(path); return route.fulfill({ json: surfaceHistory(people.jill, names.get(people.jill), risk.id) })
      }
      if (path === `tasks/${work.id}`) return route.fulfill({ json: detail() })
      if (path === `tasks/${work.id}/dependencies`) return route.fulfill({ json: taskDependencies() })
      if (path === 'tasks' || path === `projects/${project.id}/tasks`) return route.fulfill({ json: taskPage([work]) })
      if (path === `items/Task/${work.id}/links`) return route.fulfill({ json: links })
      if (path === `items/Task/${work.id}/comments`) return route.fulfill({ json: discussion })
      if (path === `items/Task/${work.id}/activity` || path === `projects/${project.id}/activity`) {
        return route.fulfill({ json: surfaceHistory(people.jill, names.get(people.jill), work.id) })
      }
      if (path === allocationRoot) return route.fulfill({ json: allocationPage([allocation]) })
      if (path === `${allocationRoot}/discipline-context`) return route.fulfill({ json: { weeks: [week], allocationCoverage: 'Complete', disciplines: [], asOf: stamp } })
      if (path === `${allocationRoot}/deadlines`) return route.fulfill({ json: { weeks: [week], dated: [], undated: [], asOf: stamp } })
      if (path === `projects/${project.id}/open-roles`) return route.fulfill({ json: [] })
      if (path === `projects/${project.id}/submissions`) return route.fulfill({ json: submissionPage([submissionListRow({
        id: blockerSubmission.detail.package.id, key: blockerSubmission.detail.package.key, title: blockerSubmission.detail.package.title,
        status: 'Checking', coordinatorId: people.alex, targetDate: '2026-11-12', blockerCount: blockerSubmission.detail.readiness.blockers.length,
      })]) })
      if (path === `projects/${project.id}/submissions/${blockerSubmission.detail.package.id}`) return route.fulfill({ json: blockerSubmission.detail })
      if (path === `projects/${project.id}/changes/options`) return route.fulfill({ json: coordinationOptions({ actorId: people.alex,
        people: [...names].map(([id, displayName]) => ({ id, displayName })), disciplines: blockerSubmission.disciplines, canWrite: false }) })
      if (path === `projects/${project.id}/reviews`) return route.fulfill({ json: reviewPage([reviewRow({ id: review.id, key: review.key, title: review.title, coordinatorId: people.jill })]) })
      if (path === `projects/${project.id}/reviews/${review.id}`) return route.fulfill({ json: reviewDetail({ package: review, actorId: people.alex,
        rounds: [reviewRoundRow({ id: review.currentRoundId, packageId: review.id, projectId: project.id, number: 1 })] }) })
      if (path === 'me/following') return route.fulfill({ json: { page: 1, pageSize: 200, totalCount: 1,
        projects: [{ projectId: project.id, projectNumber: project.projectNumber, name: project.name, unread: 1 }],
        items: [{ entry: { ...surfaceHistory(people.jill, names.get(people.jill), work.id).items[0], projectId: project.id },
          projectNumber: project.projectNumber, projectName: project.name, count: 1, notified: false, unread: true }] } })
      if (path === 'me/work/watching' || path === 'me/work/requested') return route.fulfill({ json: { total: 3, items: [
        { id: work.id, itemType: 'Task', key: work.key, title: work.name, projectId: project.id, projectNumber: project.projectNumber, status: work.status, dueDate: week,
          ownerId: people.jill, ownerName: names.get(people.jill), ownerKind: 'User' },
        { id: id(7150), itemType: 'Decision', key: 'DEC-EXTERNAL', title: 'Synthetic external owner', projectId: project.id, projectNumber: project.projectNumber, status: 'Pending', dueDate: week,
          ownerId: people.external, ownerName: 'Synthetic external client', ownerKind: 'ExternalParty' },
        { id: id(7151), itemType: 'Decision', key: 'DEC-LEGACY', title: 'Synthetic legacy unknown kind', projectId: project.id, projectNumber: project.projectNumber, status: 'Pending', dueDate: week,
          ownerId: people.jill, ownerName: 'Legacy untyped owner' },
      ] } })
      if (path === 'users') return route.fulfill({ json: [...names].map(([id, displayName]) => ({ id, displayName, isActive: true })) })
      // Restrict fallthrough to existing platform reads; its unknown-GET log is asserted at the end.
      const platform = ['config', 'me', 'views', 'reference', 'projects', 'workspaces', 'me/work', 'admin/users', 'me/notifications/unread-count', 'me/notifications/pulse']
      if (platform.includes(path) || path === `projects/${project.id}` || path === `projects/${project.projectNumber}`
        || path === `projects/${project.id}/deliverables` || path === `projects/${project.id}/milestones` || path === `projects/${project.id}/team`) return route.fallback()
      unexpectedGets.push(path)
      return route.fulfill({ status: 501, json: { detail: `Unhandled surface GET: ${path}` } })
    })

    const projectUrl = `${base}/projects/${project.projectNumber}`
    const taskUrl = `${projectUrl}/tasks?status=Not+Started&sort=key%3Aasc&group=none&panel=Task:${work.id}&taskSection=comments`
    const panel = () => page.locator('[data-person-panel]')
    const trigger = (key, user) => page.locator(`[data-person-panel-trigger="${user}"][id*="${encodeURIComponent(key).slice(0, 64)}"]:visible`).first()
    const uniquePersonIds = async () => {
      const ids = await page.locator('[data-person-panel-trigger][id]').evaluateAll(nodes => nodes.map(node => node.id))
      assert.equal(ids.length, new Set(ids).size, 'Simultaneous real/phone/table/group/overlay User triggers have unique DOM IDs')
    }
    const frames = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const roundTrip = async (source, user, options = {}) => {
      await source.waitFor({ state: 'visible' }); await source.scrollIntoViewIfNeeded(); await source.focus(); await frames()
      const origin = page.url(), focusId = await source.getAttribute('id'), writesBefore = writes.length
      assert.ok(focusId && focusId.length <= 200)
      const before = await source.evaluate(node => {
        const surface = node.closest('[data-slot="dialog-body"]') ?? node.closest('[data-slot="sheet-content"],[data-slot="dialog-content"]') ?? document.getElementById('content')
        return { nested: !!node.parentElement?.closest('a,button,summary,[role="option"]'), children: node.querySelectorAll('a,button,input,select').length,
          slot: surface?.getAttribute('data-slot') ?? surface?.id, top: surface?.scrollTop ?? 0, left: surface?.scrollLeft ?? 0,
          disclosures: Array.from(surface?.querySelectorAll('details[data-person-source-disclosure][id]') ?? []).map(d => ({ id: d.id, open: d.open })) }
      })
      assert.equal(before.nested, false); assert.equal(before.children, 0)
      if (options.requireScroll) assert.ok(before.top > 0, 'Source scroll is actually nonzero')
      await page.keyboard.press('Enter')
      if (options.denied) {
        const denied = await options.response
        assert.equal(denied.status(), 404)
        await page.getByText('This page or item does not exist, or you cannot view it.', { exact: true }).last().waitFor()
        assert.equal(await panel().count(), 0, 'No denied historical facts remain')
      } else {
        await page.locator(`[data-person-panel="${user}"]`).waitFor()
        await page.waitForURL(url => url.searchParams.get('panel') === `Person:${user}`)
        if (options.unmounted) assert.equal(await page.locator(`[id=${JSON.stringify(focusId)}]`).count(), 0, 'Source really unmounts for nested Person')
      }
      if (options.disclosure) {
        const open = await page.locator(`[id=${JSON.stringify(options.disclosure.id)}]`).evaluate(node => node.open)
        assert.equal(open, options.disclosure.open, 'Person action itself does not toggle the native group before return restoration')
      }
      await uniquePersonIds()
      await page.keyboard.press('Escape')
      await page.waitForURL(origin)
      await page.waitForFunction(id => document.activeElement?.id === id, focusId)
      await frames()
      assert.equal(page.url(), origin)
      const after = await page.locator(`[id=${JSON.stringify(focusId)}]`).evaluate(node => {
        const surface = node.closest('[data-slot="dialog-body"]') ?? node.closest('[data-slot="sheet-content"],[data-slot="dialog-content"]') ?? document.getElementById('content')
        return { slot: surface?.getAttribute('data-slot') ?? surface?.id, top: surface?.scrollTop ?? 0, left: surface?.scrollLeft ?? 0,
          disclosures: Array.from(surface?.querySelectorAll('details[data-person-source-disclosure][id]') ?? []).map(d => ({ id: d.id, open: d.open })) }
      })
      assert.equal(after.slot, before.slot); assert.deepEqual(after.disclosures, before.disclosures, 'Native open AND closed disclosure states survive return'); assert.ok(Math.abs(after.top - before.top) <= 2); assert.ok(Math.abs(after.left - before.left) <= 2)
      assert.equal(writes.length, writesBefore, 'Opening/closing Person issued no business command')
      await uniquePersonIds()
    }

    // Saved-author/mention references, actual Task remount and restored dirty composer.
    await page.goto(taskUrl)
    const box = page.getByRole('textbox', { name: 'Comment', exact: true })
    await box.fill('Synthetic unsent discussion remains after Person')
    const author = trigger(`comment:Task:${work.id}:${id(7200)}:author`, people.jill)
    await roundTrip(author, people.jill, { unmounted: true })
    assert.equal(await box.inputValue(), 'Synthetic unsent discussion remains after Person')
    const mention = page.getByRole('button', { name: `View details for ${names.get(people.alex)}`, exact: true }).filter({ has: page.getByText(`@${names.get(people.alex)}`, { exact: true }) })
    await roundTrip(mention, people.alex, { unmounted: true })
    assert.equal(await page.getByRole('button', { name: 'View details for Unvalidated token', exact: true }).count(), 0)
    assert.equal(await box.inputValue(), 'Synthetic unsent discussion remains after Person')
    const deniedResponse = page.waitForResponse(r => r.request().method() === 'GET' && new URL(r.url()).pathname === `/api/v1/people/${people.historical}/panel` && r.status() === 404)
    await roundTrip(trigger(`comment:Task:${work.id}:${id(7223)}:author`, people.historical), people.historical, { denied: true, response: deniedResponse, requireScroll: true })
    cases.push('stored-author-mention-dirty-composer-remount-historical404')

    // Separate known User roles on the same Task do not remove collaborators or toggle Watch.
    await page.goto(taskUrl)
    await roundTrip(trigger(`task:${work.id}:collaborator:`, people.jill), people.jill)
    await roundTrip(trigger(`task:${work.id}:watcher:`, people.jill), people.jill)
    await roundTrip(trigger(`task:${work.id}:assignee:detail`, people.alex), people.alex)
    assert.equal(await page.getByRole('button', { name: `Remove ${names.get(people.jill)}`, exact: true }).count(), 1)
    cases.push('inline-person-collaborator-watcher-distinct-actions')

    // Legacy grouped summaries keep the actor inside the native toggle when people links are off.
    me.flags.people_screens = false
    const beforeDarkGroups = panelReads.length
    await page.goto(`${projectUrl}/activity?groupChanges=true&actorId=${people.jill}&page=1`)
    const darkActivityGroup = page.locator('details[id^="activity-group-"]').first()
    const darkActivitySummary = darkActivityGroup.locator('summary').first()
    await darkActivitySummary.waitFor()
    assert.ok((await darkActivitySummary.innerText()).startsWith(`${names.get(people.jill)} made 2 changes`))
    assert.equal(await darkActivityGroup.evaluate(node => Array.from(node.parentElement.childNodes).filter(n => n.nodeType === Node.TEXT_NODE).map(n => n.textContent).join('').trim()), '', 'Actor is not duplicated outside the original dark summary')
    assert.equal(await darkActivityGroup.evaluate(node => node.parentElement.children.length === 1 && node.parentElement.firstElementChild === node), true, 'Dark group keeps the original details-only composition')
    assert.equal(await darkActivitySummary.locator('button,a').count(), 0)
    await darkActivitySummary.click(); assert.equal(await darkActivityGroup.evaluate(node => node.open), true)
    await darkActivitySummary.click(); assert.equal(await darkActivityGroup.evaluate(node => node.open), false)
    await page.goto(`${projectUrl}/tasks?panel=Task:${work.id}&taskSection=history&groupChanges=true`)
    const darkHistoryGroup = page.locator('details[id^="history-group-"]').first()
    const darkHistorySummary = darkHistoryGroup.locator('summary').first()
    await darkHistorySummary.waitFor()
    assert.ok((await darkHistorySummary.innerText()).startsWith(`${names.get(people.jill)} made 2 changes`))
    assert.equal(await darkHistorySummary.locator('button,a').count(), 0)
    await darkHistorySummary.click(); assert.equal(await darkHistoryGroup.evaluate(node => node.open), true)
    await darkHistorySummary.click(); assert.equal(await darkHistoryGroup.evaluate(node => node.open), false)
    assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0)
    assert.equal(panelReads.length, beforeDarkGroups, 'Legacy group toggles never open/read Person')
    cases.push('dark-activity-and-history-actor-summary-native-toggle')
    me.flags.people_screens = true

    // Grouped ActivityTable and User/Admin history remain read-only, with disclosure kept.
    await page.goto(`${projectUrl}/activity?groupChanges=true&actorId=${people.jill}&page=1`)
    const closedActivityGroup = page.locator('details[id^="activity-group-"]').first()
    await closedActivityGroup.waitFor()
    assert.equal(await closedActivityGroup.evaluate(node => node.open), false)
    assert.equal(await closedActivityGroup.locator('summary').first().locator('button,a').count(), 0)
    await roundTrip(trigger('activity-group:', people.jill), people.jill, { disclosure: { id: `activity-group-${id(7301)}`, open: false } })
    const disclosure = page.locator('details').filter({ has: page.locator('summary').filter({ hasText: 'made 2 changes' }) }).first()
    await disclosure.locator('summary').click()
    await roundTrip(disclosure.locator('[data-person-panel-trigger]').first(), people.jill, { disclosure: { id: `activity-group-${id(7301)}`, open: true } })
    assert.equal(await disclosure.getAttribute('open'), '')
    await uniquePersonIds(); cases.push('grouped-audit-user-admin-preserved-fold')

    await page.goto(`${projectUrl}/tasks?panel=Task:${work.id}&taskSection=history&groupChanges=true`)
    const historyFolds = page.locator('details[data-person-source-disclosure][id^="history-group-"]')
    await historyFolds.nth(1).waitFor()
    await historyFolds.nth(0).locator('summary').click()
    await historyFolds.nth(1).locator('summary').click(); await historyFolds.nth(1).locator('summary').click()
    assert.equal(await historyFolds.nth(0).evaluate(d => d.open), true)
    assert.equal(await historyFolds.nth(1).evaluate(d => d.open), false)
    await roundTrip(trigger(`history:Task:${work.id}:`, people.jill), people.jill, { unmounted: true })
    assert.equal(await page.getByRole('tab', { name: 'History', exact: true }).getAttribute('aria-selected'), 'true')
    assert.equal(await historyFolds.nth(0).evaluate(d => d.open), true)
    assert.equal(await historyFolds.nth(1).evaluate(d => d.open), false)
    await page.goto(`${base}/notifications?tab=following&projectId=${project.id}`)
    await roundTrip(trigger('notification:', people.jill), people.jill)
    assert.equal(writes.length, 0, 'Notification actor does not acknowledge the feed')
    cases.push('shared-history-tab-remount-and-notification-actor')

    const riskUrl = `${projectUrl}/tasks?group=none&panel=Risk:${risk.id}`
    await page.goto(`${riskUrl}&recordTab=history&recordTabItem=${encodeURIComponent(`Risk:${risk.id}`)}`)
    await roundTrip(trigger(`history:Risk:${risk.id}:`, people.jill), people.jill, { unmounted: true })
    assert.equal(await page.getByRole('tab', { name: 'History', exact: true }).getAttribute('aria-selected'), 'true')
    await page.goto(`${riskUrl}&recordTab=comments&recordTabItem=${encodeURIComponent(`Risk:${risk.id}`)}`)
    await roundTrip(trigger(`comment:Risk:${risk.id}:`, people.jill), people.jill, { unmounted: true })
    const beforeForeign = historyReads.length
    await page.goto(`${riskUrl}&recordTab=history&recordTabItem=${encodeURIComponent(`Task:${work.id}`)}`)
    await page.getByRole('tab', { name: 'History', exact: true }).waitFor(); await frames()
    assert.equal(await page.getByRole('tab', { name: 'History', exact: true }).getAttribute('aria-selected'), 'false')
    assert.equal(historyReads.length, beforeForeign, 'Another record cannot inject its History tab/read')
    await page.goto(`${riskUrl}&recordTab=unsupported&recordTabItem=${encodeURIComponent(`Risk:${risk.id}`)}`)
    await page.getByRole('tab', { name: 'History', exact: true }).waitFor()
    assert.equal(await page.getByRole('tab', { name: 'History', exact: true }).getAttribute('aria-selected'), 'false')
    cases.push('risk-comment-history-remount-exact-record-tab-allowlist')

    // Planner's actual local editor remains dirty beneath the nested Person sheet.
    await page.goto(`${base}/planner?view=list&from=${week}&weeks=1&entry=${entry.id}`)
    const label = page.locator('#planner-label')
    await label.fill('Synthetic unsaved plan label')
    const entrySheet = page.getByRole('dialog').filter({ has: label })
    await entrySheet.getByRole('button', { name: 'History', exact: true }).click()
    await roundTrip(trigger(`planning-entry:${entry.id}:activity:`, people.jill), people.jill)
    assert.equal(await label.inputValue(), 'Synthetic unsaved plan label')
    assert.equal(await entrySheet.getByRole('button', { name: 'Save', exact: true }).count(), 1)
    cases.push('planner-entry-history-retains-local-draft')

    // One explicit preview setup read, no shift/apply command; exact preview/reason survive Person.
    await page.goto(`${projectUrl}/allocations?from=${week}&purpose=Production&page=1`)
    await page.getByRole('button', { name: 'Shift all', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Shift whole project · Proposed and Confirmed', exact: true }).click()
    const shift = page.getByRole('dialog', { name: 'Shift allocation dates', exact: true })
    await shift.getByLabel('Weeks to shift', { exact: true }).fill('1')
    previewArmed = true
    await shift.getByRole('button', { name: 'Preview shift', exact: true }).click()
    await shift.getByRole('region', { name: 'Allocation shift preview', exact: true }).waitFor()
    await shift.getByLabel('Reason', { exact: true }).fill('Synthetic unsaved shift reason')
    const previewText = await shift.locator('[data-shift-preview-row]').innerText()
    await roundTrip(trigger(`allocation-shift:${project.id}:${allocation.id}:preview-person`, people.jill), people.jill)
    assert.equal(await shift.getByLabel('Reason', { exact: true }).inputValue(), 'Synthetic unsaved shift reason')
    assert.equal(await shift.locator('[data-shift-preview-row]').innerText(), previewText)
    assert.equal(await shift.getByLabel('Weeks to shift', { exact: true }).inputValue(), '1')
    assert.equal(previews.length, 1); assert.equal(previewArmed, false)
    cases.push('allocation-shift-retains-preview-and-reason-zero-apply')

    // Coordination PersonLabel really unmounts and restores the coordinator's stable key.
    await page.goto(`${projectUrl}/reviews?status=Draft&page=1&panel=ReviewPackage:${review.id}`)
    await roundTrip(trigger(`review:${review.id}:coordinator`, people.jill), people.jill, { unmounted: true })
    cases.push('person-label-real-custom-dialog-remount')

    // A single User owns two actual discipline groups in the legacy Submissions view.
    me.flags.people_screens = true; me.flags.screen_patterns = false
    await page.setViewportSize({ width: 390, height: 600 })
    await page.goto(`${projectUrl}/submissions?status=Checking&page=1&panel=SubmissionPackage:${blockerSubmission.detail.package.id}`)
    const firstBlockerOwner = page.getByRole('heading', { level: 4 }).filter({ hasText: blockerSubmission.disciplines[0].name }).locator(`[data-person-panel-trigger="${people.jill}"]`)
    const secondBlockerOwner = page.getByRole('heading', { level: 4 }).filter({ hasText: blockerSubmission.disciplines[1].name }).locator(`[data-person-panel-trigger="${people.jill}"]`)
    await firstBlockerOwner.waitFor(); await secondBlockerOwner.waitFor()
    assert.equal(await firstBlockerOwner.count(), 1); assert.equal(await secondBlockerOwner.count(), 1)
    const firstBlockerId = await firstBlockerOwner.getAttribute('id'), secondBlockerId = await secondBlockerOwner.getAttribute('id')
    assert.notEqual(firstBlockerId, secondBlockerId, 'Full discipline/owner group identity gives distinct focus IDs for the same User')
    await uniquePersonIds()
    await roundTrip(secondBlockerOwner, people.jill, { unmounted: true, requireScroll: true })
    assert.equal(await secondBlockerOwner.getAttribute('id'), secondBlockerId)
    assert.equal(await page.evaluate(() => document.activeElement?.id), secondBlockerId, 'Natural return goes to the original SECOND discipline trigger')
    assert.notEqual(await page.evaluate(() => document.activeElement?.id), firstBlockerId)
    cases.push('legacy-submission-same-user-two-discipline-groups-second-trigger-return')
    await page.setViewportSize({ width: 1440, height: 1000 })

    // Polymorphic and legacy owners stay typed; adjacent Unwatch is not invoked.
    await page.goto(`${base}/my-work?tab=watching`)
    await roundTrip(trigger(`follow-up:watching:Task:${work.id}:owner`, people.jill), people.jill)
    assert.equal(await page.getByRole('button', { name: 'View details for Synthetic external client', exact: true }).count(), 0)
    assert.equal(await page.getByRole('button', { name: 'View details for Legacy untyped owner', exact: true }).count(), 0)
    await page.getByText('Synthetic external client', { exact: true }).waitFor()
    cases.push('follow-up-user-external-legacy-kind')

    // Board duplicate-ID guard includes the real keyboard drag preview and natural cancellation focus.
    await page.goto(`${projectUrl}/board?swim=assignee`)
    await roundTrip(trigger(`board-card:${work.id}:assignee:footer`, people.alex), people.alex)
    const handle = page.getByRole('button', { name: new RegExp(`Move.*${work.key}`) }).first()
    await handle.focus(); await page.keyboard.press('Space')
    await page.waitForFunction(() => !!document.querySelector('[data-slot="card"].pointer-events-none'))
    await frames(); await uniquePersonIds()
    assert.equal(await page.locator(`[data-person-panel-trigger="${people.alex}"][id*="board-card"]`).count(), 1, 'Overlay has passive person copy')
    await page.keyboard.press('Escape'); await frames()
    assert.equal(await handle.evaluate(node => node === document.activeElement), true, 'Drag cancellation retains the natural handle focus')
    assert.deepEqual(writes, []); cases.push('board-natural-keyboard-drag-cancel-passive-overlay')

    me.capabilities.workload = false
    const beforeNoWorkload = gridReads.length
    await page.goto(taskUrl)
    await roundTrip(trigger(`task:${work.id}:assignee:detail`, people.alex), people.alex)
    assert.equal(gridReads.length, beforeNoWorkload, 'No-Workload viewer does not issue Person capacity reads')
    me.capabilities.workload = true
    cases.push('no-workload-person-facts-without-capacity-query')

    // Phone dual DOM columns and dark flag: names remain, no new Person reads.
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(`${base}/tasks?projects=${project.id}&status=Not+Started&group=none`)
    await uniquePersonIds()
    await roundTrip(trigger(`workspace-task:${work.id}:assignee:card`, people.alex), people.alex)
    me.flags.people_screens = false
    const beforeDark = panelReads.length
    await page.reload(); await page.getByRole('button', { name: work.name, exact: true }).first().waitFor()
    assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0)
    assert.equal(panelReads.length, beforeDark)
    await page.getByText(names.get(people.alex), { exact: true }).first().waitFor()
    cases.push('phone-unique-ids-flag-off-no-person-read')

    me.flags.work_screens = false
    await page.goto(`${riskUrl}&recordTab=history&recordTabItem=${encodeURIComponent(`Risk:${risk.id}`)}`)
    await page.getByRole('tab', { name: 'History', exact: true }).waitFor()
    assert.equal(await page.getByRole('tab', { name: 'History', exact: true }).getAttribute('aria-selected'), 'false', 'Both flags off retain old local default rather than typed URL behavior')
    assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0)
    cases.push('both-flags-off-local-tab-behavior')

    assert.deepEqual(writes, [], 'Every unexpected business method was refused, never hidden by a200 fallback')
    assert.deepEqual(unexpectedGets, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(errors, [])
    console.log(JSON.stringify({ verdict: 'PASS_SYNTHETIC_BROWSER', cases, businessWrites: writes.length, personalVisitCommands: personalVisits.length, explicitPreviewReads: previews.length,
      personReads: panelReads.length, gridReads: gridReads.length, source: 'coordinator-built local preview; not API authority or global S43 acceptance' }))
  } finally {
    me.flags = savedFlags; me.capabilities = savedCapabilities; project.permissions = savedProjectPermissions
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
