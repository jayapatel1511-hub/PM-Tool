// Synthetic R1 widget proof only; root supplies the verified local build. Never start services or claim command/API authority.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { reviewRegisterFixture, id } = require('./review-register.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const f = reviewRegisterFixture(project, me.id)
const rootUrl = `${base}/projects/${project.projectNumber}/reviews`
const packageQuery = 'q=Retained&status=In+Review&page=1'
const startUrl = `${rootUrl}?${packageQuery}`

;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities }, settings: { ...me.settings } }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const errors = [], writes = [], unexpectedGets = [], reads = [], locators = [], detailReads = [], personReads = [], cases = []
  let missingFinding = null, readError = false
  page.setDefaultTimeout(15000); page.on('pageerror', e => errors.push(e.message))
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { register_screens: true, people_screens: true, data_entry_safety: true })
    me.capabilities = { ...saved.capabilities, workload: false, readOnly: false }
    me.settings.today = '2026-11-02'; me.settings.dateFormat = 'yyyy-MM-dd'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        writes.push({ method: request.method(), path, body: request.postData() })
        return route.fulfill({ status: 409, json: { code: 'unexpected_review_register_write', detail: 'This suite inspects only; no business command is allowed' } })
      }
      if (path === `projects/${project.id}/review-findings`) {
        reads.push(Object.fromEntries(url.searchParams))
        if (!me.flags.register_screens) return route.fulfill({ status: 404, json: { code: 'not_found' } })
        if (readError) return route.fulfill({ status: 403, json: { code: 'forbidden', detail: 'Synthetic register read refusal' } })
        return route.fulfill({ json: f.page(url.searchParams) })
      }
      if (path.startsWith(`projects/${project.id}/review-findings/`)) {
        const findingId = path.split('/').at(-1), row = f.all.find(row => row.id === findingId)
        if ((!me.flags.register_screens && !me.flags.screen_patterns) || !row || findingId === missingFinding) {
          locators.push({ id: findingId, status: 404 })
          return route.fulfill({ status: 404, contentType: 'application/problem+json', json: { title: 'Not found', status: 404, code: 'not_found', detail: 'Exact finding unavailable', traceId: 'synthetic-r1-404' } })
        }
        const response = f.locator(row, me.flags.screen_patterns === true)
        locators.push({ id: row.id, packageId: row.packageId, roundId: row.roundId, context: 'context' in response, status: 200 })
        return route.fulfill({ json: response })
      }
      if (path === `projects/${project.id}/reviews`) return route.fulfill({ json: f.packages(Number(f.value(url.searchParams, 'page')) || 1) })
      if (path === `projects/${project.id}/reviews/discipline-summary`) {
        if (!me.flags.register_screens) { unexpectedGets.push(path); return route.fulfill({ status: 404, json: { code: 'not_found' } }) }
        assert.deepEqual([...url.searchParams.keys()], [], 'Discipline summary takes no finding or package-list filters')
        return route.fulfill({ json: f.summary })
      }
      if (path === `projects/${project.id}/changes/options` || path === `projects/${project.id}/reviews/options`) return route.fulfill({ json: f.options })
      if (path === `projects/${project.id}/reviews/${f.packageA.id}` || path === `projects/${project.id}/reviews/${f.packageB.id}`) {
        const packageId = path.split('/').at(-1); detailReads.push(packageId); return route.fulfill({ json: f.detail(packageId) })
      }
      if (/^people\/[^/]+\/panel$/.test(path)) {
        const user = path.split('/')[1]; personReads.push(user)
        assert.ok(f.names.has(user), 'Unknown/ExternalParty IDs cannot be guessed into a User reference')
        return route.fulfill({ json: f.person(user, f.names.get(user)) })
      }
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count',
        `projects/${project.projectNumber}`, `projects/${project.id}`].includes(path)) return route.fallback()
      unexpectedGets.push(path); return route.fulfill({ status: 501, json: { code: 'unexpected_review_register_get', detail: path } })
    })
    const widget = () => page.locator(`[data-review-open-findings="${project.id}"]`)
    const frames = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const preservePackages = () => {
      const query = new URL(page.url()).searchParams
      assert.equal(query.get('q'), 'Retained'); assert.equal(query.get('status'), 'In Review'); assert.equal(query.get('page'), '1')
    }
    const expected = () => {
      const current = new URL(page.url()).searchParams, params = new URLSearchParams()
      for (const [from, to] of [['findingDisciplineId', 'disciplineId'], ['findingSeverity', 'severity'], ['findingOriginatorKind', 'originatorKind'],
        ['findingOriginatorId', 'originatorId'], ['findingResolverId', 'resolverId'], ['findingAllRounds', 'allRounds'], ['findingPage', 'page'], ['findingPageSize', 'pageSize']])
        if (current.has(from)) params.set(to, current.get(from))
      return f.page(params)
    }
    const answered = async () => {
      const data = expected()
      await widget().getByText(`${data.items.length} of ${data.totalCount} unresolved findings · ${data.allRounds ? 'All rounds' : 'Current rounds'}`, { exact: true }).waitFor()
      preservePackages(); return data
    }
    const go = async (query = '') => { await page.goto(startUrl + query); await widget().waitFor(); return answered() }
    // The Sheet hides its background trigger from accessibility; read its real DOM state in both layouts.
    const filterTrigger = () => widget().getByRole('button', { name: /^Filters/, includeHidden: true })
    const controls = async () => {
      const trigger = filterTrigger(), field = page.locator('#finding-discipline')
      if (await trigger.getAttribute('aria-expanded') !== 'true') {
        // A closing Popover/Sheet can remain visible after aria-expanded becomes false.
        await field.waitFor({ state: 'hidden' })
        await trigger.click()
      }
      await field.waitFor({ state: 'visible' })
      assert.equal(await trigger.getAttribute('aria-expanded'), 'true')
    }
    const closeControls = async () => {
      const trigger = filterTrigger()
      if (await trigger.getAttribute('aria-expanded') === 'true') await page.keyboard.press('Escape')
      await page.locator('#finding-discipline').waitFor({ state: 'hidden' })
      // Observe Radix's natural close/focus completion; never force focus to make the next action pass.
      await page.waitForFunction(button => button?.getAttribute('aria-expanded') === 'false' && document.activeElement === button, await trigger.elementHandle())
    }
    const choose = async (name, value) => {
      await controls(); await page.locator(`#finding-${name}`).selectOption(value)
      await answered()
      await closeControls()
      preservePackages()
    }
    const link = row => widget().locator(`[data-review-finding-open="${row.id}"]:visible`)
    const unique = async () => {
      const ids = await page.locator('[data-person-panel-trigger][id],[data-review-finding-open][id]').evaluateAll(nodes => nodes.map(n => n.id))
      assert.equal(ids.length, new Set(ids).size, 'All table/card/actor/row/round occurrences have unique stable IDs')
      assert.equal(await page.locator('a [data-person-panel-trigger],button [data-person-panel-trigger],label [data-person-panel-trigger],summary [data-person-panel-trigger],[role="option"] [data-person-panel-trigger]').count(), 0)
    }
    const inspect = async (row, options = {}) => {
      const source = link(row); assert.equal(await source.count(), 1); assert.equal(await source.getAttribute('aria-disabled'), null)
      await source.scrollIntoViewIfNeeded(); await source.focus(); await frames()
      const origin = page.url(), focus = await source.getAttribute('id'), before = await page.locator('#content').evaluate(n => ({ top: n.scrollTop, left: n.scrollLeft }))
      if (options.scroll) assert.ok(before.top > 0, 'The exact finding source has real nonzero list scroll')
      const countBefore = detailReads.length
      const refusal = options.missing ? page.waitForResponse(r => r.url().includes(`/review-findings/${row.id}`) && r.status() === 404) : null
      await page.keyboard.press('Enter'); await page.waitForURL(url => url.searchParams.get('panel') === `ReviewFinding:${row.id}` && url.searchParams.get('round') === row.roundId)
      const modal = () => page.getByRole('dialog').last()
      if (options.missing) {
        assert.equal((await refusal).status(), 404)
        await modal().getByRole('alert').waitFor()
        assert.equal(detailReads.length, countBefore, 'No missing-finding fallback reads a parent package')
        assert.equal(await modal().getByText(row.text, { exact: true }).count(), 0)
        assert.equal(await modal().getByRole('heading', { name: new RegExp(row.packageKey) }).count(), 0)
      } else {
        await modal().getByText(row.text, { exact: true }).last().waitFor()
        const other = f.all.find(item => item.packageId === row.packageId && item.id !== row.id)
        assert.equal(await modal().getByText(other.text, { exact: true }).count(), 0, 'Only the requested finding is rendered')
        assert.equal(await modal().getByText('Next action', { exact: true }).count(), me.flags.screen_patterns ? 1 : 0)
        if (options.person) {
          const native = modal().locator('details').first()
          await native.locator('summary').click(); assert.equal(await native.evaluate(n => n.open), true)
          const actor = modal().locator('dt').filter({ hasText: 'Independent verifier' }).locator('xpath=following-sibling::dd[1]').locator('[data-person-panel-trigger]')
          assert.equal(await actor.count(), 1); await actor.scrollIntoViewIfNeeded(); await actor.focus(); await frames()
          const findingUrl = page.url(), actorId = await actor.getAttribute('id')
          const personScroll = await actor.evaluate(n => (n.closest('[data-slot="dialog-body"]') ?? n.closest('[data-slot="dialog-content"]')).scrollTop)
          assert.ok(personScroll > 0, 'Exact finding detail is genuinely scrolled')
          await page.keyboard.press('Enter'); await page.locator(`[data-person-panel="${f.users.originA}"]`).waitFor()
          assert.equal(await page.locator(`[id=${JSON.stringify(actorId)}]`).count(), 0, 'The finding source really unmounts under Person')
          await page.keyboard.press('Escape'); await page.waitForURL(findingUrl)
          await page.waitForFunction(id => document.activeElement?.id === id, actorId)
          assert.equal(await modal().locator('details').first().evaluate(n => n.open), true, 'Natural return restores the real finding history fold')
          const restoredScroll = await page.locator(`[id=${JSON.stringify(actorId)}]`).evaluate(n => (n.closest('[data-slot="dialog-body"]') ?? n.closest('[data-slot="dialog-content"]')).scrollTop)
          assert.ok(Math.abs(restoredScroll - personScroll) <= 2)
        }
      }
      await page.keyboard.press('Escape'); await page.waitForURL(origin)
      await page.waitForFunction(id => document.activeElement?.id === id, focus)
      await frames()
      const after = await page.locator('#content').evaluate(n => ({ top: n.scrollTop, left: n.scrollLeft }))
      assert.ok(Math.abs(after.top - before.top) <= 2); assert.ok(Math.abs(after.left - before.left) <= 2)
      preservePackages(); await unique(); assert.deepEqual(writes, [])
    }

    await page.setViewportSize({ width: 1440, height: 1000 })
    const first = await go(); assert.equal(first.totalCount, 75); assert.equal(first.items.length, 50)
    assert.equal(await widget().getByRole('table').locator('thead th').count(), 6)
    const unknownNext = widget().getByRole('row').filter({ has: page.locator(`[data-review-finding-open="${f.current[1].id}"]`) }).locator('td').last()
    assert.ok((await unknownNext.innerText()).includes('Unknown'))
    assert.equal(await unknownNext.locator('[data-person-panel-trigger]').count(), 0, 'Unknown next actor is not replaced by a coordinator')
    const knownNext = widget().getByRole('row').filter({ has: page.locator(`[data-review-finding-open="${f.current[0].id}"]`) }).locator('td').last()
    assert.equal(await knownNext.locator(`[data-person-panel-trigger="${f.users.originA}"]`).count(), 1, 'The exact server next actor is retained')
    assert.ok((await knownNext.innerText()).includes('The next action belongs to someone else.'))
    assert.equal(await widget().locator(`[data-review-finding-open="${f.current[74].id}"]`).count(), 0)
    await controls()
    assert.equal(await page.locator(`#finding-resolver option[value="${f.users.lastResolver}"]`).count(), 1, 'Resolver outside page one remains an option')
    await closeControls()
    await widget().getByRole('navigation', { name: 'Finding pages', exact: true }).getByRole('button', { name: 'Next', exact: true }).click()
    await answered(); assert.equal(new URL(page.url()).searchParams.get('findingPage'), '2')
    await inspect(f.current[74], { scroll: true, person: true })
    cases.push('75 unresolved findings/two packages, full options/page totals, register-only exact round/finding/Person/fold/scroll/focus return')

    for (const [field, value, expectedCount] of [['discipline', f.discB, 30], ['severity', 'Blocking', 30], ['originator', `User:${f.users.originB}`, 30], ['resolver', f.users.lastResolver, 1]]) {
      await go(); await choose(field, value); assert.equal(expected().totalCount, expectedCount)
      assert.equal(new URL(page.url()).searchParams.has('findingPage'), false)
    }
    await go(); await choose('discipline', f.discB); await choose('severity', 'Blocking'); await choose('originator', `User:${f.users.originB}`); await choose('resolver', f.users.resolverB)
    assert.equal(expected().totalCount, 29)
    assert.ok(reads.some(query => f.value(new URLSearchParams(query), 'disciplineId') === f.discB && f.value(new URLSearchParams(query), 'severity') === 'Blocking'
      && f.value(new URLSearchParams(query), 'originatorId') === f.users.originB && f.value(new URLSearchParams(query), 'resolverId') === f.users.resolverB))
    cases.push('Each and combined four filters AND correctly; finding-only paging changes preserve package q/status/page')

    await go()
    const allRoundsToggle = widget().getByRole('checkbox', { name: /^All rounds/ })
    let allRoundsObserved = null
    try {
      const response = page.waitForResponse(r => r.request().method() === 'GET'
        && new URL(r.url()).pathname === `/api/v1/projects/${project.id}/review-findings`
        && f.value(new URL(r.url()).searchParams, 'allRounds') === 'true')
        .then(async r => ({ status: r.status(), body: await r.json() })).catch(error => ({ error: error.message }))
      await allRoundsToggle.click()
      await page.waitForURL(url => url.searchParams.get('findingAllRounds') === 'true')
      await page.waitForFunction(input => input?.checked === true, await allRoundsToggle.elementHandle())
      allRoundsObserved = await response
      assert.equal(allRoundsObserved.status, 200, allRoundsObserved.error)
      assert.equal(allRoundsObserved.body.allRounds, true)
      assert.equal(allRoundsObserved.body.totalCount, 77)
      await answered()
    } catch (error) {
      const state = await widget().count() ? await widget().evaluate(section => ({
        checked: section.querySelector('input[type="checkbox"]')?.checked ?? null,
        summary: [...section.querySelectorAll('[role="status"]')].map(node => node.textContent),
      })) : { checked: null, summary: [] }
      console.error(JSON.stringify({ stage: 'review-register-all-rounds', url: page.url(), ...state, observedResponse: allRoundsObserved }))
      throw error
    }
    assert.equal(expected().totalCount, 77)
    await widget().locator('p:visible').filter({ hasText: 'Carried from an earlier round' }).first().waitFor()
    await widget().locator('p:visible').filter({ hasText: 'Awaiting withdrawal acknowledgement' }).first().waitFor()
    await widget().locator('p:visible').filter({ hasText: 'Earlier round · read only' }).first().waitFor()
    await widget().locator('p:visible').filter({ hasText: 'Package read only' }).first().waitFor()
    await inspect(f.history[0])
    assert.ok(locators.some(row => row.id === f.history[0].id && row.roundId === f.history[0].roundId && !row.context))
    await go(); assert.equal(expected().totalCount, 75)
    assert.equal(await widget().locator(`[data-review-finding-open="${f.history[0].id}"]`).count(), 0)
    await widget().locator('p:visible').filter({ hasText: 'Source D-A · revision B · Source unavailable' }).first().waitFor()
    assert.equal(await widget().locator(`[data-person-panel-trigger="${f.users.unknown}"]`).count(), 0)
    cases.push('Current excludes old carry; all-rounds adds exact history, pending acknowledgement and explicit terminal/history/unknown provenance states')

    await go(`&findingOriginatorKind=ExternalParty&findingOriginatorId=${id(9199)}`)
    assert.equal(expected().totalCount, 0)
    assert.equal(await widget().locator('[data-person-panel-trigger]').count(), 0, 'ExternalParty selection never routes to Person')
    cases.push('Explicit external originator discriminator remains empty and never becomes a User')

    await go('&findingPage=2'); missingFinding = f.current[74].id
    await inspect(f.current[74], { missing: true, scroll: true }); missingFinding = null
    readError = true; await page.goto(startUrl + '&findingSeverity=Blocking'); await widget().getByRole('alert').waitFor()
    assert.equal(await widget().getByRole('table').count(), 0, 'A refused read does not masquerade as an empty or stale successful list')
    readError = false
    cases.push('Answered missing finding never falls back to a cached package; register error is honest and retryable')

    await page.setViewportSize({ width: 375, height: 700 }); await go('&findingPage=2')
    assert.equal(await widget().getByRole('table').isVisible(), false)
    await inspect(f.current[74], { scroll: true })
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 2), false)
    cases.push('375px cards keep keyboard inspection, exact return and no page overflow')

    me.flags.screen_patterns = true; await go(); await inspect(f.current[1])
    assert.ok(locators.some(row => row.id === f.current[1].id && row.context))
    me.flags.register_screens = false
    const readsBeforeOff = reads.length
    await page.goto(startUrl + `&panel=ReviewFinding:${f.history[0].id}&round=${f.history[0].roundId}`)
    await page.getByRole('dialog').getByText(f.history[0].text, { exact: true }).last().waitFor()
    assert.equal(await widget().count(), 0); assert.equal(reads.length, readsBeforeOff, 'Pattern-only keeps exact inspection without loading the register')
    await page.keyboard.press('Escape')
    me.flags.screen_patterns = false
    const locatorsBeforeDark = locators.length
    await page.goto(startUrl + `&panel=ReviewFinding:${f.history[0].id}&round=${f.history[0].roundId}`)
    await page.getByRole('heading', { name: 'Multidisciplinary reviews', exact: true }).waitFor(); await frames()
    assert.equal(await widget().count(), 0); assert.equal(await page.getByRole('dialog').count(), 0)
    assert.equal(locators.length, locatorsBeforeDark); assert.equal(reads.length, readsBeforeOff)
    assert.equal(personReads.includes(f.users.unknown), false)
    assert.deepEqual(writes, []); assert.deepEqual(unexpectedGets, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(errors, [])
    cases.push('Register-only/both-on/pattern-only/both-off independently gate list and exact locator; no business/Seen command')
    console.log(JSON.stringify({ status: 'PASS', scope: 'synthetic R1 finding-register UI regression only; R2 responses/exports and external intake have separate verification', cases,
      registerReads: reads.length, exactLocators: locators, detailReads, personReads, businessWrites: writes.length }))
  } finally {
    me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
