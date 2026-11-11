// Actual built SPA, synthetic API contracts. No tests/services are started by authoring this file.
// Real publication/cohort/transaction/cross-device proofs remain with the coordinator's API/integration suites.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./work-seen-state.fixture.cjs')
const { projectTeam, teamMember } = require('./team.fixture.cjs')
const { planningGrid } = require('./planning.fixture.cjs')
const { taskDependencies } = require('./dependencies.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const personal = new Set(['me/work/visits', 'me/work/opened', 'me/work/seen-all'])
function deferred() { let resolve; const promise = new Promise(r => { resolve = r }); return { promise, resolve } }
async function bounded(promise, description) {
  let timer
  try { return await Promise.race([promise, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(description)), 12000) })]) }
  finally { clearTimeout(timer) }
}

async function setup(options, browserOptions) {
  const ui = await newMockPage(base, { events: false, ...browserOptions }), { page } = ui, world = f.world(options)
  const state = { commands: [], reads: [], business: [], errors: [], visitReceived: deferred(), visitGate: null, detailGate: null,
    invalidVisit: false, failOpened: false, denyDetail: false, loseMarkAll: false, replaceAfterOpen: false }
  await page.clock.install({ time: new Date(f.at) }) // Exact event is 2h before this synthetic browser clock; timers still run normally.
  page.setDefaultTimeout(12000); page.on('pageerror', e => state.errors.push(e.message))
  page.on('request', request => {
    const path = new URL(request.url()).pathname.replace('/api/v1/', '')
    if (!request.url().includes('/api/') || ['GET', 'HEAD', 'OPTIONS'].includes(request.method()) || path === 'me/sign-in') return
    if (request.method() !== 'POST' || !personal.has(path)) state.business.push({ path, method: request.method() })
  })
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
    if (request.method() !== 'GET') {
      if (path === 'me/sign-in') return route.fallback()
      if (!personal.has(path) || request.method() !== 'POST') return route.fulfill({ status: 405, json: { detail: 'Only the three own personal commands are allowed in this UI suite' } })
      const body = request.postDataJSON(); state.commands.push({ path, body })
      if (path === 'me/work/opened' && state.failOpened) return route.fulfill({ status: 503, json: { detail: 'Synthetic acknowledgement unavailable' } })
      if (path === 'me/work/visits' && state.invalidVisit) { state.invalidVisit = false; state.visitReceived.resolve(body); return route.fulfill({ json: {} }) }
      const result = world.command(path, body)
      if (path === 'me/work/visits') { state.visitReceived.resolve(body); if (state.visitGate) await state.visitGate.promise }
      if (path === 'me/work/opened' && state.replaceAfterOpen && body.observedTokens.includes(f.tokens.assignee)) {
        state.replaceAfterOpen = false; world.replace(f.tokens.replacement)
      }
      if (path === 'me/work/seen-all' && state.loseMarkAll) {
        state.loseMarkAll = false; world.replace(f.tokens.late) // Producer commits AFTER the model's original acknowledgement cut.
        return route.abort('failed')
      }
      return route.fulfill({ json: result })
    }
    state.reads.push({ path, params: Object.fromEntries(url.searchParams) })
    if (path === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: world.actor })
    if (path === 'me/work') return route.fulfill({ json: world.work(url.searchParams.get('userId')) })
    if (path === 'reference') return route.fulfill({ json: f.reference })
    if (path === 'projects') return route.fulfill({ json: f.projectRows })
    if (path === 'projects/' + f.project.id || path === 'projects/' + f.project.projectNumber) return route.fulfill({ json: f.project })
    if (path === 'projects/' + f.project.id + '/tasks' || path === 'tasks') return route.fulfill({ json: world.list(url.searchParams.has('createdBy'), !url.searchParams.has('seenSubjectId') || url.searchParams.get('seenSubjectId') === f.ids.viewer) })
    if (path === 'projects/' + f.project.id + '/deliverables') return route.fulfill({ json: [world.deliverable()] })
    if (path === 'projects/' + f.project.id + '/milestones') return route.fulfill({ json: [] })
    if (path === 'projects/' + f.project.id + '/team') return route.fulfill({ json: projectTeam({ canManage: false, members: [teamMember({ id: f.id(9400), userId: f.ids.viewer, displayName: f.viewerName }), teamMember({ id: f.id(9401), userId: f.ids.pat, displayName: f.patName })] }) })
    if (path === 'planning/grid') return route.fulfill({ json: planningGrid() })
    if (path === 'users') return route.fulfill({ json: [{ id: f.ids.viewer, displayName: f.viewerName, isActive: true }, { id: f.ids.pat, displayName: f.patName, isActive: true }] })
    if (/^people\/[^/]+\/panel$/.test(path)) {
      const id = path.split('/')[1]
      assert.ok([f.ids.viewer, f.ids.pat, f.ids.other].includes(id), 'Only proven User IDs open Person')
      assert.equal(url.searchParams.has('staffScope'), false, 'MyWork names never grant a Staff scope')
      return route.fulfill({ json: { id, displayName: id === f.ids.pat ? f.patName : id === f.ids.viewer ? f.viewerName : 'Synthetic other person', jobTitle: null, isActive: true,
        today: '2026-11-02', week: '2026-11-02', asOf: f.at, organisationFacts: { office: null, homeDiscipline: null, supervisor: null },
        projects: { page: 1, pageSize: 25, total: 0, items: [] } } })
    }
    if (/^tasks\/[^/]+$/.test(path)) {
      if (state.detailGate) await state.detailGate.promise
      if (state.denyDetail) return route.fulfill({ status: 403, json: { detail: 'Synthetic detail denied after refresh' } })
      return route.fulfill({ json: world.taskDetail(path.split('/')[1]) })
    }
    if (/^tasks\/[^/]+\/dependencies$/.test(path)) return route.fulfill({ json: taskDependencies() })
    // With work_screens on the Task panel mounts its History section (FR-SCR-26): an empty history and person filter.
    if (/^tasks\/[^/]+\/activity\/actors$/.test(path)) return route.fulfill({ json: [] })
    if (/^items\/Task\/[^/]+\/activity$/.test(path)) return route.fulfill({ json: { items: [], page: 1, pageSize: 100, totalCount: 0 } })
    if (path === 'deliverables/' + f.ids.deliverable) return route.fulfill({ json: world.deliverableDetail() })
    if (/^items\/(Task|Deliverable)\/[^/]+\/comments$/.test(path)) return route.fulfill({ json: path === 'items/Task/' + f.ids.created + '/comments' ? { ...world.thread, items: [] } : world.thread })
    if (/^items\/[^/]+\/[^/]+\/history$/.test(path)) return route.fulfill({ json: { items: [], page: 1, pageSize: 50, totalCount: 0 } })
    if (/^items\/[^/]+\/[^/]+\/links$/.test(path)) return route.fulfill({ json: { links: [], inherited: [], canAdd: false } })
    return route.fallback()
  })
  const goto = path => page.goto(base + path)
  const frames = () => page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))))
  const visits = () => state.commands.filter(c => c.path === 'me/work/visits')
  const opened = () => state.commands.filter(c => c.path === 'me/work/opened')
  const taskRow = (id = f.ids.task) => page.locator('#mw-tasks li').filter({ has: page.getByRole('button', { name: world.task(id).name, exact: true }) }).first()
  const ready = () => page.locator('[data-work-seen-all]:not([disabled])').waitFor()
  const close = (kind = /^(Task|Deliverable)$/) => page.getByRole('dialog', { name: kind }).getByRole('button', { name: 'Close', exact: true }).click()
  const view = async name => { await page.getByRole('button', { name: /^My Work views:/ }).click(); await page.getByRole('menuitemradio', { name, exact: true }).click() }
  const clean = () => { assert.deepEqual(state.business, []); assert.deepEqual(state.errors, []); assert.deepEqual(ui.mock.unknownGets, []); assert.deepEqual(ui.mock.apiErrors, []) }
  return { ...ui, world, state, goto, frames, visits, opened, taskRow, ready, close, view, clean }
}

