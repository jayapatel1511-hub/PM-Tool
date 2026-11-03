// S22 + Dashboard-only 052 S06. Synthetic mocked API; real-reader math has API coverage.
const assert = require('node:assert/strict')
const { newMockPage, me, project, task, taskDetail, panelIds } = require('./platform-fixtures.cjs')
const { dashboardView, dashboardCount } = require('./dashboard.fixture.cjs')
const { attentionRow, attentionList, attentionSnoozed } = require('./evaluation.fixture.cjs')
const { submissionListRow, submissionPage } = require('./submissions.fixture.cjs')

const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the local UI harness')
const civil = project.disciplines[0].id
const packageId = '00000000-0000-0000-0000-000000000450'
const packageRow = { id: packageId, projectId: project.id, key: 'P-MOCK-SUB450', title: 'Synthetic actual submission target', purpose: 'Synthetic permit review',
  recipientReference: 'Synthetic authority', coordinatorId: me.id, milestoneId: panelIds.milestone, targetDate: '2026-11-27', status: 'Checking', manifestVersion: 1, rowVersion: 1 }
const currentContext = { owner: { id: me.id, name: me.displayName, state: 'Active', kind: 'User' }, nextActors: [], neededBy: '2026-11-27', nextAction: 'continue', terminal: false, actorState: 'Active', requiresReviewGate: false, nextTransitions: [], inputs: [], available: true }
const unavailableContext = { owner: { id: null, name: null, state: 'Unknown', kind: 'User' }, nextActors: [], neededBy: null, nextAction: 'not_assessed', terminal: false, actorState: 'Unknown', requiresReviewGate: false, nextTransitions: [], inputs: [], available: false }
const kinds = ['Task', 'Issue', 'Task', 'Deliverable', 'Risk', 'Task', 'Issue', 'Task', 'Deliverable', 'Task', 'Risk', 'Task']
const ranked = () => kinds.map((kind, index) => attentionRow({ id: `00000000-0000-0000-0000-${String(400 + index).padStart(12, '0')}`,
  projectId: project.id, projectNumber: project.projectNumber, projectDisciplineId: civil,
  itemId: kind === 'Task' ? task.id : panelIds[kind.toLowerCase()], itemType: kind,
  itemKey: kind === 'Task' ? 'T-001' : `${kind[0]}-${index}`, itemName: `Synthetic ${kind} firing ${index + 1}`,
  ruleId: `A-${String(index + 1).padStart(2, '0')}`, ruleName: `Synthetic rule ${index + 1}`, severity: 'Warning',
  context: kind === 'Task' ? currentContext : unavailableContext,
}))
const staffingRow = () => ({ projectDisciplineId: civil, name: 'Civil', unattributed: false,
  remainingHours: 120, knownRemainingHours: 120, unestimatedTasks: 0, undatedTasks: 0,
  confirmedHours: 80, knownConfirmedHours: 80, unavailableConfirmed: 0,
  openRoleHours: 24, knownOpenRoleHours: 24, unavailableOpenRoles: 0, excessHours: 40, comparisonState: 'Complete' })
const staffingView = () => ({ fromDate: '2026-11-16', throughDate: '2026-11-27', state: 'Complete',
  target: { kind: 'Milestone', id: panelIds.milestone, key: 'M-001', name: 'Synthetic milestone', date: '2026-11-27' },
  allocationCoverage: 'Complete', unattributedConfirmedHours: 0, knownUnattributedConfirmedHours: 0, unavailableUnattributed: 0,
  disciplines: [staffingRow()], asOf: '2026-11-16T13:00:00Z' })
