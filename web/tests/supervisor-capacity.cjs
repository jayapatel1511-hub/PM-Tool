// Supervisor capacity signals and digest preference checks with mocked API responses.
// The API rules and digest job are covered separately by server tests; this keeps the browser contract small.
const { chromium } = require('playwright')
const path = require('path')
const { spawn } = require('child_process')
const assert = require('node:assert/strict')

const port = process.env.SUPERVISOR_CAPACITY_TEST_PORT ?? '5178'
const base = `http://127.0.0.1:${port}`
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const supervisor = id(1), free = id(2), partial = id(3), missing = id(4)
const monday = '2026-12-28'
const staffRow = (personId, displayName, extra = {}) => ({
  id: personId, displayName, jobTitle: 'Engineer', officeId: null, isActive: true, projects: 1,
  roles: { pm: 0, dl: 0, team: 1, reviewer: 0, viewer: 0 }, open: 2, overdue: 0, blocked: 0, blocking: 0,
  reviews: 0, reviewsStalled: 0, deliverablesDue: 0, lastActivityAt: null, ...extra,
})
const initialPeople = [
  staffRow(free, 'Free Engineer', { overdue: 1, blocked: 1, reviewsStalled: 1 }),
  staffRow(partial, 'Partial Engineer'),
  staffRow(missing, 'Out of Scope Engineer'),
]
const cell = (personId, extra = {}) => ({ week: monday, capacity: 40, timeAway: 8, additionalAvailability: 0, approved: 0,
  confirmed: 20, expected: 0, possible: 0, remaining: 12, taskEstimates: 0, unestimatedTasks: 0,
  overPlanned: false, underPlanned: false, staleEntries: 0, ownDraftHours: 0, ...extra, personId })
const me = {
  id: supervisor, displayName: 'Sam Supervisor', email: 'sam@hub.test', roles: [], systemRoles: ['Supervisor'],
  capabilities: { createProject: false, createTask: true, portfolio: false, workload: true, staff: true, allStaff: true, admin: false, templates: false, readOnly: false, directReports: 3 },
  settings: { today: '2027-01-03', dateFormat: 'yyyy-MM-dd', orgTimeZone: 'America/Halifax', idleTimeoutHours: 8 },
  preferences: { denseRows: true, digestEnabled: true },
}
const ref = { disciplines: [], clients: [], offices: [], deliverableTypes: [], phases: [], projectTypes: [] }
const emptyStaff = people => ({ scope: 'direct', canSeeAll: true, tiles: { staff: people.length, assignments: people.length, withOverdue: 0, withBlocked: 0, reviewsStalled: 0 }, people })
const grid = people => ({ weeks: [monday], currentWeek: monday, evaluatedAt: '2027-01-03T00:00:00Z', partialView: false, draftAccess: false,
  settings: { horizonWeeks: 12, overPct: 105, underPct: 50, underWeeks: 2, staleDays: 28, maxHoursPerWeek: 80 }, people })
const prefs = holdDigestWhenAway => ({
  events: [], digest: { digestEnabled: true, holdDigestWhenAway, time: '08:00', orgTime: '08:00', weekendDigests: false,
    sections: [], sectionsOff: [], weeklySummaryEnabled: false, managesProjects: false }, follows: [],
})

