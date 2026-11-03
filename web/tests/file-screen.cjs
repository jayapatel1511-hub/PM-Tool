// S44 AC-SCR-77 / AC-SCR-84. Actual-SPA contracts with synthetic reads; API tests own real scope/permissions/persistence/time predicates.
const assert = require('node:assert/strict')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./file-screen.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname), 'Use only the coordinator-owned local preview')

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui
  const capture = async name => {
    if (!process.env.TUESDAY_SCREENSHOT_DIR) return
    await page.evaluate(async () => { await document.fonts.ready; await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))) })
    await page.screenshot({ path: require('node:path').join(process.env.TUESDAY_SCREENSHOT_DIR, name), animations: 'disabled' })
  }
  const errors = [], writes = [], unexpectedMethods = [], unknownGets = [], reads = [], personReads = [], deliverableReads = [], observations = []
  let actor = f.actorFor(), responseMode = 'normal', refuseSharePoint = false
  const scope = f.ids.project + ',' + f.ids.secondProject, path = '/files?projects=' + scope
  try {
    page.setDefaultTimeout(15000)
    page.on('pageerror', error => errors.push(error.message))
    page.on('request', request => {
      const url = new URL(request.url())
      if (url.pathname.startsWith('/api/') && !['GET', 'HEAD', 'OPTIONS'].includes(request.method()) && !(request.method() === 'POST' && url.pathname === '/api/v1/me/sign-in')) writes.push(request.method() + ' ' + url.pathname)
    })
    const shellGets = new Set(['config', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'])
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), name = url.pathname.replace('/api/v1/', '')
      if (request.method() === 'POST' && name === 'me/sign-in') return route.fallback()
      if (request.method() !== 'GET') {
        unexpectedMethods.push(request.method() + ' ' + name)
        return route.fulfill({ status: 405, json: { detail: 'Files/Person inspection and Cancel permit no business commands' } })
      }
      if (name === 'me') return route.fulfill({ headers: { 'x-hub-build': 'mock-build-a' }, json: actor })
      if (name === 'reference') return route.fulfill({ json: f.reference })
      if (name === 'projects') return route.fulfill({ json: f.projectRows })
      const project = f.projects.find(project => name === 'projects/' + project.id || name === 'projects/' + project.projectNumber)
      if (project) return route.fulfill({ json: project })
      if (name === 'files') {
        const query = Object.fromEntries(url.searchParams)
        try {
          assert.ok(Object.keys(query).every(key => ['projects', 'q', 'type', 'disciplineId', 'addedById', 'addedThisWeek'].includes(key)), 'Unknown Files query field')
          if (refuseSharePoint && query.type === 'SharePoint') throw new Error('Synthetic current Files read refused after cached success')
          const data = f.read(url.searchParams, actor.flags.people_screens === true)
          if (responseMode === 'wrong-id' && data.screen?.links[0]) data.screen.links[0].linkId = f.ids.deliverable
          if (responseMode === 'missing-screen') delete data.screen
          reads.push({ query, status: 200, total: data.total, missing: data.screen?.missingDeliverables.total, applied: data.screen?.applied, mode: responseMode })
          return route.fulfill({ json: data })
        } catch (error) {
          reads.push({ query, status: 400, message: error.message })
          return route.fulfill({ status: 400, contentType: 'application/problem+json', json: { status: 400, title: 'Invalid Files read', code: 'invalid', detail: error.message } })
        }
      }
      if (/^people\/[^/]+\/panel$/.test(name)) {
        const userId = name.split('/')[1]
        if (![f.ids.author, f.ids.namesake, f.ids.registrar].includes(userId)) {
          unknownGets.push(name); return route.fulfill({ status: 501, json: { detail: 'Not a finite permitted Person read' } })
        }
        personReads.push({ userId, query: Object.fromEntries(url.searchParams) })
        if (userId === f.ids.namesake) return route.fulfill({ status: 404, contentType: 'application/problem+json', json: { status: 404, code: 'not_found', detail: 'This page or item does not exist, or you cannot view it.' } })
        return route.fulfill({ json: f.personRead(userId) })
      }
      if (name === 'deliverables/' + f.ids.missing) {
        deliverableReads.push(f.ids.missing); return route.fulfill({ json: f.deliverableRead(f.ids.missing) })
      }
      if (f.projects.some(project => name === 'projects/' + project.id + '/milestones')) return route.fulfill({ json: [] })
      if (name === 'items/Deliverable/' + f.ids.missing + '/links') return route.fulfill({ json: { canAdd: false, links: [], inherited: [] } })
      if (name === 'items/Deliverable/' + f.ids.missing + '/comments') return route.fallback()
      if (shellGets.has(name)) return route.fallback()
      unknownGets.push(name)
      return route.fulfill({ status: 501, json: { detail: 'Unexpected GET in focused Files proof: ' + name } })
    })
    const goto = value => page.goto(base + value)
    const links = () => page.locator('#content [data-file-link]')
    const link = id => page.locator('#content [data-file-link="' + id + '"]')
    const missing = () => page.locator('#files-missing [data-missing-deliverable]')
    const counts = async (total, absent) => {
      await page.waitForFunction(({ total, absent }) => document.querySelectorAll('#content [data-file-link]').length === total
        && document.querySelector('#files-missing h2 > span')?.textContent?.trim() === String(absent), { total, absent })
    }
    const filters = '#files-type,#files-discipline,#files-author,#files-week'
    const openFilters = async (keyboard = false) => {
      const phone = await page.evaluate(() => matchMedia('(max-width: 1023.98px)').matches)
      const trigger = page.locator('#content [data-slot="' + (phone ? 'sheet-trigger' : 'popover-trigger') + '"]').filter({ hasText: /^Filters/ })
      await trigger.waitFor({ state: 'visible' })
      if (await trigger.getAttribute('aria-expanded') !== 'true') {
        if (keyboard) { await trigger.focus(); await page.keyboard.press('Enter') } else await trigger.click()
      }
      const content = page.locator('[data-slot="' + (phone ? 'sheet-content' : 'popover-content') + '"][data-state="open"]').filter({ has: page.locator('#files-type') })
      await content.waitFor({ state: 'visible' }); assert.equal(await trigger.getAttribute('aria-expanded'), 'true')
      await content.locator('#files-type').waitFor({ state: 'visible' })
      return content
    }
    const closeFilters = async () => {
      const mounted = await page.locator(filters).all()
      assert.ok(mounted.length > 0)
      await page.keyboard.press('Escape'); await Promise.all(mounted.map(field => field.waitFor({ state: 'hidden' })))
    }
    const choose = async (field, value) => {
      await page.locator(field).selectOption(value)
      const key = { '#files-type': 'type', '#files-discipline': 'disciplineId', '#files-author': 'addedById' }[field]
      await page.waitForURL(url => value ? url.searchParams.get(key) === value : !url.searchParams.has(key))
    }
    const clear = async () => {
      await page.getByRole('group', { name: 'Active filters', exact: true }).getByRole('button', { name: 'Clear filters', exact: true }).click()
      await page.waitForURL(url => ['q', 'type', 'disciplineId', 'addedById', 'addedThisWeek'].every(key => !url.searchParams.has(key)))
      await page.getByRole('group', { name: 'Active filters', exact: true }).waitFor({ state: 'hidden' })
      assert.equal(new URL(page.url()).searchParams.get('projects'), scope)
    }
    const position = source => source.evaluate(node => {
      const surface = node.closest('[data-slot="sheet-content"],[data-slot="dialog-content"]') ?? document.getElementById('content')
      return { slot: surface?.getAttribute('data-slot') ?? surface?.id, top: surface?.scrollTop ?? 0, left: surface?.scrollLeft ?? 0 }
    })
    const personReturn = async (source, userId, { nested = false, denied = false, scroll = false } = {}) => {
      assert.equal(await source.count(), 1)
      await source.scrollIntoViewIfNeeded(); await source.focus()
      const original = page.url(), focusId = await source.getAttribute('id'), before = await position(source)
      if (scroll) assert.ok(before.top > 0, 'Actual source scroll must be nonzero')
      await page.keyboard.press('Enter'); await page.waitForURL(url => url.searchParams.get('panel') === 'Person:' + userId)
      if (denied) {
        await page.getByText('This page or item does not exist, or you cannot view it.', { exact: true }).last().waitFor()
        assert.equal(await page.locator('[data-person-panel]').count(), 0)
      } else {
        await page.locator('[data-person-panel="' + userId + '"]').waitFor()
        if (nested) {
          const supervisor = page.locator('[data-person-panel="' + userId + '"] [data-person-panel-trigger="' + f.ids.registrar + '"]')
          await supervisor.focus(); const nestedId = await supervisor.getAttribute('id'), parent = page.url()
          await page.keyboard.press('Enter'); await page.locator('[data-person-panel="' + f.ids.registrar + '"]').waitFor()
          assert.equal(await page.locator('[data-person-panel="' + userId + '"]').count(), 0)
          await page.keyboard.press('Escape'); await page.waitForURL(parent)
          await page.waitForFunction(id => document.activeElement?.id === id, nestedId)
        }
      }
      await page.keyboard.press('Escape'); await page.waitForURL(original)
      await page.waitForFunction(id => document.activeElement?.id === id, focusId)
      const after = await position(page.locator('[id=' + JSON.stringify(focusId) + ']'))
      assert.equal(after.slot, before.slot); assert.ok(Math.abs(after.top - before.top) <= 2); assert.ok(Math.abs(after.left - before.left) <= 2)
      const triggerIds = await page.locator('#content [data-person-panel-trigger][id]').evaluateAll(nodes => nodes.map(node => node.id))
      assert.equal(triggerIds.length, new Set(triggerIds).size, 'Separate author and registrar occurrences have stable unique source IDs')
      assert.equal(await page.locator('button [data-person-panel-trigger],a [data-person-panel-trigger],label [data-person-panel-trigger]').count(), 0)
    }
    await goto(path); await counts(9, 4)
    await link(f.ids.declared).getByText('Declared C, current D', { exact: false }).waitFor()
    await capture('files-desktop1.png')
    assert.ok((await link(f.ids.declared).innerText()).toLowerCase().includes('manually registered'))
    assert.equal(await link(f.ids.declared).locator('#file-reference-' + f.ids.declared).count(), 1)
    assert.equal(await link(f.ids.equal).getByRole('button', { name: 'Register reference', exact: true }).count(), 0, 'No edit right inferred from a matching revision')
    assert.ok((await link(f.ids.equal).innerText()).includes('Declared D'))
    for (const changed of [f.ids.parentAdded, f.ids.parentRemoved]) {
      assert.ok((await link(changed).innerText()).includes('changed'))
      assert.equal(await link(changed).getByText('Declared C, current D', { exact: false }).count(), 0, 'Changed parents never inherit a current revision')
    }
    assert.ok((await link(f.ids.unavailable).innerText()).includes('unavailable'))
    assert.ok((await link(f.ids.secondLink).innerText()).includes('not recorded'))
    assert.equal(await link(f.ids.orphanLink).locator('[data-person-panel-trigger]').count(), 0, 'Unknown author remains text')
    assert.equal(await missing().count(), 4)
    for (const id of [f.ids.missing, f.ids.taskOnly, f.ids.deletedLink, f.ids.secondMissing]) assert.equal(await page.locator('[data-missing-deliverable="' + id + '"]').count(), 1)
    assert.equal(await page.locator('[data-missing-deliverable="' + f.ids.deliverable + '"]').count(), 0, 'A mismatch is still a live direct link')
    const external = link(f.ids.declared).getByRole('link')
    assert.equal(await external.getAttribute('href'), 'https://example.test/synthetic/' + f.ids.declared)
    assert.equal(await external.getAttribute('target'), '_blank'); assert.match(await external.getAttribute('rel'), /noopener/)
    assert.equal(await link(f.ids.orphanLink).getByRole('link').count(), 0)
    assert.equal(await link(f.ids.orphanLink).getByRole('button', { name: 'Copy path', exact: true }).count(), 1)
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], { origin: new URL(base).origin })
    await link(f.ids.orphanLink).getByRole('button', { name: 'Copy path', exact: true }).click()
    assert.equal(await page.evaluate(() => navigator.clipboard.readText()), '\\\\files.test\\synthetic\\valve')
    observations.push('AC77: manual C/D mismatch, neutral/unknown/changed-parent states, original HTTP/UNC actions, direct-link missing cohort and rights')

    const beforeTyping = reads.length
    await page.locator('#files-search').fill('task-only')
    assert.equal(reads.length, beforeTyping, 'A draft query does not fetch per keystroke')
    await page.locator('#files-search').press('Enter'); await page.waitForURL(url => url.searchParams.get('q') === 'task-only'); await counts(1, 1)
    assert.equal(await page.locator('[data-missing-deliverable="' + f.ids.taskOnly + '"]').count(), 1)
    await clear(); await counts(9, 4); assert.equal(await page.locator('#files-search').inputValue(), '')
    await page.locator('#files-search').fill('linked drawing'); await page.locator('#files-search').press('Enter')
    await page.waitForURL(url => url.searchParams.get('q') === 'linked drawing'); await counts(0, 0)
    assert.equal(await page.locator('[data-missing-deliverable="' + f.ids.deliverable + '"]').count(), 0, 'A live direct link filtered out by search still satisfies the anti-join')
    await clear(); await counts(9, 4)

    await openFilters(); await choose('#files-discipline', f.ids.civil); await counts(7, 4)
    await choose('#files-author', f.ids.author); await counts(3, 4)
    await page.locator('#files-week').click(); await page.waitForURL(url => url.searchParams.get('addedThisWeek') === 'true'); await counts(3, 4)
    await choose('#files-type', 'SharePoint'); await counts(1, 4)
    assert.equal(await link(f.ids.declared).count(), 1)
    await page.getByText('Type, added-by and week filters apply to links.', { exact: false }).waitFor()
    await page.getByText(/America\/Halifax/).waitFor()
    await closeFilters()
    await page.getByRole('button', { name: 'Remove filter: Added by', exact: true }).click()
    await page.waitForURL(url => !url.searchParams.has('addedById')); await counts(1, 4)
    await clear(); await counts(9, 4)
    await openFilters(); await choose('#files-author', f.ids.namesake); await counts(4, 4)
    await page.locator('#files-week').click(); await page.waitForURL(url => url.searchParams.get('addedThisWeek') === 'true'); await counts(3, 4)
    await closeFilters(); await clear(); await counts(9, 4)
    observations.push('AC77: global discipline across projects, identical-name actual authors, original added-week/org dates, combined filters/tokens/Clear; missing membership independent of link filters')

    await page.setViewportSize({ width: 375, height: 740 })
    const phoneFilters = await openFilters(true)
    await capture('files-filters-phone1.png')
    await phoneFilters.locator('#files-type').focus(); await page.keyboard.press('Tab')
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'files-discipline')
    await page.keyboard.press('Tab'); assert.equal(await page.evaluate(() => document.activeElement?.id), 'files-author')
    await choose('#files-discipline', f.ids.civil); await counts(7, 4); await closeFilters()
    await personReturn(link(f.ids.declared).locator('[data-person-panel-trigger="' + f.ids.author + '"]'), f.ids.author, { nested: true })
    await personReturn(link(f.ids.declared).locator('[data-person-panel-trigger="' + f.ids.registrar + '"]'), f.ids.registrar)
    await personReturn(link(f.ids.secondLink).locator('[data-person-panel-trigger="' + f.ids.namesake + '"]'), f.ids.namesake, { denied: true, scroll: true })
    assert.equal(new URL(page.url()).searchParams.get('disciplineId'), f.ids.civil)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Phone content fits375px')
    const source = page.locator('#files-missing-' + f.ids.missing), original = page.url()
    await source.scrollIntoViewIfNeeded(); await source.focus(); await page.keyboard.press('Enter')
    await page.waitForURL(url => url.searchParams.get('panel') === 'Deliverable:' + f.ids.missing)
    await page.getByRole('dialog', { name: 'Deliverable', exact: true }).getByRole('heading', { name: 'Synthetic valve missing drawing', exact: true }).waitFor()
    await page.keyboard.press('Escape'); await page.waitForURL(original)
    await page.waitForFunction(id => document.activeElement?.id === id, 'files-missing-' + f.ids.missing)
    const writableMissing = page.locator('[data-missing-deliverable="' + f.ids.missing + '"]')
    await writableMissing.getByRole('button', { name: 'Add link', exact: true }).click()
    await writableMissing.locator('[data-document-link-add="Deliverable:' + f.ids.missing + '"]').waitFor()
    await writableMissing.getByRole('textbox', { name: 'Address or network path', exact: true }).waitFor()
    await writableMissing.getByRole('button', { name: 'Cancel', exact: true }).click()
    assert.deepEqual(writes, [], 'Inspect/Person/Cancel never writes a link or acknowledges work')
    observations.push('AC77/84:375px natural filter order, actual author/registrar/nested/404 URL-scroll-focus return, actual Deliverable ID drawer and missing-row AddLink Cancel')
    await page.setViewportSize({ width: 1440, height: 1000 }); await clear(); await counts(9, 4)

    for (const [parameter, value] of [['disciplineId', 'bad-id'], ['addedById', 'bad-id'], ['addedThisWeek', 'yes']]) {
      await goto(path + '&' + parameter + '=' + value)
      await page.getByRole('alert').getByText('Synthetic invalid ' + parameter, { exact: true }).waitFor()
      assert.equal(await links().count(), 0); assert.equal(await missing().count(), 0)
      await clear(); await counts(9, 4)
    }
    await goto(path + '&addedById=' + f.ids.unknown); await counts(0, 4)
    await openFilters(); assert.equal(await page.locator('#files-author option:checked').innerText(), 'Selected author ID: ' + f.ids.unknown); await closeFilters()
    await clear(); await counts(9, 4)
    await goto(path + '&disciplineId=' + f.ids.unknown); await counts(0, 0)
    await clear(); await counts(9, 4)
    await goto('/files?projects=' + f.ids.hiddenProject); await counts(0, 0)
    await openFilters()
    assert.equal(await page.locator('#files-author option').count(), 1); assert.equal(await page.locator('#files-discipline option').count(), 1)
    await closeFilters(); await goto(path); await counts(9, 4)
    for (const mode of ['wrong-id', 'missing-screen']) {
      responseMode = mode; await goto(path)
      await page.getByRole('alert').getByText('Reference context is unavailable.', { exact: true }).waitFor()
      assert.equal(await links().count(), 0); assert.equal(await missing().count(), 0)
      assert.equal(await page.locator('#content [data-person-panel-trigger]').count(), 0)
    }
    responseMode = 'normal'; await goto(path); await counts(9, 4)
    observations.push('AC77: malformed controls refuse; unknown IDs remain removable without guessed labels; missing/misaligned response context withholds figures/rows/Person rights')

    await openFilters(); await choose('#files-type', 'SharePoint'); await counts(2, 4)
    const populatedAt = await page.evaluate(() => Date.now())
    await choose('#files-type', 'Other'); await counts(6, 4)
    await page.waitForFunction(since => Date.now() - since > 15_100, populatedAt, { timeout: 20000 }) // Normal main.tsx staleTime; no fake clock/cache/focus.
    refuseSharePoint = true
    const refused = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/files' && new URL(response.url()).searchParams.get('type') === 'SharePoint' && response.status() === 400)
    await choose('#files-type', 'SharePoint'); await refused; await closeFilters()
    await page.getByRole('alert').getByText('Synthetic current Files read refused after cached success', { exact: true }).waitFor()
    assert.equal(await links().count(), 0); assert.equal(await missing().count(), 0)
    assert.equal(await page.locator('#content [data-person-panel-trigger]').count(), 0)
    await clear(); await counts(9, 4); refuseSharePoint = false
    observations.push('AC77/84: actual stale QueryClient key refusal hides cached rows/mismatch/counts/rights, valid Clear recovers')

    await goto('/files?projects=' + f.ids.crowdedProject + '&addedById=' + f.ids.externalAuthor + '&disciplineId=' + f.ids.externalDiscipline)
    await counts(1, 0); await openFilters()
    assert.equal(await page.locator('#files-author option:checked').innerText(), 'Selected author ID: ' + f.ids.externalAuthor)
    assert.equal(await page.locator('#files-discipline option:checked').innerText(), 'Selected discipline ID: ' + f.ids.externalDiscipline)
    await page.locator('#files-author-limit').waitFor(); await page.locator('#files-discipline-limit').waitFor()
    assert.equal(await page.locator('#files-author option').count(), 202); assert.equal(await page.locator('#files-discipline option').count(), 202)
    await closeFilters()
    await goto('/files?projects=' + f.ids.crowdedProject); await counts(201, 2001)
    assert.equal(await missing().count(), 2000)
    await page.getByText('Showing 2000 of 2001 deliverables without a direct link.', { exact: true }).waitFor()
    observations.push('AC77: selected IDs beyond200 choices retained truthfully; separate exact missing total2001, bounded2000 rows and visible truncation')

    const readsBeforeOff = reads.length, personBeforeOff = personReads.length
    actor = f.actorFor(false)
    await page.setViewportSize({ width: 375, height: 740 })
    await goto(path + '&disciplineId=bad-id&addedById=bad-id&addedThisWeek=bad')
    await page.waitForFunction(() => document.querySelectorAll('#content [data-file-link]').length === 9)
    assert.equal(await page.locator('#files-missing').count(), 0)
    assert.equal(await page.locator('#content [data-person-panel-trigger]').count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Register reference', exact: true }).count(), 0)
    assert.equal(await page.getByText('Declared C, current D', { exact: false }).count(), 0)
    await openFilters()
    assert.equal(await page.locator('#files-discipline,#files-author,#files-week').count(), 0)
    await choose('#files-type', 'SharePoint')
    await page.waitForFunction(() => document.querySelectorAll('#content [data-file-link]').length === 2)
    await closeFilters()
    assert.ok(reads.slice(readsBeforeOff).every(read => !read.query.disciplineId && !read.query.addedById && !read.query.addedThisWeek && read.missing === undefined))
    assert.equal(personReads.length, personBeforeOff)
    assert.ok(personReads.every(read => [f.ids.author, f.ids.namesake, f.ids.registrar].includes(read.userId) && !read.query.staffScope))
    assert.deepEqual(deliverableReads, [f.ids.missing]); assert.deepEqual(writes, []); assert.deepEqual(unexpectedMethods, []); assert.deepEqual(unknownGets, []); assert.deepEqual(ui.mock.unknownGets, []); assert.deepEqual(errors, [])
    observations.push('AC84: real flag-off legacy reply/request/375px type control, no enhanced metadata/missing/Person actions or business commands')
    console.log(JSON.stringify({ status: 'PASS', suite: 'file-screen', evidence: 'synthetic actual-SPA UI only; real scope/metadata/permissions/time/persistence remain provider API-owned', observations,
      fileReads: reads.length, personReads: personReads.length, deliverableReads: deliverableReads.length, businessWrites: writes.length, unexpectedGets: unknownGets.length }))
  } catch (error) {
    try {
      const actual = await page.evaluate(() => ({ url: location.href.slice(0, 2000), input: document.getElementById('files-search')?.value?.slice(0, 200) ?? null,
        links: document.querySelectorAll('#content [data-file-link]').length, missingRows: document.querySelectorAll('[data-missing-deliverable]').length,
        missingTotal: document.querySelector('#files-missing h2 > span')?.textContent?.trim().slice(0, 100) ?? null,
        activeFilters: document.querySelector('[role="group"][aria-label="Active filters"]')?.textContent?.trim().slice(0, 1000) ?? null }))
      console.error(JSON.stringify({ status: 'FAIL', suite: 'file-screen', actual, recentReads: reads.slice(-12), personReads: personReads.slice(-8), observations: observations.slice(-8) }))
    } catch (diagnosticError) { console.error(JSON.stringify({ status: 'FAIL', suite: 'file-screen', diagnosticError: String(diagnosticError).slice(0, 1000), url: page.url().slice(0, 2000), recentReads: reads.slice(-12) })) }
    throw error
  } finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
