// FUTURE actual-SPA UI proof. UNRUN until Root mounts Preferences after complete delivery integration.
// No test-only mount; synthetic public receipts are not a provider/transport/privacy authorization oracle.
const assert = require('node:assert/strict')
const { join } = require('node:path')
const { newMockPage } = require('./platform-fixtures.cjs')
const { unreadCount } = require('./notifications.fixture.cjs')
const f = require('./notice-delivery.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
;(async () => {
  const ui = await newMockPage(base, { events: false }), { page } = ui, source = f.world()
  const errors = [], unexpected = [], commands = [], screenReads = [], pulseReads = [], recentReads = [], observations = []
  if (process.env.TUESDAY_REVIEW_VISUAL === '1') source.flags.visual_system = true
  const shell = new Set(['config', 'reference', 'workspaces', 'views', 'projects', 'events'])
  let screenFailure = 0, recentFailure = 0, expectedPreference = null, acceptPreference = null
  const screenReply = status => page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/notifications/screen' && response.status() === status)
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), name = url.pathname.replace('/api/v1/', ''), method = request.method()
      const problem = (status, detail) => route.fulfill({ status, contentType: 'application/problem+json', json: { status, code: 'synthetic_delivery_refusal', detail } })
      if (method === 'POST' && name === 'me/sign-in') return route.fallback()
      if (method === 'GET') {
        if (name === 'me') return route.fulfill({ json: source.actor })
        if (name === 'me/preferences') return route.fulfill({ json: source.legacy })
        if (name === 'me/notice-preferences') return route.fulfill({ json: source.current() })
        if (name === 'me/notifications/pulse') { const value = source.pulse(); pulseReads.push(value.stamp); return route.fulfill({ json: value }) }
        if (name === 'me/notifications/unread-count') return route.fulfill({ json: unreadCount(0, 0) })
        if (name === 'me/notifications') {
          const status = url.searchParams.get('pageSize') === '8' ? recentFailure || 200 : 200
          recentReads.push({ status, query: Object.fromEntries(url.searchParams) })
          return status === 200 ? route.fulfill({ json: source.legacyNotices(url.searchParams) }) : problem(status, 'Synthetic recent notification refresh refused')
        }
        if (name === 'me/notifications/screen') {
          assert.equal(source.flags.people_screens, true)
          assert.ok([...url.searchParams.keys()].every(key => ['unread', 'type', 'projectId', 'needsAction', 'page', 'pageSize'].includes(key)))
          screenReads.push({ status: screenFailure || 200, version: source.version(), query: Object.fromEntries(url.searchParams) })
          if (screenFailure) return problem(screenFailure, 'Synthetic current delivery history refused')
          const value = source.screen(url.searchParams)
          // Adversarial extra fields deliberately violate the public DTO; the UI must not enumerate/render them.
          const items = value.items.map(entry => ({ ...entry, screen: { ...entry.screen, delivery: { ...entry.screen.delivery,
            privateIntentId: f.privateCanary, privateProof: { origin: f.privateCanary }, providerMessageId: f.privateCanary } } }))
          return route.fulfill({ json: { ...value, items } })
        }
        if (shell.has(name)) return route.fallback()
        unexpected.push(method + ' ' + name); return problem(501, 'Unexpected delivery GET')
      }
      const body = request.postDataJSON(); commands.push({ method, name, body })
      if (method === 'PATCH' && name === 'me/notice-preferences') {
        assert.ok(expectedPreference); assert.deepEqual(body, expectedPreference); expectedPreference = null
        assert.equal(request.headers()['if-match'], undefined); assert.equal(body.revision, source.current().revision)
        const result = source.apply(body)
        await new Promise(release => { assert.ok(acceptPreference); const announce = acceptPreference; acceptPreference = null; announce({ release, result }) })
        return route.fulfill({ json: result })
      }
      unexpected.push(method + ' ' + name); return problem(405, 'Unexpected delivery command')
    })
    const row = key => page.locator('[data-notification-screen="' + source.ids[key] + '"]')
    const fact = key => row(key).locator('[data-notification-delivery]')
    const follow = key => row(key).getByRole('button', { name: 'Follow level for ' + f.project.projectNumber, exact: true })
    const noPrivateDisplay = async () => assert.equal((await page.content()).includes(f.privateCanary), false)
    const capture = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: join(process.env.TUESDAY_EVIDENCE_DIR, name + '.png'), fullPage: true }) }
    await page.goto(base + '/notifications'); await fact('real').waitFor()
    await page.getByText(source.definitions.length + ' notifications', { exact: true }).waitFor()
    assert.equal(await fact('real').getAttribute('data-notification-delivery'), 'Emailed')
    await fact('real').getByText('Accepted by the email service', { exact: true }).waitFor()
    await fact('real').getByText('Recorded delivery history · 2 of 2 occurrences have recorded delivery history', { exact: true }).waitFor()
    await fact('real').getByText('2 occurrences accepted by the email service', { exact: true }).waitFor()
    assert.equal(await fact('real').locator('time').getAttribute('datetime'), f.stamp)
    assert.match(await fact('real').locator('time').innerText(), /^Accepted by the email service \d{4}-\d{2}-\d{2} \d{2}:\d{2}$/)
    assert.equal(await fact('simulated').getAttribute('data-notification-delivery'), 'Simulated')
    await fact('simulated').getByText('Simulated email — no real email sent', { exact: true }).waitFor()
    assert.equal(await fact('simulated').locator('time').count(), 0)
    for (const key of ['legacy', 'missingTime', 'badTime', 'badCounts', 'badState', 'fractional', 'conflicting', 'simulatedTime']) {
      assert.equal(await fact(key).getAttribute('data-notification-delivery'), 'Unknown')
      assert.equal(await fact(key).innerText(), 'Email delivery history unavailable')
      assert.equal(await fact(key).locator('time').count(), 0)
    }
    assert.equal(await fact('mixed').getAttribute('data-notification-delivery'), 'Mixed')
    await fact('mixed').getByText('Some delivery history unavailable · 2 of 3 occurrences have recorded delivery history', { exact: true }).waitFor()
    assert.equal(await fact('mixed').locator('time').count(), 0)
    assert.equal(await fact('partialBulk').getAttribute('data-notification-delivery'), 'Partial')
    await fact('partialBulk').getByText('Some email updates accepted', { exact: true }).waitFor()
    assert.equal(await fact('partialBulk').getByText(/occurrences accepted by the email service/).count(), 0)
    assert.equal(await fact('partialBulk').locator('time').count(), 0)
    assert.equal(await fact('partialReal').getAttribute('data-notification-delivery'), 'Partial')
    await fact('partialReal').getByText('1 occurrence accepted by the email service', { exact: true }).waitFor()
    await fact('partialReal').getByText('1 occurrence has an uncertain send outcome', { exact: true }).waitFor()
    assert.equal(await fact('partialReal').locator('time').count(), 0)
    assert.equal(await fact('uncertain').getAttribute('data-notification-delivery'), 'Uncertain')
    await fact('uncertain').getByText('Email send outcome uncertain', { exact: true }).waitFor()
    assert.equal(await fact('uncertain').locator('time').count(), 0)
    assert.equal(await fact('held').getAttribute('data-notification-delivery'), 'HeldAway')
    assert.equal(await fact('noAddress').getAttribute('data-notification-delivery'), 'NoAddress')
    assert.equal(await page.locator('[data-notification-delivery="Emailed"]').count(), 1)
    await noPrivateDisplay(); assert.deepEqual(commands, []); await capture('notice-delivery-desktop')
    observations.push('Complete consistent real receipt alone shows acceptance/time; simulation explicitly says no real email; legacy/unknown/malformed state/count/time safely unavailable; Mixed/Partial/Uncertain never claim complete Emailed, including partial bulk with real0')

    // Real keyboard navigation only: no programmatic focus or synthetic DOM events.
    await page.getByLabel('Type', { exact: true }).click()
    for (let tabs = 0; tabs < 40 && !await follow('real').evaluate(node => document.activeElement === node); tabs++) await page.keyboard.press('Tab')
    assert.equal(await follow('real').evaluate(node => document.activeElement === node), true)
    const stableUrl = page.url()
    await page.keyboard.press('Enter'); await page.getByRole('menuitemradio', { name: 'Muted', exact: true }).waitFor()
    await page.keyboard.press('Escape'); await page.waitForFunction(node => document.activeElement === node, await follow('real').elementHandle())
    assert.equal(page.url(), stableUrl); assert.deepEqual(commands, [])
    await page.setViewportSize({ width: 375, height: 812 }); await follow('partialBulk').scrollIntoViewIfNeeded(); await follow('partialBulk').click()
    await page.getByRole('menuitemradio', { name: 'My items only', exact: true }).waitFor(); await page.keyboard.press('Escape')
    await page.waitForFunction(node => document.activeElement === node, await follow('partialBulk').elementHandle())
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    await noPrivateDisplay(); await capture('notice-delivery-phone'); assert.deepEqual(commands, [])
    observations.push('Desktop Tab/Enter/Escape and375px sibling action preserve natural source focus/route with readable delivery facts and no command or private metadata display')

    const unchangedNotices = JSON.stringify(source.notices()), beforePulse = screenReads.length
    const pulseRefresh = screenReply(200); source.acceptQueued(); await pulseRefresh
    await row('queued').locator('[data-notification-delivery="Emailed"]').waitFor()
    assert.ok(screenReads.length > beforePulse); assert.ok(pulseReads.includes(source.pulse().stamp))
    assert.equal(JSON.stringify(source.notices()), unchangedNotices, 'Delivery-only pulse refresh does not change notification count/content/time')
    assert.equal(await page.locator('[data-notification-delivery="Emailed"]').count(), 2)
    screenFailure = 403; const refused = screenReply(403); source.bumpPulse(); await refused
    await page.getByRole('alert').getByText('Synthetic current delivery history refused', { exact: true }).waitFor()
    assert.equal(await page.locator('[data-notification-screen]').count(), 0); assert.equal(await page.locator('[data-notification-delivery]').count(), 0)
    screenFailure = 0; const recovered = screenReply(200); source.bumpPulse(); await recovered; await fact('real').waitFor()
    assert.equal(await page.locator('[data-notification-delivery="Emailed"]').count(), 2)
    observations.push('Actual4s pulse changes refresh receipt status with unchanged app counts/content/time; failed current same-key GET hides cached delivery facts; actual subsequent pulse GET recovers, no cache/clock/event injection or command')

    // This portion requires the future real Preferences mount. A confirmed PATCH remains saved if notification refresh fails.
    await page.setViewportSize({ width: 1440, height: 1000 })
    const freshReadAt = Date.now(), beforePreferences = screenReads.length, sameStamp = source.pulse().stamp
    await page.getByRole('link', { name: 'Notification preferences', exact: true }).click()
    const preferences = page.getByRole('form', { name: 'Notice preferences', exact: true })
    await preferences.waitFor(); await preferences.getByRole('combobox', { name: 'Digest frequency', exact: true }).selectOption('Weekly')
    expectedPreference = { revision: 0, noticeDigestFrequency: 'Weekly' }
    const held = new Promise(resolve => { acceptPreference = resolve })
    await preferences.getByRole('button', { name: 'Save preferences', exact: true }).click(); const receipt = await held
    const recent = page.waitForResponse(response => { const url = new URL(response.url()); return url.pathname === '/api/v1/me/notifications' && url.searchParams.get('pageSize') === '8' && response.status() === 200 })
    await page.getByRole('button', { name: 'Notifications', exact: true }).click(); await recent
    await page.getByRole('link', { name: 'Open the notification centre', exact: true }).waitFor()
    recentFailure = 403
    const failedRefresh = page.waitForResponse(response => { const url = new URL(response.url()); return url.pathname === '/api/v1/me/notifications' && url.searchParams.get('pageSize') === '8' && response.status() === 403 })
    const patched = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/notice-preferences' && response.request().method() === 'PATCH' && response.status() === 200)
    receipt.release(); await patched; await failedRefresh
    await preferences.getByRole('status').filter({ hasText: 'Preferences saved.' }).waitFor()
    assert.equal(source.current().revision, 1); assert.equal(commands.length, 1)
    assert.equal(await preferences.getByRole('combobox', { name: 'Digest frequency', exact: true }).inputValue(), 'Weekly')
    assert.equal(await preferences.getByRole('status').filter({ hasText: 'We couldn’t confirm whether your changes were saved.' }).count(), 0)
    assert.equal(await preferences.getByRole('button', { name: 'Save preferences', exact: true }).isDisabled(), true)
    assert.ok(recentReads.some(read => read.status === 403)); await page.keyboard.press('Escape')
    recentFailure = 0
    const invalidatedReturn = screenReply(200); await page.goBack(); await invalidatedReturn
    await page.waitForURL(url => url.pathname === '/notifications'); await fact('real').waitFor()
    assert.ok(Date.now() - freshReadAt < 15000, 'Preference roundtrip is inside production screen freshness')
    assert.ok(screenReads.length > beforePreferences); assert.equal(source.pulse().stamp, sameStamp)
    assert.equal(commands.length, 1); assert.equal(expectedPreference, null); assert.equal(acceptPreference, null)
    await noPrivateDisplay()
    observations.push('Real preference Save0->1 independently invalidates an active bell GET403 and cached screen read; Saved stays confirmed with no uncertain/retry, unchanged pulse and cache-fresh real Back still fetches current screen')

    source.flags.people_screens = false; const beforeOff = screenReads.length
    await page.reload(); await page.getByRole('button', { name: 'Mark all read', exact: true }).waitFor()
    assert.equal(await page.locator('[data-notification-screen]').count(), 0); assert.equal(await page.locator('[data-notification-delivery]').count(), 0)
    assert.equal(screenReads.length, beforeOff); assert.equal(commands.length, 1)
    assert.deepEqual(errors, []); assert.deepEqual(unexpected, []); assert.deepEqual(ui.mock.unknownGets, [])
    console.log(JSON.stringify({ status: 'PASS', suite: 'notice-delivery', evidence: 'synthetic actual-SPA UI; provider delivery/policy/privacy gates separate', observations, screenReads, pulseReads, recentReads, commands }))
  } catch (error) { console.error(JSON.stringify({ status: 'FAIL', suite: 'notice-delivery', url: page.url(), observations, screenReads, pulseReads, recentReads, commands, unexpected, errors })); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