;(async () => {
  const observations = []
  // GET/prefetch is inert; an initial visible own read is deduplicated and needs an actual successful receipt.
  {
    const c = await setup(), { page, state } = c
    try {
      await c.goto('/projects/' + f.project.projectNumber + '/tasks')
      await page.getByText(c.world.task(f.ids.task).name, { exact: true }).first().waitFor()
      await page.evaluate(async id => { await fetch('/api/v1/me/work'); await fetch('/api/v1/tasks/' + id) }, f.ids.task)
      await c.frames(); assert.equal(state.commands.length, 0, 'HTTP prefetched facts do not enroll or acknowledge')
      const workBefore = state.reads.filter(r => r.path === 'me/work').length
      state.visitGate = deferred()
      await c.goto('/my-work?tab=overview'); await bounded(state.visitReceived.promise, 'Visible own MyWork did not submit its visit')
      assert.equal(state.reads.filter(r => r.path === 'me/work').length - workBefore, 1, 'Shared MyWork observers issue one initial base GET')
      assert.equal(await page.locator('[data-work-seen-all]').isDisabled(), true, 'Mark all waits for a validated visit receipt')
      assert.equal(state.reads.filter(r => /^(?:tasks\/[^/]+|items\/.+\/(?:history|comments))$/.test(r.path)).length, 1, 'Closed My Work rows add no per-row detail/history/comments reads')
      state.visitGate.resolve(); await c.ready()
      await c.taskRow().locator('[data-work-new]').waitFor()
      const first = c.visits()[0].body.requestId
      const upcomingRead = page.waitForResponse(response => {
        const url = new URL(response.url())
        return url.pathname === '/api/v1/tasks' && url.searchParams.has('dueFrom') && url.searchParams.get('open') === 'true'
      })
      await c.view('Upcoming'); await upcomingRead
      await page.getByRole('heading', { name: 'My Work', exact: true }).waitFor()
      await page.getByRole('button', { name: 'My Work views: Upcoming', exact: true }).waitFor()
      assert.equal(new URL(page.url()).searchParams.get('tab'), 'upcoming')
      await c.view('Overview'); await page.getByRole('button', { name: 'Filters', exact: true }).click()
      await page.locator('#mw-priority').selectOption('High'); await c.frames()
      assert.equal(c.visits().length, 1, 'Tabs and filters keep the mounted visit')
      await page.locator('#mw-priority').selectOption(''); await c.taskRow().locator('[data-work-new]').waitFor()
      await c.goto('/my-work?tab=overview'); await c.ready()
      assert.equal(c.visits().length, 2, 'A fresh route visit is distinct')
      assert.notEqual(c.visits()[1].body.requestId, first)
      await c.taskRow().locator('[data-work-new]').waitFor(); assert.equal(c.opened().length, 0, 'A visit preserves pending New')
      const activity = c.taskRow().locator('[data-work-activity="Task:' + f.ids.task + '"]')
      assert.equal(await activity.locator('time').getAttribute('datetime'), '2026-11-02T10:00:00Z')
      assert.ok((await activity.innerText()).includes(f.patName)); assert.equal(await activity.getByRole('button', { name: 'Open Comments for S-ASSIGNED (3)', exact: true }).count(), 1)
      assert.equal(await page.getByText('Synthetic cached actor must not appear', { exact: true }).count(), 0)
      assert.equal(await page.locator('#mw-projects [data-work-comments]').count(), 0, 'Non-commentable Project has no fake thread/count action')
      assert.equal(await page.locator('#mw-projects').getByText('System', { exact: true }).count(), 1, 'Only an explicit System fact gets the System label')
      assert.equal(await c.taskRow(f.ids.created).getByLabel('No comments', { exact: true }).count(), 1, 'Known zero comments remains distinct from unavailable')
      assert.ok((await page.locator('#mw-milestones').innerText()).includes('Actor unavailable'))
      assert.ok((await page.locator('#mw-milestones').innerText()).includes('Time unavailable'))
      const personIds = await page.locator('[data-person-panel-trigger]').evaluateAll(nodes => nodes.map(n => n.id))
      assert.equal(new Set(personIds).size, personIds.length, 'Repeated tasks/reviews/waiting/blocking activity names have distinct source keys')
      for (const [section, prefix] of [['waiting', /^S-BLOCKER Synthetic blocker/], ['blocking', /^S-SUCCESSOR Synthetic successor/]]) {
        const opener = page.locator('#mw-' + section).getByText(prefix).locator('[data-person-panel-trigger="' + f.ids.pat + '"]')
        const origin = page.url(), focusId = await opener.getAttribute('id')
        await opener.focus(); await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + f.ids.pat + '"]').waitFor()
        await c.close('Person'); await page.waitForURL(origin); await page.waitForFunction(id => document.activeElement?.id === id, focusId)
      }
      assert.equal(c.visits().length, 2, 'Nested blocker/successor Person return keeps the current own visit')
      c.clean(); observations.push('GET/prefetch inert; deduplicated visible visit, tabs/filters, pending New and exact event/comment facts')
    } finally { state.visitGate?.resolve(); await c.browser.close() }
  }
  // An invalid successful HTTP body cannot fake enrolment; retry preserves the exact original request ID.
  {
    const c = await setup(); c.state.invalidVisit = true
    try {
      await c.goto('/my-work?tab=overview'); await c.page.getByRole('button', { name: 'Retry My Work visit', exact: true }).waitFor()
      assert.equal(await c.page.locator('[data-work-seen-all]').isDisabled(), true)
      const initial = c.visits()[0].body
      await c.page.getByRole('button', { name: 'Retry My Work visit', exact: true }).click(); await c.ready()
      assert.deepEqual(c.visits()[1].body, initial); c.clean(); observations.push('Required response validation and visit retry identity')
    } finally { await c.browser.close() }
  }
  // Actual browser tab visibility: a hidden MyWork answer is not enrollment; showing it refetches before enrollment.
  {
    const c = await setup(undefined, { nativeVisibility: true }), other = await c.page.context().newPage()
    try {
      await c.goto('/projects/' + f.project.projectNumber + '/tasks'); await other.bringToFront()
      await c.page.waitForFunction(() => document.visibilityState === 'hidden', undefined, { polling: 50 })
      await c.goto('/my-work?tab=overview'); await c.page.locator('#mw-tasks').waitFor({ state: 'attached' })
      // A real hidden tab can suspend RAF; inspect the committed read without waiting for hidden frames.
      assert.equal(c.visits().length, 0)
      const before = c.state.reads.filter(r => r.path === 'me/work').length
      await c.page.bringToFront(); await c.ready()
      assert.ok(c.state.reads.filter(r => r.path === 'me/work').length > before, 'Visible return has a fresh own answer')
      assert.equal(c.visits().length, 1); c.clean(); observations.push('Real hidden document has no visit; visibility return refetches')
    } finally { await other.close(); await c.browser.close() }
  }
  // Other-person and dark reads contain no private UI/commands, including detail opens while the flag is dark.
  {
    const c = await setup()
    try {
      await c.goto('/my-work?userId=' + f.ids.other); await c.page.locator('#mw-tasks').waitFor(); await c.frames()
      assert.equal(await c.page.locator('[data-work-new],[data-work-seen-controls]').count(), 0); assert.equal(c.state.commands.length, 0)
      c.world.actor.flags.work_screens = false
      await c.goto('/my-work?tab=overview'); await c.taskRow().waitFor(); await c.frames()
      assert.equal(await c.page.locator('[data-work-new],[data-work-seen-controls],[data-work-activity]').count(), 0)
      await c.taskRow().getByRole('button', { name: c.world.task(f.ids.task).name, exact: true }).click()
      await c.page.getByRole('dialog', { name: 'Task', exact: true }).waitFor(); await c.frames()
      assert.equal(c.state.commands.length, 0); c.clean(); observations.push('Other-person and flag-off private UI/commands omitted')
    } finally { await c.browser.close() }
  }
  // A still-mounted detail gets replacement tokens from a new canonical answer, with a new payload/request ID.
  {
    const c = await setup({ duplicateWireTokens: true }); c.state.replaceAfterOpen = true
    try {
      await c.goto('/my-work?tab=overview'); await c.ready()
      const replacementOpened = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/me/work/opened' && r.request().postDataJSON().observedTokens.includes(f.tokens.replacement))
      await c.taskRow().getByRole('button', { name: c.world.task(f.ids.task).name, exact: true }).click()
      await replacementOpened
      assert.equal(c.opened().length, 2)
      assert.deepEqual(c.opened()[0].body.observedTokens, [f.tokens.assignee, f.tokens.sameTaskReviewer].sort())
      assert.deepEqual(c.opened()[1].body.observedTokens, [f.tokens.replacement, f.tokens.sameTaskReviewer].sort())
      assert.notEqual(c.opened()[0].body.requestId, c.opened()[1].body.requestId)
      assert.ok(c.opened().every(c => c.body.itemType === 'Task' && c.body.itemId === f.ids.task))
      await c.close(); await c.page.locator('#mw-reviews [data-work-new]').waitFor(); c.clean()
      observations.push('Exact displayed token set, same mounted detail replacement gets new ID; other reviewer remains New')
    } finally { await c.browser.close() }
  }
  // A real native command dialog hides its parent. Background replacement facts must await a fresh exposed answer.
  {
    const c = await setup({ nativeDialog: true }, { nativeVisibility: true }), other = await c.page.context().newPage()
    try {
      await c.page.bringToFront(); await c.goto('/my-work?tab=overview'); await c.ready()
      const seenRead = c.page.waitForResponse(async r => new URL(r.url()).pathname === '/api/v1/tasks/' + f.ids.task && r.status() === 200 && (await r.json()).seen?.state === 'Seen')
      await c.taskRow().getByRole('button', { name: c.world.task(f.ids.task).name, exact: true }).click(); await seenRead
      const initial = c.opened()[0].body, count = c.opened().length
      await c.page.getByRole('dialog', { name: 'Task', exact: true }).getByRole('button', { name: 'Set block', exact: true }).click()
      const modal = c.page.getByRole('dialog', { name: 'Block S-ASSIGNED', exact: true })
      await modal.locator('#blk-reason').fill('Synthetic unsaved command text; this case only cancels')
      c.world.replace(f.tokens.replacement)
      // Exercise the established 15s browser-cache freshness/focus path, not a transaction timing race.
      await c.page.clock.runFor(15001)
      const backgroundRead = c.page.waitForResponse(async r => new URL(r.url()).pathname === '/api/v1/tasks/' + f.ids.task && r.status() === 200 && (await r.json()).seen?.observedTokens.some(t => t.token === f.tokens.replacement))
      await other.bringToFront()
      await c.page.waitForFunction(() => document.visibilityState === 'hidden', undefined, { polling: 50 })
      await c.page.bringToFront()
      await c.page.waitForFunction(() => document.visibilityState === 'visible', undefined, { polling: 50 })
      await backgroundRead; await c.frames()
      assert.equal(await modal.locator('#blk-reason').inputValue(), 'Synthetic unsaved command text; this case only cancels')
      assert.equal(c.opened().length, count, 'A replacement answered under the native modal is not acknowledged')
      c.state.detailGate = deferred()
      const reentry = c.page.waitForRequest(r => new URL(r.url()).pathname === '/api/v1/tasks/' + f.ids.task)
      await modal.getByRole('button', { name: 'Cancel', exact: true }).click(); await reentry; await c.frames()
      assert.equal(c.opened().length, count, 'Re-exposure waits for the new canonical answer, not hidden cached New')
      const acknowledged = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/me/work/opened' && r.request().postDataJSON().observedTokens.includes(f.tokens.replacement))
      c.state.detailGate.resolve(); c.state.detailGate = null; await acknowledged
      assert.notEqual(c.opened().at(-1).body.requestId, initial.requestId)
      assert.deepEqual(c.opened().at(-1).body.observedTokens, [f.tokens.replacement, f.tokens.sameTaskReviewer].sort())
      c.clean(); observations.push('Native dialog hidden-parent suppression, retained text and fresh re-entry before replacement acknowledgement')
    } finally { c.state.detailGate?.resolve(); await other.close(); await c.browser.close() }
  }
  // Pending/cancelled reads and cached New followed by a denied fresh answer cannot acknowledge.
  {
    const c = await setup()
    try {
      await c.goto('/my-work?tab=overview'); await c.ready(); c.state.detailGate = deferred()
      const requested = c.page.waitForRequest(r => new URL(r.url()).pathname === '/api/v1/tasks/' + f.ids.task)
      await c.taskRow().getByRole('button', { name: c.world.task(f.ids.task).name, exact: true }).click(); await requested
      await c.close(); c.state.detailGate.resolve(); c.state.detailGate = null; await c.frames()
      assert.equal(c.opened().length, 0, 'Cancelled detail content never acknowledges')
      c.state.failOpened = true
      const failed = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/me/work/opened' && r.status() === 503)
      await c.taskRow().getByRole('button', { name: c.world.task(f.ids.task).name, exact: true }).click(); await failed; await c.close()
      const count = c.opened().length; c.state.denyDetail = true
      const denied = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/tasks/' + f.ids.task && r.status() === 403)
      await c.taskRow().getByRole('button', { name: c.world.task(f.ids.task).name, exact: true }).click(); await denied
      await c.page.getByRole('alert').filter({ hasText: 'Synthetic detail denied after refresh' }).waitFor(); await c.frames()
      assert.equal(c.opened().length, count, 'Cached New plus a denied canonical refresh does not acknowledge')
      c.clean(); observations.push('Cancelled/pending and cached-then-denied details send no premature acknowledgement')
    } finally { c.state.detailGate?.resolve(); await c.browser.close() }
  }
  // A legacy canonical/source version of zero remains a valid observed token, without creating a visit.
  {
    const c = await setup({ enrolled: false })
    try {
      const opened = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/me/work/opened')
      await c.goto('/projects/' + f.project.projectNumber + '/tasks?panel=Task:' + f.ids.zero); const response = await opened
      assert.deepEqual(c.opened()[0].body.observedTokens, [f.tokens.zero]); assert.equal(c.visits().length, 0)
      assert.equal((await response.json()).firstVisitAt, null, 'Opening version zero is not MyWork enrollment')
      assert.equal(c.state.reads.some(r => r.path === 'tasks/' + f.ids.zero), true); c.clean(); observations.push('Fresh legacy version-zero detail acknowledges only its observed token')
    } finally { await c.browser.close() }
  }
  // Mark all is a private action allowed to active ReadOnly users. Lost response retry cannot widen its original cut.
  {
    const c = await setup({ readOnly: true }); c.state.loseMarkAll = true
    try {
      await c.goto('/my-work?tab=overview'); await c.ready()
      const reviewer = c.page.locator('#mw-reviews li').filter({ has: c.page.getByRole('button', { name: c.world.task(f.ids.review).name, exact: true }) })
      await reviewer.locator('[data-work-new]').waitFor()
      await c.page.locator('[data-work-seen-all]').click()
      await c.page.getByRole('button', { name: 'Retry mark all seen', exact: true }).waitFor()
      const initial = c.state.commands.find(c => c.path === 'me/work/seen-all').body
      const retried = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/me/work/seen-all' && r.status() === 200)
      await c.page.getByRole('button', { name: 'Retry mark all seen', exact: true }).click(); await retried
      await reviewer.locator('[data-work-new]').waitFor({ state: 'detached' })
      await c.taskRow().locator('[data-work-new]').waitFor()
      const attempts = c.state.commands.filter(c => c.path === 'me/work/seen-all')
      assert.equal(attempts.length, 2); assert.deepEqual(attempts[1].body, initial)
      assert.deepEqual(Object.keys(initial), ['requestId'], 'No UI IDs, filters, scope or timestamps in Mark all body')
      assert.equal(c.world.acknowledged.has(f.tokens.late), false, 'Late producer remains beyond the original cut')
      assert.equal(await reviewer.locator('[data-work-new]').count(), 0, 'Earlier reviewer token clears only after confirmed re-read')
      c.clean(); observations.push('ReadOnly personal Mark all, immutable lost-response retry and late New retained')
    } finally { await c.browser.close() }
  }
  // Creator-only is not an assignment, actual reviewer is. Non-Task Comments opens its exact thread and returns naturally.
  {
    const c = await setup()
    try {
      await c.goto('/my-work?tab=today&who=created'); await c.ready()
      const creator = c.page.getByRole('button', { name: c.world.task(f.ids.created).name, exact: true })
      await creator.waitFor(); assert.equal(await c.page.locator('[data-work-new]').count(), 0)
      assert.ok(c.state.reads.filter(r => r.path === 'tasks').every(r => r.params.seenSubjectId === f.ids.viewer))
      await creator.click(); await c.page.getByRole('dialog', { name: 'Task', exact: true }).waitFor(); await c.frames()
      assert.equal(c.opened().length, 0); await c.close(); await c.view('Overview')
      const review = c.page.locator('#mw-reviews li').filter({ has: c.page.getByRole('button', { name: c.world.task(f.ids.review).name, exact: true }) })
      await review.locator('[data-work-new]').waitFor()
      const realReview = c.page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/me/work/opened' && r.request().postDataJSON().itemId === f.ids.review)
      await review.getByRole('button', { name: c.world.task(f.ids.review).name, exact: true }).click(); await realReview; await c.close()
      await c.page.setViewportSize({ width: 375, height: 900 })
      const comments = c.page.locator('#mw-deliverables').getByRole('button', { name: 'Open Comments for S-DRAWING (3)', exact: true })
      const origin = c.page.url(), focusId = await comments.getAttribute('id')
      await comments.focus(); await c.page.keyboard.press('Enter')
      const dialog = c.page.getByRole('dialog', { name: 'Deliverable', exact: true })
      await dialog.getByText('Synthetic exact drawing comment 1', { exact: true }).waitFor()
      const url = new URL(c.page.url())
      assert.equal(url.searchParams.get('panel'), 'Deliverable:' + f.ids.deliverable)
      assert.equal(url.searchParams.get('recordTabItem'), 'Deliverable:' + f.ids.deliverable); assert.equal(url.searchParams.get('recordTab'), 'comments')
      assert.equal(url.searchParams.has('taskSection'), false)
      assert.equal(await c.page.evaluate(() => history.state.usr.recordContextReturn.url), origin.replace(base, ''))
      await c.close(); await c.page.waitForURL(origin); await c.page.waitForFunction(id => document.activeElement?.id === id, focusId)
      assert.ok(await c.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
      c.clean(); observations.push('Creator-only vs actual reviewer, exact nonTask Comments/return at375 with zero business writes')
    } finally { await c.browser.close() }
  }
  console.log(JSON.stringify({ result: 'PASS', observations, businessWrites: 0 }))
})().catch(error => { console.error(error); process.exitCode = 1 })