const countData = () => ({
  tasks: { total: dashboardCount(31, `disciplineId=${civil}`), complete: dashboardCount(9, `status=Complete&disciplineId=${civil}`), inProgress: dashboardCount(7, `status=In%20Progress&disciplineId=${civil}`),
    review: dashboardCount(6, `readyForReview=true&disciplineId=${civil}`), overdue: dashboardCount(5, `overdue=true&disciplineId=${civil}`), blocked: dashboardCount(4, `blocked=true&disciplineId=${civil}`),
    waiting: dashboardCount(3, `waiting=true&disciplineId=${civil}`), unassigned: dashboardCount(2, `unassigned=true&disciplineId=${civil}`), backlog: dashboardCount(1, `backlog=true&disciplineId=${civil}`) },
  deliverables: { upcoming: dashboardCount(8, `dueFrom=2026-11-16&dueTo=2026-11-30&indicator=open&disciplineId=${civil}`), atRisk: dashboardCount(3, `indicator=atRisk&disciplineId=${civil}`), issued: dashboardCount(2, `status=Issued%2CAccepted&disciplineId=${civil}`), total: dashboardCount(13, `disciplineId=${civil}`) },
  decisions: { pending: dashboardCount(4, 'status=Pending%2CUnder%20Review%2CDeferred'), overdue: dashboardCount(2, 'indicator=overdue') },
  registers: { issuesOpen: dashboardCount(3, 'indicator=open'), issuesHigh: dashboardCount(2, 'indicator=open&severity=High'), risksHigh: dashboardCount(1, 'indicator=open&severity=High') },
})