;(async () => {
  const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', port, '--strictPort'], { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' })
  let browser
  const errors = [], unknown = [], gridRequests = [], preferenceBodies = []
  let people = initialPeople
  let workload = true
  let digestHold = true
  const personalNotification = { id: id(80), eventType: 'TaskAssigned', title: 'Review the harbour task', body: 'A task needs your attention', count: 1, createdAt: '2027-01-03T09:00:00Z', updatedAt: '2027-01-03T09:00:00Z', readAt: null }
  const feedItem = (n) => ({ entry: { id: id(90 + n), occurredAt: '2027-01-03T09:00:00Z', actorId: supervisor, actorName: 'Sam Supervisor', actorType: 'User', projectId: id(70), itemType: 'Task', itemId: id(71), itemKey: `P-DEMO-T${String(n).padStart(4, '0')}`, itemName: n === 201 ? 'Beyond first page' : `Feed item ${n}`, action: 'Updated', categories: [], changes: [], summary: '', source: 'Activity', ...(n === 1 || n === 201 ? { correlationId: 'corr-1' } : {}) }, count: 1, projectNumber: 'P-DEMO', projectName: 'Synthetic followed project', notified: false, unread: true })
  try {
    for (let i = 0; i < 60; i++) { try { if ((await fetch(base)).ok) break } catch {} await new Promise(r => setTimeout(r, 100)) }
    browser = await chromium.launch({ headless: true, ...(process.env.CHROME_EXECUTABLE_PATH ? { executablePath: process.env.CHROME_EXECUTABLE_PATH } : {}) })
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' })
    await page.addInitScript(() => { sessionStorage.setItem('hub.devUser', 'sam@hub.test'); sessionStorage.setItem('hub.signedIn', '1') })
    page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request()
      const url = new URL(request.url())
      const endpoint = url.pathname.replace('/api/v1/', '')
      let status = 200, data = {}
      if (endpoint === 'config') data = { authMode: 'Development', entra: {} }
      else if (endpoint === 'me') data = { ...me, capabilities: { ...me.capabilities, workload } }
      else if (endpoint === 'me/sign-in') data = {}
      else if (endpoint === 'me/notifications/pulse') data = { stamp: '0' }
      else if (endpoint === 'me/notifications/unread-count') data = { notifications: 1, following: 1 }
      else if (endpoint === 'workspaces') data = []
      else if (endpoint === 'views') data = { views: [], canShare: false }
      else if (endpoint === 'reference') data = ref
      else if (endpoint === 'staff') data = emptyStaff(people)
      else if (endpoint === 'me/notifications' && request.method() === 'GET') data = { items: [personalNotification], page: 1, pageSize: 50, totalCount: 1 }
      else if (endpoint === 'me/notifications/read' && request.method() === 'POST') { data = {}; assert.deepEqual(request.postDataJSON(), { ids: [personalNotification.id] }) }
      else if (endpoint === 'me/following' && request.method() === 'GET') {
        const pageNumber = Number(url.searchParams.get('page') ?? '1')
        data = { projects: [{ projectId: id(70), projectNumber: 'P-DEMO', name: 'Synthetic followed project', status: 'Active', unread: 1 }], items: pageNumber === 1 ? [feedItem(1), feedItem(2)] : [feedItem(201)], totalCount: 201, page: pageNumber, pageSize: 200 }
      }
      else if (endpoint === 'me/preferences' && request.method() === 'GET') data = prefs(digestHold)
      else if (endpoint === 'me/preferences/digest' && request.method() === 'PUT') { preferenceBodies.push(request.postDataJSON()); digestHold = preferenceBodies.at(-1).holdDigestWhenAway; data = {} }
      else if (endpoint === 'planning/grid') {
        gridRequests.push({ personIds: url.searchParams.get('personIds')?.split(',') ?? [], from: url.searchParams.get('from'), weeks: url.searchParams.get('weeks') })
        const ids = url.searchParams.get('personIds')?.split(',') ?? []
        data = grid(ids.filter(id_ => id_ !== missing).map(id_ => id_ === free
          ? { id: free, displayName: 'Free Engineer', partial: false, cells: [cell(free, { overPlanned: true, staleEntries: 2 })] }
          : { id: partial, displayName: 'Partial Engineer', partial: true, cells: [cell(partial, { remaining: 99 })] }))
      } else if (endpoint.startsWith('staff/')) data = []
      else { unknown.push(endpoint); data = {} }
      await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) })
    })

    // Sunday at the ISO year boundary still asks for the preceding Monday.
    await page.goto(`${base}/staff`)
    await page.getByRole('columnheader', { name: 'This week' }).waitFor()
    assert.deepEqual(gridRequests[0], { personIds: [free, partial, missing], from: monday, weeks: '1' })
    const freeRow = page.getByRole('row').filter({ hasText: 'Free Engineer' }).first()
    assert.match(await freeRow.innerText(), /12 h free · 8 h away/)
    assert.equal(await freeRow.locator('[data-signals]').getAttribute('data-signals'), '5')
    const signalSummary = await freeRow.locator('[data-signals] button').getAttribute('aria-label')
    assert.match(signalSummary, /Over-planned/); assert.match(signalSummary, /Stale plan 2/); assert.match(signalSummary, /Dec 28/)
    const partialRow = page.getByRole('row').filter({ hasText: 'Partial Engineer' }).first()
    const partialFigure = partialRow.locator('[data-capacity]')
    assert.match(await partialFigure.innerText(), /^Partial view/)
    assert.ok(await partialFigure.getAttribute('aria-describedby'))
    assert.equal(await partialRow.locator('[data-capacity] span.sr-only').count(), 1)
    const missingRow = page.getByRole('row').filter({ hasText: 'Out of Scope Engineer' }).first()
    assert.equal(await missingRow.locator('[data-capacity]').count(), 0)

    // The shared hook batches 120 people into three requests, each within the server's 50-person limit.
    people = Array.from({ length: 120 }, (_, i) => staffRow(id(100 + i), `Person ${i + 1}`))
    gridRequests.length = 0
    await page.reload(); await page.getByRole('columnheader', { name: 'This week' }).waitFor()
    assert.equal(gridRequests.length, 3); assert.ok(gridRequests.every(request => request.personIds.length <= 50))
    assert.ok(gridRequests.every(request => request.from === monday && request.weeks === '1'))

    // Workload denial removes both the column and the capacity request.
    workload = false; gridRequests.length = 0; people = initialPeople
    await page.reload(); await page.getByRole('heading', { name: 'My Staff' }).waitFor()
    assert.equal(await page.getByRole('columnheader', { name: 'This week' }).count(), 0)
    assert.equal(gridRequests.length, 0)

    // Personal mark-read sends only the selected id and keeps the centre URL stable.
    await page.goto(`${base}/notifications`)
    await page.getByText(personalNotification.title, { exact: true }).waitFor()
    const personalUrl = await page.url()
    await page.getByRole('button', { name: `Mark read: ${personalNotification.title}` }).click()
    await page.waitForTimeout(50)
    assert.equal(await page.url(), personalUrl)

    // Following's 200-item pages load the second page and expose an item beyond page one.
    await page.goto(`${base}/notifications?tab=following`)
    await page.getByText('Feed item 1').waitFor()
    const followingMore = page.getByRole('button', { name: 'Show more' })
    const followingPage2 = page.waitForResponse(response => response.url().includes('/api/v1/me/following') && new URL(response.url()).searchParams.get('page') === '2')
    await followingMore.click(); await followingPage2
    await page.getByText('Feed item 1').waitFor()
    await page.getByText('Feed item 2').waitFor()
    await page.getByText('2 changes', { exact: true }).waitFor()
    assert.equal(await page.getByText('Beyond first page', { exact: true }).count(), 0)

    // Phone cards remain inside the viewport at both supported narrow widths.
    workload = true; await page.goto(`${base}/staff`)
    for (const width of [390, 320, 768, 1024]) {
      await page.setViewportSize({ width, height: 740 }); await page.reload()
      if (width < 768) await page.getByRole('list', { name: 'My Staff' }).waitFor()
      else await page.getByRole('columnheader', { name: 'This week' }).waitFor()
      assert.equal(await page.locator('main').evaluate(node => node.scrollWidth - node.clientWidth), 0, `${width}px overflow`)
    }

    // Preference toggle persists through the real PUT payload and refreshed GET state.
    await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto(`${base}/preferences`)
    const awayToggle = page.getByRole('switch', { name: "Hold my digest on days I'm away" })
    assert.equal(await awayToggle.getAttribute('data-state'), 'checked')
    const savedPreference = page.waitForResponse(response => response.url().endsWith('/api/v1/me/preferences/digest') && response.request().method() === 'PUT')
    await awayToggle.click(); await savedPreference
    assert.deepEqual(preferenceBodies.at(-1), { enabled: true, time: '08:00', holdDigestWhenAway: false })
    assert.equal(await page.getByRole('switch', { name: "Hold my digest on days I'm away" }).getAttribute('data-state'), 'unchecked')
    await page.setViewportSize({ width: 390, height: 740 }); await page.reload()
    await page.getByRole('switch', { name: "Hold my digest on days I'm away" }).waitFor()
    assert.equal(await page.locator('main').evaluate(node => node.scrollWidth - node.clientWidth), 0, 'Preferences phone overflow')

    await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') })
    const violations = await page.evaluate(async () => (await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa'] } })).violations.map(v => v.id))
    assert.deepEqual(errors, [], JSON.stringify(errors)); assert.deepEqual(unknown, [], `Unmocked API: ${unknown.join(', ')}`)
    assert.deepEqual(violations, [], `Accessibility violations: ${violations.join(', ')}`)
    console.log(JSON.stringify({ scope: 'Chromium preview with mocked API', gridRequests: gridRequests.length, preferenceBodies: preferenceBodies.length, violations }))
  } finally {
    await browser?.close(); server.kill()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
