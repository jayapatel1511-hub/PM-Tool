// AC-SCR-78 action/filter/follow only + AC-SCR-84. Delivery evidence and S47 are intentionally outside this synthetic driver.
const assert = require('node:assert/strict')
const path = require('node:path')
const { newMockPage, me } = require('./platform-fixtures.cjs')
const f = require('./notification-screen.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities } }
  // Reuse the existing isolated real-visibility browser, so focus/refetch behavior is not Playwright's emulated focus.
  const { browser, page, mock } = await newMockPage(base, { events: false, nativeVisibility: true })
  const read = new Set(), screenReads = [], oldReads = [], commands = [], unknown = [], errors = [], cases = []
  let level = 'AllActivity', followError = false, screenError = false, armedFollow = null, armedRead = null, cohort = 'all', stage = 'setup'
  page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false])); me.flags.people_screens = true; me.flags.screen_patterns = false
    me.capabilities = { ...saved.capabilities, workload: false }
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), p = url.pathname.replace('/api/v1/', '')
      const problem = (status, detail) => route.fulfill({ status, json: { code: 'synthetic_notification_refusal', detail } })
      if (request.method() !== 'GET') {
        if (p === 'me/sign-in') return route.fallback()
        const body = request.postDataJSON()
        if (request.method() === 'PUT' && p === `projects/${f.project.id}/follow` && armedFollow) {
          assert.deepEqual(body, { level: armedFollow }); commands.push({ method: 'PUT', path: p, body }); armedFollow = null
          if (followError) return problem(403, 'Synthetic follow change refused')
          level = body.level; return route.fulfill({ json: { level, source: 'Manual' } })
        }
        if (request.method() === 'POST' && p === 'me/notifications/read' && armedRead) {
          assert.deepEqual(body, { ids: [armedRead] }); commands.push({ method: 'POST', path: p, body }); read.add(armedRead); armedRead = null
          return route.fulfill({ json: { marked: 1 } })
        }
        unknown.push({ method: request.method(), path: p, body }); return problem(409, 'Unarmed notification command')
      }
      if (p === 'me/notifications/screen') {
        assert.equal(me.flags.people_screens, true); screenReads.push(Object.fromEntries(url.searchParams))
        assert.ok([...url.searchParams.keys()].every(key => ['unread', 'type', 'projectId', 'needsAction', 'page', 'pageSize'].includes(key)))
        if (screenError) return problem(403, 'Current notification screen read refused')
        return route.fulfill({ json: f.screen(url.searchParams, level, read, cohort) })
      }
      if (p === 'me/notifications') { oldReads.push(Object.fromEntries(url.searchParams)); return route.fulfill({ json: f.legacy(read) }) }
      if (p === 'me/notifications/pulse') return route.fulfill({ json: { stamp: `notices:${read.size}:${level}:${me.flags.people_screens}` } })
      if (p === 'me/notifications/unread-count') return route.fulfill({ json: { notifications: 5 - read.size, following: 0 } })
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects'].includes(p)) return route.fallback()
      unknown.push({ method: 'GET', path: p }); return problem(501, 'Unexpected notification read: ' + p)
    })
    const row = id => page.locator(`[data-notification-screen="${id}"]`)
    const follow = id => row(id).getByRole('button', { name: `Follow level for ${f.project.projectNumber}`, exact: true })
    const capture = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: path.join(process.env.TUESDAY_EVIDENCE_DIR, `${name}.png`), fullPage: true }) }
    const go = async () => { await page.goto(`${base}/notifications`); await page.getByText('5 notifications', { exact: true }).waitFor() }
    const screenResponse = predicate => page.waitForResponse(response => { const url = new URL(response.url()); return url.pathname === '/api/v1/me/notifications/screen' && response.status() === 200 && predicate(url.searchParams) })
    stage = 'current-cohort'; await go()
    assert.equal(await page.locator('[data-notification-screen]').count(), 5)
    await page.getByText('Action context unavailable (1).', { exact: true }).waitFor()
    assert.equal(await page.locator('[data-notification-delivery="Unknown"]').count(), 5)
    assert.equal(await page.getByText(/^Emailed |^Held: away$/).count(), 0)
    assert.deepEqual(commands, []); await capture('notification-screen-desktop')
    cases.push('Five current rows, explicit unknown context, honest unavailable email history; no inferred Emailed/Held or initial command')

    stage = 'filter'
    const filtered = screenResponse(query => query.get('needsAction') === 'true')
    await page.getByRole('button', { name: 'Needs your action', exact: true }).click(); await filtered
    await page.getByText('2 notifications', { exact: true }).waitFor()
    assert.equal(await page.locator('[data-notification-screen]').count(), 2)
    await row(f.ids.first).waitFor(); await row(f.ids.second).waitFor(); assert.equal(await row(f.ids.other).count(), 0)
    const typed = screenResponse(query => query.get('type') === 'TaskAssigned' && query.get('needsAction') === 'true')
    await page.getByLabel('Type', { exact: true }).selectOption('TaskAssigned'); await typed
    const scoped = screenResponse(query => query.get('projectId') === f.project.id && query.get('needsAction') === 'true')
    await page.getByLabel('Project', { exact: true }).selectOption(f.project.id); await scoped
    await page.getByText('2 notifications', { exact: true }).waitFor(); await capture('notification-screen-action-filter')
    cases.push('Needs-your-action/type/project combine through actual screen GET; two rows despite a collapsed notification count of three')

    stage = 'follow-and-focus'; const beforeUrl = page.url(), trigger = follow(f.ids.first)
    await trigger.click(); await page.getByRole('menuitemradio', { name: 'Muted', exact: true }).waitFor(); await page.keyboard.press('Escape')
    await page.waitForFunction(node => document.activeElement === node, await trigger.elementHandle())
    armedFollow = 'Muted'
    const savedFollow = page.waitForResponse(response => response.url().endsWith(`/api/v1/projects/${f.project.id}/follow`) && response.request().method() === 'PUT' && response.status() === 200)
    await trigger.click(); await page.getByRole('menuitemradio', { name: 'Muted', exact: true }).click(); await savedFollow
    await follow(f.ids.first).getByText('Muted', { exact: true }).waitFor()
    assert.equal(level, 'Muted'); assert.equal(page.url(), beforeUrl); assert.equal(commands.filter(command => command.path.endsWith('/read')).length, 0)
    assert.equal(commands.length, 1)
    cases.push('Follow menu is a sibling action; Escape restores natural source focus; exact own PUT persists after server reply with no read/navigation')

    stage = 'follow-refusal'; followError = true; armedFollow = 'AllActivity'
    await follow(f.ids.first).click(); await page.getByRole('menuitemradio', { name: 'All activity', exact: true }).click()
    await row(f.ids.first).getByRole('alert').getByText('Synthetic follow change refused', { exact: true }).waitFor()
    assert.equal(await follow(f.ids.first).count(), 0); assert.equal(level, 'Muted'); assert.equal(page.url(), beforeUrl)
    assert.equal(commands.filter(command => command.path.endsWith('/read')).length, 0); followError = false
    cases.push('Refused follow write keeps stored level, hides refused follow control and never marks read or navigates')

    stage = 'explicit-read-and-open'; await go(); armedRead = f.ids.first
    const marked = page.waitForResponse(response => response.url().endsWith('/api/v1/me/notifications/read') && response.status() === 200)
    await row(f.ids.first).getByRole('button', { name: 'Mark read: Synthetic first action', exact: true }).click(); await marked
    await row(f.ids.first).getByRole('button', { name: 'Mark read: Synthetic first action', exact: true }).waitFor({ state: 'hidden' })
    armedRead = f.ids.other
    const openedRead = page.waitForResponse(response => response.url().endsWith('/api/v1/me/notifications/read') && response.status() === 200)
    await row(f.ids.other).getByRole('button').filter({ has: page.getByText('Synthetic other actor', { exact: true }) }).click()
    await openedRead; await page.waitForURL(`${base}/notifications?opened=${f.ids.other}`)
    assert.equal(read.size, 2)
    cases.push('Existing Mark read and row-open/read are separate explicit actions; row open preserves the supplied deep link')

    stage = 'phone'; await page.setViewportSize({ width: 375, height: 812 }); await go()
    await follow(f.ids.second).scrollIntoViewIfNeeded(); await follow(f.ids.second).click()
    await page.getByRole('menuitemradio', { name: 'My items only', exact: true }).waitFor(); await page.keyboard.press('Escape')
    await page.waitForFunction(node => document.activeElement === node, await follow(f.ids.second).elementHandle())
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)); await capture('notification-screen-phone')
    cases.push('375px keeps usable sibling menu, natural focus and no page overflow')

    stage = 'unknown-empty'; cohort = 'unknown'
    await page.goto(`${base}/notifications?needsAction=true`)
    await page.getByText('No confirmed matches for Needs your action.', { exact: true }).waitFor()
    await page.getByText('Action context unavailable (1).', { exact: true }).waitFor()
    assert.equal(await page.getByText('No notifications currently need your action.', { exact: true }).count(), 0)
    cohort = 'knownOther'; await page.reload()
    await page.getByText('No notifications currently need your action.', { exact: true }).waitFor()
    assert.equal(await page.getByText('No confirmed matches for Needs your action.', { exact: true }).count(), 0)
    cohort = 'all'
    cases.push('Zero confirmed matches with unknown context stays explicitly unassessed; all-known zero retains the ordinary empty-action label')

    stage = 'same-key-background-refusal'; await go()
    // Let ordinary prior success-refresh timers finish before arming the independent background-refetch proof.
    await new Promise(resolve => setTimeout(resolve, 1600))
    await page.getByText('5 notifications', { exact: true }).waitFor(); await follow(f.ids.second).waitFor()
    const populatedUrl = page.url(), populatedQuery = { ...screenReads.at(-1) }, beforeRefetch = screenReads.length
    const refusedRead = page.waitForResponse(response => { const url = new URL(response.url()); return url.pathname === '/api/v1/me/notifications/screen'
      && response.status() === 403 && JSON.stringify(Object.fromEntries(url.searchParams)) === JSON.stringify(populatedQuery) }, { timeout: 30000 })
    screenError = true
    const away = await page.context().newPage()
    try {
      await away.goto('about:blank'); await away.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'hidden')
      // Wait through the real production15s stale window. No cache invalidation, synthetic visibility/focus event or app clock mutation.
      await new Promise(resolve => setTimeout(resolve, 15_100))
      assert.equal(screenReads.length, beforeRefetch)
      await page.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'visible'); await refusedRead
    } finally { await page.bringToFront(); await away.close() }
    await page.getByRole('alert').getByText('Current notification screen read refused', { exact: true }).waitFor()
    assert.equal(page.url(), populatedUrl); assert.deepEqual(screenReads.at(-1), populatedQuery)
    assert.equal(await page.locator('[data-notification-screen]').count(), 0); assert.equal(await page.getByText('5 notifications', { exact: true }).count(), 0)
    assert.equal(await page.getByRole('button', { name: `Follow level for ${f.project.projectNumber}`, exact: true }).count(), 0)
    screenError = false
    cases.push('Same populated query refetch403 after real tab visibility/freshness removes cached rows/count/follow controls with unchanged URL and query')

    stage = 'off'; me.flags.people_screens = false; await page.setViewportSize({ width: 1440, height: 1000 })
    const beforeOff = screenReads.length; await page.goto(`${base}/notifications`)
    await page.getByText('Synthetic first action', { exact: true }).waitFor()
    assert.equal(await page.getByRole('button', { name: 'Needs your action', exact: true }).count(), 0)
    assert.equal(await page.locator('[data-notification-screen], [data-notification-delivery]').count(), 0)
    assert.equal(screenReads.length, beforeOff); assert.ok(oldReads.length > 0)
    assert.equal(commands.length, 4); assert.equal(commands.filter(command => command.path.endsWith('/read')).length, 2)
    assert.deepEqual(unknown, []); assert.deepEqual(errors, []); assert.deepEqual(mock.unknownGets, [])
    cases.push('AC84: legacy personal list/rows remain off, with no new GET/filter/footer/delivery line and no extra command')
    console.log(JSON.stringify({ status: 'PASS', scope: 'Synthetic S45-A only; transport evidence and S47 remain open', cases, screenReads, oldReads, commands }))
  } catch (error) {
    console.error(JSON.stringify({ stage, url: page.url(), completedCases: cases, screenReads, oldReads, commands, unknown, errors })); throw error
  } finally { me.flags = saved.flags; me.capabilities = saved.capabilities; await browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