;(async () => {
  const ui = await newMockPage(base), { page, mock } = ui
  const errors = [], attentionReads = [], commands = [], packageReads = []
  let rows = ranked(), hidden = [], truncated = false, staffing = staffingView(), releaseDashboard, dashboardBlocked, blockDashboard = false
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', e => errors.push(e)); page.on('console', m => { if (m.type() === 'error') errors.push(new Error(m.text())) })
    me.flags = { ...me.flags, visual_system: true, project_screens: true, screen_patterns: false, live_events: false }
    project.permissions.snooze = { ok: true, reason: null }; project.permissions.isPm = true
    await page.route(`**/api/v1/projects/${project.id}/dashboard*`, async route => {
      if (blockDashboard) await new Promise(resolve => { releaseDashboard = resolve; dashboardBlocked() })
      const result = dashboardView({ ...countData(), ...(me.flags.visual_system || me.flags.project_screens ? { asOf: staffing.asOf } : {}), ...(me.flags.project_screens ? { staffing } : {}) })
      await route.fulfill({ json: result })
    })
    await page.route(`**/api/v1/projects/${project.id}/attention*`, async route => {
      const url = new URL(route.request().url()); attentionReads.push(url)
      const items = url.searchParams.get('includeSnoozed') === 'true' ? [...rows, ...hidden] : rows
      await route.fulfill({ json: { ...attentionList(items, hidden.length), truncated, ...(truncated ? { total: 99 } : {}) } })
    })
    await page.route('**/api/v1/attention/*/snooze', async route => {
      const id = new URL(route.request().url()).pathname.split('/').at(-2)
      commands.push({ id, method: route.request().method(), body: route.request().postDataJSON() })
      const row = rows.find(item => item.id === id); assert.ok(row)
      // This refetch also simulates the other Issue resolving, so its group disappears.
      rows = rows.filter(item => item.itemType !== 'Issue'); hidden = [{ ...row, snoozed: true, snoozedUntil: '2026-11-23T13:00:00Z', snoozeNote: 'Synthetic review note' }]
      await route.fulfill({ json: attentionSnoozed(id, hidden[0].snoozedUntil) })
    })
    await page.route(`**/api/v1/tasks/${task.id}`, route => route.fulfill({ json: { ...taskDetail, context: currentContext } }))
    await page.route(`**/api/v1/projects/${project.id}/submissions**`, route => {
      const path = new URL(route.request().url()).pathname
      if (path === `/api/v1/projects/${project.id}/submissions/${packageId}`) {
        packageReads.push(path)
        return route.fulfill({ json: { package: packageRow, checks: [], manifest: [], evidence: [], issue: null, readiness: null,
          effectiveStatus: 'Checking', canAuthorise: false, canCoordinate: false } })
      }
      assert.equal(path, `/api/v1/projects/${project.id}/submissions`)
      return route.fulfill({ json: submissionPage([submissionListRow({ ...packageRow, blockerCount: 0 })]) })
    })

    const url = `${base}/projects/${project.projectNumber}/dashboard?discipline=${civil}&severity=Warning&review=dashboard`
    const attention = page.locator('#attention'), group = attention.getByRole('group', { name: 'Attention by kind' })
    const staffingSection = page.locator('#dashboard-staffing'), facts = staffingSection.locator(`tr[data-dashboard-staffing="${civil}"]`)
    const cards = page.locator('[data-dashboard-counts] [data-count-card]')
    const waitRows = async count => {
      if (count > 0) await attention.locator('li').nth(count - 1).waitFor({ state: 'visible' })
      await page.waitForFunction(expected => document.querySelectorAll('#attention li').length === expected, count)
    }
    const settleAttention = async (kind, label, count, all = false) => {
      await page.waitForURL(value => value.searchParams.get('attentionKind') === kind
        && (all ? value.searchParams.get('attentionAll') === 'true' : !value.searchParams.has('attentionAll')))
      await group.getByRole('button', { name: label, exact: true, pressed: true }).waitFor()
      await waitRows(count)
    }
    await page.goto(url)
    await group.getByRole('button', { name: 'Task 6', exact: true }).waitFor()
    await settleAttention('Task', 'Task 6', 5)
    for (const width of [1440, 375]) {
      await page.setViewportSize({ width, height: 1000 })
      const geometry = await page.evaluate(async () => {
        await document.fonts.ready
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))
        const band = document.querySelector('.visual-page--project > .visual-page-band')
        const frame = band.parentElement, content = frame.querySelector(':scope > .visual-page-content')
        const animations = [frame, band, content].flatMap(node => node.getAnimations()).filter(animation => animation.playState === 'running' || animation.pending)
        if (animations.length) await Promise.race([
          Promise.allSettled(animations.map(animation => animation.finished)),
          new Promise((_, reject) => setTimeout(() => reject(new Error('Dashboard layout transitions did not settle')), 2000)),
        ])
        await new Promise(resolve => requestAnimationFrame(resolve))
        // The band reserves empty space for overlap; its full bottom is not the content boundary.
        const facts = [...band.querySelectorAll('h1,h2,.visual-title-block,.visual-page-actions > *,.visual-page-band-tabs,.visual-page-band-footer')]
          .map(node => { const rect = node.getBoundingClientRect(); return { text: node.textContent.slice(0, 100), bottom: rect.bottom, width: rect.width, height: rect.height } })
          .filter(rect => rect.width > 0 && rect.height > 0)
        return { bandBottom: band.getBoundingClientRect().bottom, facts,
          firstCardTop: document.querySelector('[data-dashboard-counts] [data-count-card]').getBoundingClientRect().top }
      })
      assert.ok(geometry.facts.length > 0, 'Measure actual Dashboard title/reference/control content')
      assert.ok(geometry.facts.every(fact => geometry.firstCardTop >= fact.bottom - 1), `Dashboard cards must clear the title/reference/controls at ${width}px: ${JSON.stringify(geometry)}`)
    }
    await page.setViewportSize({ width: 1440, height: 1000 })
    assert.equal(await group.getByRole('button').count(), 4)
    assert.deepEqual(await group.getByRole('button').allTextContents(), ['Task 6', 'Issue 2', 'Deliverable 2', 'Risk 2'])
    assert.equal(await attention.locator('li').count(), 5, 'first five Task rule firings, not the first five records or total response')
    assert.equal(new URL(page.url()).searchParams.get('attentionKind'), 'Task')
    assert.equal(await attention.locator('section[aria-label="Record context"]').count(), 0)
    const query = attentionReads.at(-1).searchParams
    assert.equal(query.get('disciplineId'), civil); assert.equal(query.get('severity'), 'Warning')
    await attention.getByRole('button', { name: 'Show all 6', exact: true }).click()
    await settleAttention('Task', 'Task 6', 6, true)
    assert.equal(await attention.locator('li').count(), 6, 'all six firings remain even though they share the same Task ID')
    assert.equal(new URL(page.url()).searchParams.get('attentionAll'), 'true')
    await group.getByRole('button', { name: 'Issue 2', exact: true }).click()
    await settleAttention('Issue', 'Issue 2', 2)
    assert.equal(await attention.locator('li').count(), 2); assert.equal(new URL(page.url()).searchParams.has('attentionAll'), false)
    await group.getByRole('button', { name: 'Task 6', exact: true }).click()
    await settleAttention('Task', 'Task 6', 5)
    assert.equal(await attention.locator('li').count(), 5)
    await attention.getByRole('button', { name: 'Show all 6', exact: true }).click()
    await settleAttention('Task', 'Task 6', 6, true)
    const origin = page.url(), source = attention.getByRole('button', { name: /^T-001 Synthetic Task firing 1$/ })
    await page.setViewportSize({ width: 1280, height: 700 }); await source.scrollIntoViewIfNeeded()
    const scroll = await page.locator('main#content').evaluate(el => el.scrollTop)
    assert.ok(scroll > 0)
    await source.click(); await page.getByRole('dialog').getByRole('heading').first().waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(origin); await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, scroll)
    await settleAttention('Task', 'Task 6', 6, true)
    assert.equal(await attention.locator('li').count(), 6, 'kind/all survives source return even with context flag off')
    assert.equal(new URL(page.url()).searchParams.get('review'), 'dashboard')
    await page.setViewportSize({ width: 1440, height: 1000 })

    assert.equal(await cards.count(), 6)
    const counts = countData(), cardFacts = [
      ['Tasks in progress', 7, 'tasks', counts.tasks.inProgress, 'violet', 'Civil'],
      ['Tasks for review', 6, 'tasks', counts.tasks.review, 'amber', 'Civil'],
      ['Overdue tasks', 5, 'tasks', counts.tasks.overdue, 'red', 'Civil'],
      ['Deliverables due within 14 days', 8, 'deliverables', counts.deliverables.upcoming, 'amber', 'Civil'],
      ['Pending decisions', 4, 'decisions', counts.decisions.pending, 'violet', 'whole project'],
      ['Open issues', 3, 'issues', counts.registers.issuesOpen, 'violet', 'whole project'],
    ]
    for (const [label, value, path, count, tone, where] of cardFacts) {
      const card = cards.filter({ hasText: label }); assert.equal(await card.count(), 1)
      assert.equal(await card.getAttribute('href'), `/projects/${project.projectNumber}/${path}?${count.link}`)
      assert.equal(await card.locator('.count-card__value').innerText(), String(value))
      assert.equal(await card.locator('.count-card__where').innerText(), `Project P-MOCK · ${where}`)
      assert.match(await card.getAttribute('class'), new RegExp(`count-card--${tone}`))
      assert.equal(await card.locator('a,button').count(), 0, 'whole card has no nested anchors or buttons')
    }
    const firstCard = cards.filter({ hasText: 'Tasks in progress' }); await firstCard.focus()
    await page.getByRole('tooltip').filter({ hasText: 'Tasks with In Progress status' }).waitFor(); await firstCard.blur()
    const siblings = page.locator('[aria-label="Other project counts"]')
    assert.equal(await siblings.locator('a').count(), 12, 'every remaining count stays a sibling destination')
    for (const [path, entries] of [['tasks', [counts.tasks.total, counts.tasks.complete, counts.tasks.blocked, counts.tasks.waiting, counts.tasks.unassigned, counts.tasks.backlog]], ['deliverables', [counts.deliverables.total, counts.deliverables.atRisk, counts.deliverables.issued]], ['decisions', [counts.decisions.overdue]], ['issues', [counts.registers.issuesHigh]], ['risks', [counts.registers.risksHigh]]]) {
      for (const count of entries) assert.equal(await siblings.locator(`a[href="/projects/${project.projectNumber}/${path}?${count.link}"]`).count(), 1)
    }
    assert.deepEqual(await facts.locator('td').allTextContents(), ['120 h', '80 h', '24 h', 'Remaining exceeds allocated by 40 h'])
    await staffingSection.getByText('Next milestone fallback:', { exact: true }).waitFor()
    await staffingSection.getByText(/On Hold tasks are open; Complete and Cancelled are excluded/).waitFor()
    assert.doesNotMatch(await staffingSection.innerText(), /224 h|144 h/)

    // The primary package basis must use its real submissions route, independently of context.
    assert.equal(me.flags.screen_patterns, false)
    staffing = { ...staffingView(), target: { kind: 'SubmissionPackage', id: packageId, key: packageRow.key, name: packageRow.title, date: packageRow.targetDate } }
    await page.reload()
    const packageLink = staffingSection.getByRole('link', { name: `${packageRow.key} ${packageRow.title}`, exact: true })
    await packageLink.waitFor()
    assert.equal(await packageLink.getAttribute('href'), `/projects/${project.projectNumber}/submissions?panel=SubmissionPackage:${packageId}`)
    await staffingSection.getByText('Next open submission package:', { exact: true }).waitFor()
    await page.setViewportSize({ width: 1280, height: 700 }); await packageLink.scrollIntoViewIfNeeded()
    const packageOrigin = page.url(), packageScroll = await page.locator('main#content').evaluate(el => el.scrollTop)
    assert.ok(packageScroll > 0, 'the actual package source has a real Dashboard scroll position')
    await packageLink.click()
    await page.waitForURL(value => value.pathname.endsWith('/submissions') && value.searchParams.get('panel') === `SubmissionPackage:${packageId}`)
    await page.getByRole('dialog').getByRole('heading', { name: `${packageRow.key} · ${packageRow.title}`, exact: true }).waitFor()
    assert.deepEqual(packageReads, [`/api/v1/projects/${project.id}/submissions/${packageId}`])
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click()
    await page.waitForURL(packageOrigin)
    await page.waitForFunction(expected => document.querySelector('main#content')?.scrollTop === expected, packageScroll)
    await settleAttention('Task', 'Task 6', 6, true)
    assert.equal(new URL(page.url()).searchParams.get('attentionKind'), 'Task'); assert.equal(new URL(page.url()).searchParams.get('attentionAll'), 'true')
    assert.equal(new URL(page.url()).searchParams.get('discipline'), civil); assert.equal(new URL(page.url()).searchParams.get('severity'), 'Warning')
    assert.equal(new URL(page.url()).searchParams.get('review'), 'dashboard')
    await packageLink.waitFor(); await page.setViewportSize({ width: 1440, height: 1000 })
    staffing = staffingView(); await page.reload()
    const milestoneSource = staffingSection.getByRole('button', { name: 'M-001 Synthetic milestone', exact: true })
    await milestoneSource.waitFor(); const milestoneOrigin = page.url()
    await milestoneSource.click(); await page.getByRole('dialog').getByRole('heading').first().waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click(); await page.waitForURL(milestoneOrigin)
    await settleAttention('Task', 'Task 6', 6, true)

    await group.getByRole('button', { name: 'Issue 2', exact: true }).click()
    await settleAttention('Issue', 'Issue 2', 2)
    await attention.getByRole('button', { name: /^Snooze/ }).first().click()
    await page.getByRole('dialog').getByLabel('Reason', { exact: true }).fill('Synthetic review note')
    await page.getByRole('dialog').getByRole('button', { name: 'Snooze', exact: true }).click()
    await group.getByRole('button', { name: 'Task 6', exact: true }).waitFor()
    await page.waitForURL(value => value.searchParams.get('attentionKind') === 'Task')
    await settleAttention('Task', 'Task 6', 5)
    assert.equal(new URL(page.url()).searchParams.has('attentionAll'), false)
    assert.deepEqual(commands, [{ id: '00000000-0000-0000-0000-000000000401', method: 'POST', body: { days: 7, note: 'Synthetic review note' } }])
    await attention.getByRole('button', { name: 'Show 1 snoozed', exact: true }).click()
    await group.getByRole('button', { name: 'Issue 1', exact: true }).waitFor()
    await page.waitForURL(value => value.searchParams.get('attentionSnoozed') === 'true')
    await settleAttention('Task', 'Task 6', 5)
    assert.equal(attentionReads.at(-1).searchParams.get('includeSnoozed'), 'true')
    assert.equal(new URL(page.url()).searchParams.get('attentionSnoozed'), 'true')
    assert.equal(new URL(page.url()).searchParams.get('severity'), 'Warning')
    await attention.getByRole('button', { name: 'Hide snoozed', exact: true }).click()
    await page.waitForURL(value => !value.searchParams.has('attentionSnoozed'))
    await group.getByRole('button', { name: 'Issue 1', exact: true }).waitFor({ state: 'hidden' })
    await settleAttention('Task', 'Task 6', 5)

    staffing = { ...staffingView(), state: 'Partial', allocationCoverage: 'VisibleOnly', unattributedConfirmedHours: null, knownUnattributedConfirmedHours: 7,
      disciplines: [{ ...staffingRow(), remainingHours: null, knownRemainingHours: 90, unestimatedTasks: 1, undatedTasks: 2, confirmedHours: null, knownConfirmedHours: 16,
        openRoleHours: null, knownOpenRoleHours: 12, unavailableOpenRoles: 1, excessHours: null, comparisonState: 'Unavailable' }] }
    await page.reload(); await facts.getByText('Known subtotal: 90 h', { exact: true }).waitFor()
    await facts.getByText('Known subtotal: 16 h', { exact: true }).waitFor(); await facts.getByText('Known subtotal: 12 h', { exact: true }).waitFor()
    await facts.getByText('Tasks without estimates: 1', { exact: true }).waitFor(); await facts.getByText('Open tasks without due dates: 2', { exact: true }).waitFor()
    await facts.getByText('Open roles with unavailable spreads: 1', { exact: true }).waitFor(); await facts.getByText('Comparison unavailable', { exact: true }).waitFor()
    await staffingSection.getByText(/Confirmed figures cover only allocations you may view/).waitFor()
    assert.doesNotMatch(await staffingSection.innerText(), /does not exceed|exceeds allocated by/)
    blockDashboard = true
    const pendingRead = new Promise(resolve => { dashboardBlocked = resolve })
    await page.getByLabel('Discipline scope', { exact: true }).selectOption(''); await pendingRead
    assert.equal(await page.getByText('Known subtotal: 90 h', { exact: true }).count(), 0, 'old staffing figures are cleared while the next scope is loading')
    blockDashboard = false; releaseDashboard(); await facts.getByText('Known subtotal: 90 h', { exact: true }).waitFor()

    staffing = { ...staffingView(), state: 'OverdueTarget', throughDate: '2026-11-15', disciplines: [], allocationCoverage: 'Unavailable' }
    await page.reload(); await staffingSection.getByText(/This target is overdue/).waitFor()
    assert.equal(await staffingSection.locator('table').count(), 0); assert.doesNotMatch(await staffingSection.innerText(), /Inclusive window/)
    staffing = { ...staffingView(), state: 'NoTarget', target: null, throughDate: null, disciplines: [], allocationCoverage: 'Unavailable' }
    await page.reload(); await staffingSection.getByText('No dated open submission package or milestone', { exact: true }).waitFor()
    assert.equal(await staffingSection.locator('table').count(), 0)

    staffing = staffingView(); rows = ranked().filter(row => row.itemType === 'Task'); hidden = []; truncated = true
    await page.goto(url); await group.getByRole('button', { name: 'Task At least 6', exact: true }).waitFor()
    await settleAttention('Task', 'Task At least 6', 5)
    await attention.getByText(/Kind counts are lower bounds/).waitFor()
    await attention.getByRole('button', { name: 'Show loaded (6)', exact: true }).click()
    await settleAttention('Task', 'Task At least 6', 6, true); assert.equal(await attention.locator('li').count(), 6)
    assert.equal(await attention.getByRole('button', { name: 'Show all 99', exact: true }).count(), 0)
    truncated = false; rows = ranked(); me.flags.visual_system = false; me.flags.screen_patterns = true
    await page.goto(url); await group.getByRole('button', { name: 'Task 6', exact: true }).waitFor()
    await settleAttention('Task', 'Task 6', 5)
    assert.equal(await cards.count(), 0, 'project_screens does not enable visual-system cards')
    await attention.locator('section[aria-label="Record context"]').first().getByText('You', { exact: true }).waitFor()
    await group.getByRole('button', { name: 'Issue 2', exact: true }).click()
    await settleAttention('Issue', 'Issue 2', 2)
    await attention.getByText('Current record facts unavailable; next step not assessed.', { exact: true }).first().waitFor()

    rows = ranked().slice(0, 2).map((row, index) => ({ ...row, itemType: `FutureKind${index}`, context: undefined }))
    await page.goto(url); await group.getByRole('button', { name: 'Other kinds 2', exact: true }).waitFor()
    await settleAttention('__other', 'Other kinds 2', 2)
    assert.equal(await group.getByRole('button').count(), 1); assert.doesNotMatch(await group.innerText(), /FutureKind/)
    await attention.getByText('Current record facts unavailable; next step not assessed.', { exact: true }).first().waitFor()
    rows = []; await page.goto(`${url}&attentionKind=Task&attentionAll=true`)
    await page.waitForURL(value => !value.searchParams.has('attentionKind') && !value.searchParams.has('attentionAll'))
    await waitRows(0)
    assert.equal(await attention.locator('li').count(), 0)

    rows = ranked(); me.flags.visual_system = true; me.flags.project_screens = false; me.flags.screen_patterns = false
    await page.goto(url); await cards.first().waitFor()
    await attention.getByRole('button', { name: 'Show all 12', exact: true }).waitFor(); await waitRows(10)
    assert.equal(await cards.count(), 6); assert.equal(await group.count(), 0); assert.equal(await staffingSection.count(), 0)
    assert.equal(await attention.locator('li').count(), 10, 'optional grouping off retains the legacy ten-then-all behavior')
    await attention.getByRole('button', { name: 'Show all 12', exact: true }).click(); await waitRows(12); assert.equal(await attention.locator('li').count(), 12)
    me.flags.visual_system = false; await page.goto(url); await attention.getByRole('button', { name: 'Show all 12', exact: true }).waitFor()
    assert.equal(await cards.count(), 0); assert.equal(await group.count(), 0); assert.equal(await staffingSection.count(), 0)
    assert.equal(commands.length, 1, 'only the explicit snooze was a write')
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, []); assert.deepEqual(errors, [], errors.map(e => e.stack ?? e.message).join('\n'))
    console.log('PASS: Dashboard separate staffing facts/unknowns/targets, grouped ranked rule firings and retained controls/source/snooze, six exact count-card destinations and independent flags')
  } catch (error) { console.error(await page.locator('body').innerText(), mock.unknownGets, errors); throw error }
  finally { if (releaseDashboard) releaseDashboard(); await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
