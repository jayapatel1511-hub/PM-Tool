// Actual built SPA and synthetic permitted API reads. Coordinator runs; no business command is allowed.
// SearchPersonReferencesTests and PersonPanelTests retain actual server identity/access coverage.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./person-search-reassign.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use the coordinator local built preview')

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const errors = [], writes = [], reads = [], searchReads = [], forbidden = [], directories = [], observations = []
  let denied = null, off = false
  try {
    page.setDefaultTimeout(12000)
    page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => {
      const url = new URL(request.url())
      if (!url.pathname.startsWith('/api/') || ['GET', 'HEAD', 'OPTIONS'].includes(request.method()) || url.pathname === '/api/v1/me/sign-in') return
      writes.push({ method: request.method(), path: url.pathname, body: request.postData() })
    })
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        return route.fulfill({ status: 405, json: { detail: 'No business command is part of a Person selection' } })
      }
      if (path === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: f.actor })
      if (path === 'reference') return route.fulfill({ json: f.reference })
      if (path === 'search') {
        searchReads.push(Object.fromEntries(url.searchParams))
        return route.fulfill({ json: f.searchRead(url.searchParams, off) })
      }
      if (path === 'users/' + f.ids.source + '/open-work') return route.fulfill({ json: f.openWork })
      if (path === 'users') { directories.push(url.href); return route.fulfill({ json: f.people }) }
      if (path.startsWith('planning/') || path.startsWith('workload') || path.startsWith('staff')) {
        forbidden.push(url.href); return route.fulfill({ status: 403, json: { detail: 'Synthetic viewer has no hours/planner scope' } })
      }
      if (/^people\/[^/]+\/panel$/.test(path)) {
        const userId = path.split('/')[1]
        reads.push({ userId, query: Object.fromEntries(url.searchParams) })
        if (denied === userId) return route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, title: 'Not found', code: 'not_found', detail: 'Synthetic Person unavailable' }) })
        return route.fulfill({ json: f.personRead(userId) })
      }
      return route.fallback()
    })
    const goto = path => page.goto(base + path)
    const palette = () => page.locator('[data-search-palette]:visible')
    const popup = () => palette().locator('[data-search-popup]')
    const person = id => page.locator('[data-person-panel="' + id + '"]')
    const sheet = () => page.getByRole('dialog', { name: 'Person', exact: true })
    const close = () => sheet().getByRole('button', { name: 'Close', exact: true }).click()
    const trigger = (id, scope = page) => scope.locator('[data-person-panel-trigger="' + id + '"]:visible').first()
    const unique = async label => {
      const state = await page.locator('[data-person-panel-trigger]').evaluateAll(nodes => ({
        ids: nodes.map(node => node.id), nested: nodes.filter(node => node.parentElement?.closest('a,button,[role="option"],[role="listbox"],summary')).map(node => node.id),
      }))
      assert.equal(new Set(state.ids).size, state.ids.length, label + ': full trigger IDs are unique')
      assert.deepEqual(state.nested, [], label + ': no Person button is nested in a navigation or selection control')
    }
    const roundtrip = async (opener, userId) => {
      const original = page.url(), focusId = await opener.getAttribute('id')
      await opener.focus(); await page.keyboard.press('Enter')
      try { await person(userId).waitFor() } catch (error) {
        console.error(JSON.stringify({ expectedPerson: userId, actualUrl: page.url(), activeId: await page.evaluate(() => document.activeElement?.id), lastPersonReads: reads.slice(-4), alerts: await page.getByRole('alert').allTextContents() }))
        throw error
      }
      assert.equal(new URL(page.url()).searchParams.get('panel'), 'Person:' + userId)
      assert.equal(await person(userId).locator('[data-capacity]').count(), 0, 'No workload capability means no hours')
      await close(); await page.waitForURL(original)
      await page.waitForFunction(id => document.activeElement?.id === id, focusId)
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
      return focusId
    }
    const source = '/search?q=baseline&type=projects&archived=true'
    await goto(source)
    await page.locator('[data-search-page-hit]').first().waitFor()
    await palette().getByRole('combobox').fill('synthetic')
    await popup().getByRole('listbox').waitFor()
    const input = palette().getByRole('combobox', { name: 'Search', exact: true })
    const firstKey = 'projects:' + f.ids.task
    await popup().locator('[data-search-hit="' + firstKey + '"]').waitFor()
    await input.press('ArrowDown')
    assert.equal(await popup().getByRole('listbox').getByRole('option', { selected: true }).getAttribute('data-search-hit'), firstKey, 'First arrow preserves record selection')
    await input.press('ArrowDown'); await input.press('ArrowUp')
    assert.equal(await popup().getByRole('listbox').getByRole('option', { selected: true }).getAttribute('data-search-hit'), firstKey, 'Up/down keeps existing combobox semantics')
    const personSelect = () => palette().locator('[data-search-person-actions] select')
    await popup().hover(); await page.mouse.wheel(0, 420)
    await page.waitForFunction(() => (document.querySelector('[data-search-palette] [data-search-popup]')?.scrollTop ?? 0) > 0)
    await personSelect().selectOption('tasks:' + f.ids.task)
    const savedScroll = await popup().evaluate(node => node.scrollTop)
    const directoryBefore = directories.length
    await unique('palette and underlying page with same item/User IDs')
    const taskOpener = trigger(f.ids.source, palette()), taskFocus = await roundtrip(taskOpener, f.ids.source)
    assert.equal(await input.inputValue(), 'synthetic', 'Person return retains exact local query')
    assert.equal(await personSelect().inputValue(), 'tasks:' + f.ids.task, 'Active typed hit identity survives Person interlude')
    assert.equal(await popup().getByRole('listbox').getByRole('option', { selected: true }).getAttribute('data-search-hit'), 'tasks:' + f.ids.task)
    assert.ok(Math.abs(await popup().evaluate(node => node.scrollTop) - savedScroll) <= 2, 'Search popup nonzero scroll survives naturally')
    assert.equal(directories.length, directoryBefore, 'Search Person details never broadens a directory query')
    await personSelect().selectOption(firstKey)
    const projectFocus = await trigger(f.ids.source, palette()).getAttribute('id')
    assert.notEqual(projectFocus, taskFocus, 'Same item GUID and User in distinct groups has a distinct opener')
    await roundtrip(trigger(f.ids.source, palette()), f.ids.source)
    await personSelect().selectOption('comments:' + f.groups.comments[0].id)
    await roundtrip(trigger(f.ids.sameName, palette()), f.ids.sameName)
    assert.ok(reads.every(read => !read.query.staffScope), 'Search has no invented Staff scope')
    observations.push('desktop query, arrow selection, exact User refs, group/role IDs, actual blur, popup scroll and natural focus return')

    // Enter still performs the original work navigation action, independent of Details.
    await input.focus(); await input.press('Escape'); await input.fill('synthetic'); await input.focus(); await input.press('ArrowUp')
    assert.equal(await input.inputValue(), 'synthetic', 'Enter has an explicit nonempty search term after native Escape')
    await input.press('Enter'); await page.waitForURL(base + '/search?q=synthetic')
    assert.equal(new URL(page.url()).searchParams.has('panel'), false, 'Ordinary Enter never substitutes a Person destination')

    // Full-page paging, archived scope and the old work-record anchors survive Person round trips.
    const pageUrl = '/search?q=synthetic&type=tasks&archived=true'
    await goto(pageUrl)
    await page.locator('[data-search-page-hit]').first().waitFor()
    assert.equal(await page.locator('[data-search-page-hit]').count(), 50)
    await page.getByRole('button', { name: 'Show more', exact: true }).click()
    await page.getByText('Synthetic item without User metadata', { exact: true }).waitFor()
    await page.waitForFunction(() => document.querySelectorAll('#search-results ul > li').length === 52)
    const firstRow = page.locator('[data-search-page-hit="tasks:' + f.ids.task + '"]')
    assert.equal(await firstRow.getByRole('link').getAttribute('href'), '/projects/P-MOCK/tasks?panel=Task:' + f.ids.task)
    await roundtrip(trigger(f.ids.source, firstRow), f.ids.source)
    assert.equal(page.url(), base + pageUrl)
    assert.equal(await page.locator('#search-results ul > li').count(), 52, 'Already loaded pages are retained')
    const missing = page.locator('#search-results li').filter({ hasText: 'Synthetic item without User metadata' })
    assert.equal(await missing.locator('[data-person-panel-trigger]').count(), 0, 'Name alone and item GUID never become a Person link')
    await unique('full Search page')
    // Combined old titles stay intact; Details comes solely from separate typed metadata.
    for (const group of ['allocations', 'planning']) {
      await goto('/search?q=synthetic&type=' + group)
      const row = page.locator('[data-search-page-hit]').first()
      assert.ok((await row.getByRole('link').innerText()).includes('Combined ' + (group === 'allocations' ? 'allocation' : 'planning') + ' title stays intact'))
      await roundtrip(trigger(group === 'allocations' ? f.ids.sameName : f.ids.source, row), group === 'allocations' ? f.ids.sameName : f.ids.source)
    }
    await goto('/search?q=synthetic&type=people')
    const noWork = page.locator('[data-search-page-hit="people:' + f.ids.noWork + '"]')
    await noWork.waitFor()
    assert.equal(await noWork.getByRole('link').count(), 0, 'CanViewWork=false remains a non-navigation primary result')
    await roundtrip(trigger(f.ids.noWork, noWork), f.ids.noWork)
    denied = f.ids.noWork
    await trigger(f.ids.noWork, noWork).click(); await sheet().getByRole('alert').waitFor()
    assert.equal(await sheet().locator('[data-person-panel]').count(), 0, '404 hides previously cached Person facts')
    await close(); await page.waitForURL(base + '/search?q=synthetic&type=people'); denied = null
    observations.push('full Search archived query, loaded paging, unchanged record targets, intact combined titles, no-work access and 404')

    // Phone: a real parent Search Sheet remains mounted under Person and returns with its source state.
    await page.setViewportSize({ width: 375, height: 900 }); await goto(source)
    await page.locator('header').getByRole('button', { name: 'Search', exact: true }).click()
    const searchSheet = () => page.getByRole('dialog', { name: 'Search', exact: true })
    await searchSheet().waitFor()
    await palette().getByRole('combobox', { name: 'Search', exact: true }).fill('synthetic')
    await personSelect().waitFor()
    await popup().hover(); await page.mouse.wheel(0, 360)
    await page.waitForFunction(() => [...document.querySelectorAll('[data-search-popup]')].some(node => node.scrollTop > 0))
    await personSelect().selectOption('people:' + f.ids.sameName)
    const phoneScroll = await popup().evaluate(node => node.scrollTop)
    const phoneFocus = await trigger(f.ids.sameName, palette()).getAttribute('id')
    await trigger(f.ids.sameName, palette()).click(); await person(f.ids.sameName).waitFor()
    assert.equal(await page.locator('[data-search-palette] input').evaluateAll(nodes => nodes.filter(node => node.value === 'synthetic').length), 1, 'Phone Search input stays mounted beneath Person')
    await close(); await page.waitForURL(base + source)
    await searchSheet().waitFor(); await page.waitForFunction(id => document.activeElement?.id === id, phoneFocus)
    assert.equal(await palette().getByRole('combobox', { name: 'Search', exact: true }).inputValue(), 'synthetic')
    assert.equal(await personSelect().inputValue(), 'people:' + f.ids.sameName)
    assert.ok(Math.abs(await popup().evaluate(node => node.scrollTop) - phoneScroll) <= 2, 'Phone popup scroll is retained')
    await unique('375px Search sheet')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Search fits 375px')
    await searchSheet().getByRole('button', { name: 'Close', exact: true }).click()
    await page.setViewportSize({ width: 1440, height: 1000 })
    observations.push('375px nested Search Sheet/Person, exact URL, local query, typed selection, popup scroll, natural full opener focus')

    // Source name and selected destination are separate from both picker and command.
    const reassignUrl = '/people/' + f.ids.source + '/reassign-work?from=synthetic'
    await goto(reassignUrl)
    const heading = page.getByRole('heading', { level: 1 })
    assert.ok((await heading.innerText()).includes('(Inactive)'), 'Inactive source title remains explicit')
    const picks = page.getByRole('checkbox', { name: 'Select T-SYN-0 Synthetic selected task', exact: true })
    await picks.nth(0).check(); await picks.nth(1).check()
    const target = page.getByRole('button', { name: 'Reassign to…', exact: true })
    const choose = async name => {
      await target.click(); await page.getByRole('option', { name, exact: true }).click()
    }
    await choose('Synthetic destination B'); await choose('Synthetic destination C')
    assert.equal(await page.locator('[data-person-panel-trigger="' + f.ids.destinationB + '"]').count(), 0, 'B name is never paired with current C identity')
    await roundtrip(trigger(f.ids.source, heading), f.ids.source)
    denied = f.ids.source
    await trigger(f.ids.source, heading).click(); await sheet().getByRole('alert').waitFor()
    assert.equal(await sheet().locator('[data-person-panel]').count(), 0, 'Source denial renders no cached facts')
    await close(); await page.waitForURL(base + reassignUrl)
    await page.waitForFunction(id => document.activeElement?.id === id, await trigger(f.ids.source, heading).getAttribute('id'))
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    denied = null
    await roundtrip(trigger(f.ids.destinationC), f.ids.destinationC)
    assert.equal(await picks.nth(0).isChecked(), true); assert.equal(await picks.nth(1).isChecked(), true)
    assert.equal(await target.innerText(), 'Synthetic destination C')
    assert.equal(await page.getByRole('button', { name: 'Reassign 2', exact: true }).isEnabled(), true)
    denied = f.ids.destinationC
    await trigger(f.ids.destinationC).click(); await sheet().getByRole('alert').waitFor()
    assert.equal(await sheet().locator('[data-person-panel]').count(), 0)
    await close(); await page.waitForURL(base + reassignUrl); denied = null
    assert.equal(await picks.nth(0).isChecked(), true); assert.equal(await picks.nth(1).isChecked(), true)
    assert.equal(await target.innerText(), 'Synthetic destination C', 'Denied Person read preserves current reassignment target')
    assert.ok((await page.getByRole('link', { name: 'Synthetic selected task', exact: true }).first().getAttribute('href')).includes('panel=Task:' + f.ids.task))
    await unique('reassignment source and destination')
    observations.push('inactive source, same-task distinct roles, B to C target, source/target return and 404 preserve state without commands')

    off = true; f.actor.flags.people_screens = false
    const readsBefore = reads.length
    await goto('/search?q=synthetic&type=people')
    await page.getByText('Synthetic person without work access', { exact: true }).waitFor()
    assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0)
    await palette().getByRole('combobox', { name: 'Search', exact: true }).fill('synthetic')
    await popup().getByRole('listbox').waitFor()
    assert.equal(await palette().locator('[data-search-person-actions]').count(), 0)
    await goto(reassignUrl); await heading.waitFor()
    assert.equal(await page.locator('[data-person-panel-trigger]').count(), 0)
    assert.equal(reads.length, readsBefore, 'Flag off adds no Person read')
    assert.deepEqual(writes, [], 'Inspecting People never sends reassignment or another business command')
    assert.deepEqual(forbidden, [], 'No hours, planner or staff-scope API reads')
    assert.ok(reads.every(read => f.people.some(p => p.id === read.userId)), 'Only known User IDs reach the Person endpoint')
    assert.ok(searchReads.some(query => query.type === 'tasks' && query.page === '2' && query.includeArchived === 'true'), 'Actual archived second-page read occurred')
    assert.deepEqual(errors, []); assert.deepEqual(ui.mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'person-search-reassign', observations, personReads: reads.length, searchReads: searchReads.length, businessWrites: writes.length, forbiddenReads: forbidden.length }))
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
