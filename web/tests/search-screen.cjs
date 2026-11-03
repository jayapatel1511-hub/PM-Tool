// Focused actual-SPA contracts with synthetic reads. Real Search predicates/authority belong to SearchScreenTests.
// S46: AC-SCR-79 recents/Clear/EIT/filters; AC-SCR-84 no new fields, controls or storage while off.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./search-screen.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the coordinator local preview')
const historyKey = viewerId => 'hub.recent-searches.v1.' + encodeURIComponent(viewerId)

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], writes = [], unexpectedMethods = [], unknownGets = [], queries = [], personReads = [], observations = []
  let actor = f.actorFor(), refuseScopedTasks = false
  try {
    page.setDefaultTimeout(15000)
    // Observe only recents-specific access. Existing application preference storage is intentionally allowed.
    await page.addInitScript(() => {
      window.__s46RecentOps = []
      for (const method of ['getItem', 'setItem', 'removeItem']) {
        const original = Storage.prototype[method]
        Storage.prototype[method] = function (key, ...args) {
          if (typeof key === 'string' && key.startsWith('hub.recent-searches.v1.')) window.__s46RecentOps.push({ method, key })
          return original.call(this, key, ...args)
        }
      }
    })
    page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => {
      const path = new URL(request.url()).pathname
      if (path.startsWith('/api/') && !['GET', 'HEAD', 'OPTIONS'].includes(request.method()) && !(request.method() === 'POST' && path === '/api/v1/me/sign-in')) writes.push(request.method() + ' ' + path)
    })
    const shellGets = new Set(['config', 'reference', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'])
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() === 'POST' && path === 'me/sign-in') return route.fallback()
      if (request.method() !== 'GET') {
        unexpectedMethods.push(request.method() + ' ' + path)
        return route.fulfill({ status: 405, json: { detail: 'Search and Person inspection permit no business command' } })
      }
      if (path === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: actor })
      if (path === 'reference') return route.fulfill({ json: f.reference })
      if (path === 'search') {
        const query = Object.fromEntries(url.searchParams)
        try {
          if (refuseScopedTasks && query.type === 'tasks' && query.projectId === f.ids.project) throw new Error('Synthetic scoped task results refused after the cached read')
          const data = f.searchRead(url.searchParams, actor.flags.people_screens === true)
          queries.push({ query, status: 200, counts: data.counts })
          return route.fulfill({ json: data })
        } catch (error) {
          queries.push({ query, status: 400 })
          return route.fulfill({ status: 400, contentType: 'application/problem+json', body: JSON.stringify({ status: 400, title: 'Invalid search', code: 'invalid', detail: error.message }) })
        }
      }
      if (/^people\/[^/]+\/panel$/.test(path)) {
        const userId = path.split('/')[1]
        personReads.push({ userId, query: Object.fromEntries(url.searchParams) })
        return route.fulfill({ json: f.personRead(userId) })
      }
      if (shellGets.has(path)) return route.fallback()
      unknownGets.push(path)
      return route.fulfill({ status: 501, json: { detail: 'Unexpected GET in focused Search harness: ' + path } })
    })
    const goto = path => page.goto(base + path)
    const recents = () => page.getByRole('region', { name: 'Recent searches', exact: true })
    const stored = async viewerId => {
      // Native storageState reads do not call the patched application Storage methods.
      const state = await page.context().storageState(), origin = state.origins.find(origin => origin.origin === new URL(base).origin)
      const raw = origin?.localStorage.find(item => item.name === historyKey(viewerId))?.value
      return raw ? JSON.parse(raw) : []
    }
    const submit = async term => {
      await page.locator('#search-q').fill(term); await page.locator('#search-q').press('Enter')
      await page.waitForURL(url => url.searchParams.get('q') === term)
      await page.waitForFunction(term => document.getElementById('search-q')?.defaultValue === term, term)
      await recents().getByRole('button', { name: term, exact: true }).waitFor()
    }
    const clearHistory = async () => {
      await recents().getByRole('button', { name: 'Clear recent searches', exact: true }).click()
      await recents().getByRole('status').getByText('Recent searches cleared.', { exact: true }).waitFor()
      assert.deepEqual(await stored(actor.id), [])
    }
    const tabCount = async type => (await page.locator('#search-tab-' + type).locator(':scope > span').innerText()).trim()
    const filterFields = '#search-project-filter,#search-status-filter,#search-discipline-filter'
    const openFilters = async (keyboard = false) => {
      const narrow = await page.evaluate(() => matchMedia('(max-width: 1023.98px)').matches)
      const triggerSlot = narrow ? 'sheet-trigger' : 'popover-trigger', contentSlot = narrow ? 'sheet-content' : 'popover-content'
      // Wait for FilterBar's actual responsive branch, rather than finding still-visible
      // fields in an exiting desktop Popover after the viewport has changed.
      const button = page.locator('#content [data-slot="' + triggerSlot + '"]').filter({ hasText: /^Filters/ })
      await button.waitFor({ state: 'visible' })
      if (await button.getAttribute('aria-expanded') !== 'true') {
        if (keyboard) { await button.focus(); await page.keyboard.press('Enter') } else await button.click()
      }
      const content = page.locator('[data-slot="' + contentSlot + '"][data-state="open"]').filter({ has: page.locator(filterFields) })
      await content.waitFor({ state: 'visible' })
      await page.getByRole('dialog', { name: 'Filters', exact: true }).waitFor({ state: 'visible' })
      assert.equal(await button.getAttribute('aria-expanded'), 'true')
      await content.locator(filterFields).first().waitFor({ state: 'visible' })
      return content
    }
    const closeFilters = async () => {
      const fields = await page.locator(filterFields).all()
      assert.ok(fields.length > 0, 'Close the actual mounted filter content')
      await page.keyboard.press('Escape')
      await Promise.all(fields.map(field => field.waitFor({ state: 'hidden' })))
    }
    const waitRows = async (type, total) => {
      await page.waitForFunction(({ type, total }) => {
        const tab = document.getElementById('search-tab-' + type), badge = tab?.querySelector(':scope > span')
        return tab?.getAttribute('aria-selected') === 'true' && badge?.textContent?.trim() === String(total)
      }, { type, total })
    }
    const roundtrip = async (opener, userId) => {
      const original = page.url(), focusId = await opener.getAttribute('id')
      await opener.focus(); await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + userId + '"]').waitFor()
      assert.equal(new URL(page.url()).searchParams.get('panel'), 'Person:' + userId)
      await page.getByRole('dialog', { name: 'Person', exact: true }).getByRole('button', { name: 'Close', exact: true }).click()
      await page.waitForURL(original); await page.waitForFunction(id => document.activeElement?.id === id, focusId)
    }
    await goto('/search'); await recents().waitFor()
    await page.locator('#search-q').fill('alpha')
    assert.deepEqual(await stored(f.ids.viewer), [], 'Per-keystroke typing never records history')
    for (const term of ['alpha', 'beta', 'gamma']) await submit(term)
    assert.deepEqual(await stored(f.ids.viewer), ['gamma', 'beta', 'alpha'])
    assert.deepEqual(await recents().locator('ol button').allTextContents(), ['gamma', 'beta', 'alpha'])
    await recents().getByRole('button', { name: 'beta', exact: true }).focus(); await page.keyboard.press('Enter')
    await page.waitForURL(url => url.searchParams.get('q') === 'beta')
    assert.deepEqual(await stored(f.ids.viewer), ['beta', 'gamma', 'alpha'], 'Keyboard activation of an older recent term is one deliberate search')
    await page.reload(); await recents().getByRole('button', { name: 'beta', exact: true }).waitFor()
    assert.deepEqual(await stored(f.ids.viewer), ['beta', 'gamma', 'alpha'], 'Reload neither duplicates nor loses last searches')
    actor = f.actorFor(f.ids.otherViewer)
    await page.reload(); await recents().getByRole('button', { name: 'beta', exact: true }).waitFor()
    assert.equal(await recents().getByRole('button', { name: 'alpha', exact: true }).count(), 0)
    assert.deepEqual(await stored(f.ids.otherViewer), ['beta']); assert.deepEqual(await stored(f.ids.viewer), ['beta', 'gamma', 'alpha'])
    actor = f.actorFor(); await page.reload(); await recents().getByRole('button', { name: 'alpha', exact: true }).waitFor()
    await clearHistory(); assert.deepEqual(await stored(f.ids.otherViewer), ['beta'])
    observations.push('three deliberate searches in order, no keystroke writes, native keyboard recent replay, reload/dedup and actual viewer partition/Clear')

    await submit('valve'); await page.locator('#search-tab-tasks').click(); await waitRows('tasks', 4); await clearHistory()
    await openFilters(); await page.locator('#search-project-filter').selectOption(f.ids.project); await waitRows('tasks', 2)
    await page.locator('#search-discipline-filter').selectOption(f.ids.discipline); await waitRows('tasks', 2)
    await page.locator('#search-status-filter').selectOption('In Progress'); await waitRows('tasks', 1)
    assert.equal(await tabCount('projects'), '1')
    assert.ok(queries.some(read => !read.query.type && read.query.projectId === f.ids.project && read.counts?.tasks === 2), 'Other-tab/base query keeps the unmodified task cohort before tab status')
    assert.ok(queries.filter(read => !read.query.type).every(read => !read.query.status), 'All-type counts never receive a per-tab status')
    await closeFilters()
    const taskRow = page.locator('[data-search-page-hit="tasks:' + f.ids.task + '"]')
    assert.equal(await taskRow.getByRole('link').getAttribute('href'), '/projects/P-C000/tasks?panel=Task:' + f.ids.task)
    const filteredUrl = page.url()
    await page.setViewportSize({ width: 375, height: 900 })
    const phoneFilters = await openFilters(true)
    await phoneFilters.locator('#search-project-filter').focus(); await page.keyboard.press('Tab')
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'search-status-filter', 'Phone filter fields have natural keyboard order')
    await closeFilters()
    await roundtrip(taskRow.locator('[data-person-panel-trigger="' + f.ids.alex + '"]'), f.ids.alex)
    assert.equal(page.url(), filteredUrl); assert.deepEqual(await stored(f.ids.viewer), [], 'Clear survives type/filter/Person returns without re-recording unchanged q')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Search/filter tokens fit375px')
    assert.equal(await page.locator('.visual-sidebar a[aria-current="page"]').count(), 0, 'Search utility selects no main rail item')
    await page.setViewportSize({ width: 1440, height: 1000 })
    await page.locator('#search-tab-people').click()
    assert.equal(new URL(page.url()).searchParams.has('projectId'), false); assert.equal(new URL(page.url()).searchParams.has('status'), false)
    assert.equal(new URL(page.url()).searchParams.get('disciplineId'), f.ids.discipline)
    await openFilters(); assert.equal(await page.locator('#search-project-filter').count(), 0); await closeFilters()
    await page.getByRole('button', { name: 'Clear filters', exact: true }).click()
    await page.waitForURL(url => ['projectId', 'status', 'disciplineId'].every(key => !url.searchParams.has(key)))
    await page.getByRole('group', { name: 'Active filters', exact: true }).waitFor({ state: 'hidden' })
    await submit('EIT'); await page.locator('#search-tab-people').click(); await waitRows('people', 2)
    assert.ok((await page.locator('[data-search-page-hit="people:' + f.ids.alex + '"]').innerText()).includes('Civil EIT'))
    await submit('Electrical'); await page.locator('#search-tab-people').click(); await waitRows('people', 1)
    const jill = page.locator('[data-search-page-hit="people:' + f.ids.jill + '"]')
    assert.ok((await jill.innerText()).includes('Technologist'), 'Home-discipline-only match keeps actual hit data')
    assert.equal(await jill.getByRole('link').count(), 0, 'CanViewWork=false is retained')
    await roundtrip(jill.locator('[data-person-panel-trigger="' + f.ids.jill + '"]'), f.ids.jill)
    observations.push('metadata controls, explicit-status filtered count, other-tab base counts, unsupported tab filter removal,375keyboard/natural Person return, title/home People matches and CanViewWork')

    await goto('/search?q=valve&type=tasks&projectId=' + f.ids.externalProject); await waitRows('tasks', 1); await openFilters()
    assert.equal(await page.locator('#search-project-filter').inputValue(), f.ids.externalProject)
    assert.equal(await page.locator('#search-project-filter option:checked').innerText(), 'Selected project ID: ' + f.ids.externalProject)
    await page.getByText(/^Showing 200 project choices/).waitFor(); await page.getByText(/^Showing 200 discipline choices/).waitFor(); await closeFilters()
    await goto('/search?q=EIT&type=people&disciplineId=' + f.ids.externalDiscipline); await waitRows('people', 1); await openFilters()
    assert.equal(await page.locator('#search-discipline-filter option:checked').innerText(), 'Selected discipline ID: ' + f.ids.externalDiscipline)
    await closeFilters()
    const beforeImplicit = queries.length
    await goto('/search?q=EIT&projectId=' + f.ids.project); await waitRows('projects', 0)
    assert.equal(await tabCount('people'), '2')
    assert.ok(!queries.slice(beforeImplicit).some(read => read.query.type === 'people' && read.query.projectId), 'Implicit first-tab selection skips cohorts that cannot carry common filters')
    await page.locator('#search-tab-people').click(); await waitRows('people', 2)
    assert.equal(new URL(page.url()).searchParams.has('projectId'), false)
    await goto('/search?q=EIT&type=people&projectId=' + f.ids.project)
    await page.getByRole('alert').getByText('Synthetic projectId filter is not applicable to people', { exact: true }).waitFor()
    assert.equal(await page.locator('#search-results [data-person-panel-trigger]').count(), 0)
    await page.getByRole('button', { name: 'Clear filters', exact: true }).click(); await waitRows('people', 2)
    observations.push('bounded choice truncation with selected external IDs kept truthfully, implicit common-filter skip and explicit inapplicable URL refusal/clear')

    // Actual stale cache: leave a successful Task key mounted in QueryClient, then revisit it after normal freshness expires.
    await goto('/search?q=valve&type=tasks&projectId=' + f.ids.project); await waitRows('tasks', 2)
    const observedAt = await page.evaluate(() => Date.now())
    await page.locator('#search-tab-projects').click(); await waitRows('projects', 1)
    await page.waitForFunction(since => Date.now() - since > 15_100, observedAt, { timeout: 20000 }) // main.tsx keeps reads fresh15s; no fake clock/focus/cache injection.
    refuseScopedTasks = true
    const refused = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/search' && new URL(response.url()).searchParams.get('type') === 'tasks' && response.status() === 400)
    await page.locator('#search-tab-tasks').click(); await refused
    await page.getByRole('alert').getByText('Synthetic scoped task results refused after the cached read', { exact: true }).waitFor()
    assert.equal(await page.locator('#search-results [data-person-panel-trigger]').count(), 0, 'Refused current-key read hides previously cached Person actions')
    assert.equal(await page.locator('#search-results').getByText('Synthetic valve review', { exact: true }).count(), 0)
    assert.equal(await page.locator('#search-tab-tasks').getByText('Unavailable', { exact: true }).count(), 1, 'Refusal does not invent a zero count')
    await page.getByRole('button', { name: 'Clear filters', exact: true }).click(); await waitRows('tasks', 4); refuseScopedTasks = false
    observations.push('real15s QueryClient cache revisit/refusal hides stale hit names/Person actions, unknown count and clear-to-valid-key recovery')

    const savedBeforeOff = await stored(f.ids.viewer), beforeOff = queries.length
    actor = f.actorFor(f.ids.viewer, false)
    await goto('/search?q=valve&type=tasks&projectId=malformed&status=malformed&disciplineId=malformed'); await waitRows('tasks', 4)
    await page.locator('#search-results').getByText('Synthetic valve review', { exact: true }).waitFor()
    assert.equal(await recents().count(), 0); assert.equal(await page.locator('#search-project-filter,#search-status-filter,#search-discipline-filter').count(), 0)
    assert.equal(await page.locator('#search-results [data-person-panel-trigger]').count(), 0)
    assert.deepEqual(await page.evaluate(() => window.__s46RecentOps), [], 'Off touches no recents storage; unrelated preference storage is allowed')
    assert.deepEqual(await stored(f.ids.viewer), savedBeforeOff)
    assert.ok(queries.slice(beforeOff).every(read => !read.query.projectId && !read.query.status && !read.query.disciplineId), 'Off request params preserve legacy Search even with malformed URL controls')
    assert.ok(personReads.every(read => f.people.some(person => person.id === read.userId) && !read.query.staffScope))
    assert.deepEqual(writes, []); assert.deepEqual(unexpectedMethods, []); assert.deepEqual(unknownGets, []); assert.deepEqual(ui.mock.unknownGets, []); assert.deepEqual(errors, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'search-screen', evidence: 'synthetic actual-SPA UI contracts only; real Search mappings/authority remain API-owned', observations,
      searchReads: queries.length, personReads: personReads.length, businessWrites: writes.length, unexpectedGets: unknownGets.length }))
  } catch (error) {
    try {
      const actual = await page.evaluate(() => {
        const input = document.getElementById('search-q'), selected = document.querySelector('[id^="search-tab-"][aria-selected="true"]')
        const filters = document.querySelector('[role="group"][aria-label="Active filters"]')
        return { url: location.href.slice(0, 2000), input: input?.value?.slice(0, 200) ?? null,
          selectedTab: selected?.id ?? null, selectedCount: selected?.querySelector(':scope > span')?.textContent?.trim().slice(0, 100) ?? null,
          activeFilters: filters?.textContent?.trim().slice(0, 1000) ?? null }
      })
      console.error(JSON.stringify({ status: 'FAIL', suite: 'search-screen', actual, recentSearchReads: queries.slice(-12), observations: observations.slice(-6) }))
    } catch (diagnosticError) {
      console.error(JSON.stringify({ status: 'FAIL', suite: 'search-screen', diagnosticError: String(diagnosticError).slice(0, 1000),
        url: page.url().slice(0, 2000), recentSearchReads: queries.slice(-12) }))
    }
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
