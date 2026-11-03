// FUTURE actual-SPA proof: intentionally UNRUN while NoticePreferences is unmounted.
// Root may run after the complete delivery policy and real Preferences mount exist. No test-only mount.
// Synthetic transport proves UI intent/recovery; provider tests own self authorization/locking/persistence.
const assert = require('node:assert/strict')
const { join } = require('node:path')
const { newMockPage } = require('./platform-fixtures.cjs')
const f = require('./notice-preferences.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname))
;(async () => {
  const ui = await newMockPage(base, { events: false, nativeVisibility: true }), { page } = ui, source = f.world()
  const errors = [], unexpected = [], writes = [], reads = [], applied = [], observations = []
  if (process.env.TUESDAY_REVIEW_VISUAL === '1') source.flags.visual_system = true
  const legacy = JSON.stringify(source.legacy)
  const shell = new Set(['config', 'reference', 'workspaces', 'views', 'me/notifications/pulse', 'me/notifications/unread-count', 'events'])
  let mode = 'success', failedRead = 0, malformedRead = false, expectedBody = null, acceptHeld = null
  try {
    page.setDefaultTimeout(15000); page.on('pageerror', error => errors.push(error.message))
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), name = url.pathname.replace('/api/v1/', ''), method = request.method()
      const problem = (status, detail, code = 'invalid') => route.fulfill({ status, contentType: 'application/problem+json', json: { status, code, detail } })
      if (method === 'POST' && name === 'me/sign-in') return route.fallback()
      if (method === 'GET') {
        if (name === 'me') return route.fulfill({ json: source.actor })
        if (name === 'me/preferences') return route.fulfill({ json: source.legacy })
        if (name === 'home') return route.fulfill({ json: source.home })
        if (name === 'me/notice-preferences') {
          assert.equal(source.flags.people_screens, true)
          reads.push({ status: failedRead || 200, malformed: malformedRead, revision: source.current().revision })
          if (failedRead) return problem(failedRead, 'Synthetic current settings refused ' + failedRead)
          return route.fulfill({ json: malformedRead ? { revision: source.current().revision } : source.current() })
        }
        if (shell.has(name)) return route.fallback()
        unexpected.push(method + ' ' + name); return problem(501, 'Unexpected GET')
      }
      const body = request.postDataJSON()
      writes.push({ method, name, body })
      if (method === 'PATCH' && name === 'me/notice-preferences') {
        assert.equal(source.flags.people_screens, true)
        assert.ok(expectedBody, 'Every PATCH needs a new deliberate Save action')
        assert.deepEqual(body, expectedBody); expectedBody = null
        assert.equal(request.headers()['if-match'], undefined, 'Observed revision belongs to the exact body contract')
        if (mode === 'conflict') {
          assert.notEqual(body.revision, source.current().revision)
          return problem(409, 'Synthetic settings changed since your draft', 'concurrency_conflict')
        }
        assert.equal(body.revision, source.current().revision)
        if (mode === 'network') return route.abort('failed')
        const result = source.apply(body); applied.push({ ...result })
        if (mode === 'lost') return problem(503, 'Synthetic reply lost after save')
        if (mode === 'malformed') return route.fulfill({ json: { revision: result.revision } })
        if (mode === 'contradictory') return route.fulfill({ json: f.contradictoryReply(result) })
        if (mode === 'held') await new Promise(release => {
          assert.ok(acceptHeld); const announce = acceptHeld; acceptHeld = null
          announce({ result: { ...result }, release })
        })
        return route.fulfill({ json: result })
      }
      unexpected.push(method + ' ' + name); return problem(405, 'Unexpected command')
    })
    const form = () => page.getByRole('form', { name: 'Notice preferences', exact: true })
    const frequency = () => form().getByRole('combobox', { name: 'Digest frequency', exact: true })
    const day = () => form().getByRole('combobox', { name: 'Reminder day', exact: true })
    const time = () => form().getByLabel('Reminder time', { exact: true })
    const reminder = () => form().getByRole('switch', { name: 'Remind me to enter my time', exact: true })
    const hold = () => form().getByRole('switch', { name: 'Hold immediate email while I’m away', exact: true })
    const save = () => form().getByRole('button', { name: 'Save preferences', exact: true })
    const check = () => form().getByRole('button', { name: 'Check current settings', exact: true })
    const currentSettingsButton = () => form().getByRole('button', { name: 'Use current settings', exact: true })
    const saved = () => form().getByRole('status').filter({ hasText: 'Preferences saved.' })
    const uncertainty = () => form().getByRole('status').filter({ hasText: 'We couldn’t confirm whether your changes were saved.' })
    const capture = async name => { if (process.env.TUESDAY_EVIDENCE_DIR) await page.screenshot({ path: join(process.env.TUESDAY_EVIDENCE_DIR, name + '.png') }) }
    const adoptCurrent = async () => { await currentSettingsButton().click(); assert.equal(await save().isDisabled(), true); assert.equal(await check().count(), 0) }
    const holdSave = async body => {
      mode = 'held'; expectedBody = body
      const pending = new Promise(resolve => { acceptHeld = resolve })
      await save().click()
      return await pending
    }
    const releaseSave = async held => {
      const reply = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/notice-preferences' && response.request().method() === 'PATCH' && response.status() === 200)
      held.release(); await reply
      await page.waitForFunction(() => Array.from(document.querySelectorAll('#notice-preferences button')).some(button => button.textContent === 'Check current settings' && !button.disabled))
    }
    const backgroundRead = async status => {
      failedRead = status === 200 ? 0 : status
      const before = reads.length, other = await page.context().newPage()
      try {
        await other.bringToFront(); await page.waitForFunction(() => document.visibilityState === 'hidden')
        // Let the real production query freshness window elapse; no query/visibility/focus emulation.
        await new Promise(resolve => setTimeout(resolve, 15100))
        const response = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/me/notice-preferences' && response.request().method() === 'GET' && response.status() === status)
        await page.bringToFront(); await response
      } finally { await other.close() }
      assert.ok(reads.slice(before).some(read => read.status === status))
    }

    await page.goto(base + '/preferences'); await form().waitFor()
    assert.equal(await frequency().inputValue(), 'Daily'); assert.equal(await hold().isChecked(), false)
    assert.equal(await reminder().isChecked(), false); assert.equal(await day().inputValue(), ''); assert.equal(await time().inputValue(), '')
    assert.equal(await save().isDisabled(), true)
    const digestHold = () => page.locator('#digest').getByRole('switch', { name: "Hold my digest on days I'm away", exact: true })
    assert.equal(await digestHold().isChecked(), true)
    await reminder().click(); await form().getByRole('alert').filter({ hasText: 'Choose a day and time' }).waitFor()
    assert.equal(await save().isDisabled(), true); assert.equal(writes.length, 0)
    await day().selectOption('5'); await time().fill('15:30'); await frequency().selectOption('Weekly'); await hold().click()
    assert.equal(await save().isDisabled(), false)
    const goHome = async () => {
      const link = page.getByRole('link', { name: 'Home', exact: true })
      if (!(await link.isVisible())) await page.getByRole('button', { name: 'More', exact: true }).click()
      await link.click()
    }
    await goHome()
    const leave = page.getByRole('dialog', { name: 'Keep editing or discard?', exact: true })
    await leave.waitFor(); await leave.getByRole('button', { name: 'Keep editing', exact: true }).click(); await leave.waitFor({ state: 'hidden' })
    assert.equal(new URL(page.url()).pathname, '/preferences'); assert.equal(await time().inputValue(), '15:30'); assert.equal(writes.length, 0)
    expectedBody = { revision: 0, noticeDigestFrequency: 'Weekly', holdImmediateEmailWhenAway: true, timeReminderEnabled: true, timeReminderWeekday: 5, timeReminderTimeLocal: '15:30' }
    const saveStarted = Date.now()
    await save().click(); await saved().waitFor(); assert.equal(source.current().revision, 1)
    const readsAfterSave = reads.length, savedForm = await form().elementHandle()
    assert.ok(savedForm)
    await goHome(); await page.waitForURL(url => url.pathname === '/home')
    await page.getByRole('heading', { name: 'Home', exact: true }).waitFor()
    assert.equal(await savedForm.evaluate(node => node.isConnected), false, 'The successful preference editor really unmounted')
    await page.getByRole('button', { name: 'Account', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Notification preferences', exact: true }).click()
    await page.waitForURL(url => url.pathname === '/preferences'); await form().waitFor()
    assert.ok(Date.now() - saveStarted < 15000, 'Real client roundtrip completed inside production query freshness')
    assert.equal(reads.length, readsAfterSave, 'Fresh normal-success return needs no replacement GET to recover the saved revision')
    assert.equal(await frequency().inputValue(), 'Weekly'); assert.equal(await hold().isChecked(), true); assert.equal(await reminder().isChecked(), true)
    assert.equal(await day().inputValue(), '5'); assert.equal(await time().inputValue(), '15:30'); assert.equal(await save().isDisabled(), true)
    assert.equal(await digestHold().isChecked(), true); await capture('notice-preferences-desktop')
    await reminder().click(); expectedBody = { revision: 1, timeReminderEnabled: false }
    await save().click(); await saved().waitFor(); assert.equal(source.current().revision, 2)
    assert.equal(await day().inputValue(), '5'); assert.equal(await time().inputValue(), '15:30')
    observations.push('Null reminder choices start blank; enabled reminder requires actual day/time; dirty leave keeps choices; normal Save0→Home→real account-menu Preferences remount before15s keeps all saved choices without GET and next deliberate save uses revision1; independent digest hold and disabled reminder choices retained')

    await frequency().selectOption('Daily'); await hold().click()
    source.change({ revision: 3, timeReminderWeekday: 2, timeReminderTimeLocal: '09:45' })
    mode = 'conflict'; expectedBody = { revision: 2, noticeDigestFrequency: 'Daily', holdImmediateEmailWhenAway: false }
    await save().click(); await form().getByText('Synthetic settings changed since your draft', { exact: true }).waitFor()
    assert.equal(writes.length, 3); assert.equal(await frequency().inputValue(), 'Daily'); assert.equal(await frequency().isDisabled(), true)
    const formNode = await form().elementHandle(), timeNode = await time().elementHandle(), frequencyNode = await frequency().elementHandle()
    assert.ok(formNode && timeNode && frequencyNode)
    const error = () => page.locator('[data-notice-preferences-error]')
    for (const status of [403, 404, 503]) {
      failedRead = status; const before = reads.length, stableUrl = page.url()
      await check().click(); await error().getByRole('alert').waitFor()
      assert.equal(reads.length, before + 1); assert.equal(reads.at(-1).status, status)
      assert.equal(await formNode.evaluate(node => node.isConnected && node.hidden && node.inert && node.getAttribute('aria-hidden') === 'true'), true)
      assert.equal(await frequencyNode.evaluate(node => node.isConnected && node.value === 'Daily'), true)
      assert.equal(await timeNode.evaluate(node => node.isConnected && node.value === '15:30'), true)
      assert.equal(await form().count(), 0); assert.equal(await page.getByText('Preferences saved.', { exact: true }).count(), 0)
      assert.equal(await page.locator('[data-notice-preferences-current]:visible').count(), 0)
      assert.equal(page.url(), stableUrl); assert.equal(writes.length, 3)
      if (status === 403) await capture('notice-preferences-current-error')
      failedRead = 0
      await error().getByRole('button', { name: 'Check current settings', exact: true }).click(); await form().waitFor()
      await form().getByRole('status').filter({ hasText: 'Current settings differ from your draft.' }).waitFor()
      assert.equal(await frequency().inputValue(), 'Daily'); assert.equal(await time().inputValue(), '15:30'); assert.equal(writes.length, 3)
    }
    await adoptCurrent(); assert.equal(await frequency().inputValue(), 'Weekly'); assert.equal(await day().inputValue(), '2'); assert.equal(await time().inputValue(), '09:45')
    await frequency().selectOption('Daily'); await hold().click()
    mode = 'success'; expectedBody = { revision: 3, noticeDigestFrequency: 'Daily', holdImmediateEmailWhenAway: false }
    await save().click(); await saved().waitFor(); assert.equal(source.current().revision, 4); assert.equal(writes.length, 4)
    observations.push('Conflict keeps original revision2 and draft; actual current403/404/503 hides/inerts cached form and current facts while keeping DOM/URL; explicit reads never PATCH, deliberate current-state adoption precedes new revision3 save')

    await frequency().selectOption('Weekly'); mode = 'lost'; expectedBody = { revision: 4, noticeDigestFrequency: 'Weekly' }
    await save().click(); await uncertainty().waitFor(); assert.equal(writes.length, 5); assert.equal(source.current().revision, 5)
    assert.equal(await save().count(), 0); assert.equal(await saved().count(), 0)
    await goHome(); await leave.waitFor()
    await leave.getByRole('button', { name: 'Keep editing', exact: true }).click(); await leave.waitFor({ state: 'hidden' })
    assert.equal(await frequency().inputValue(), 'Weekly'); assert.equal(writes.length, 5)
    await check().click(); await form().getByRole('status').filter({ hasText: 'Current settings match your draft.' }).waitFor()
    assert.equal(await uncertainty().count(), 1); assert.equal(await saved().count(), 0); assert.equal(writes.length, 5)
    await adoptCurrent()
    await hold().click(); mode = 'malformed'; expectedBody = { revision: 5, holdImmediateEmailWhenAway: true }
    await save().click(); await uncertainty().waitFor(); assert.equal(writes.length, 6); assert.equal(source.current().revision, 6)
    await check().click(); await form().getByRole('status').filter({ hasText: 'Current settings match your draft.' }).waitFor()
    assert.equal(writes.length, 6); assert.equal(await saved().count(), 0); await adoptCurrent()
    await frequency().selectOption('Daily'); mode = 'network'; expectedBody = { revision: 6, noticeDigestFrequency: 'Daily' }
    await save().click(); await uncertainty().waitFor(); assert.equal(writes.length, 7); assert.equal(source.current().revision, 6)
    await check().click(); await form().getByRole('status').filter({ hasText: 'Current settings differ from your draft.' }).waitFor()
    assert.equal(await frequency().inputValue(), 'Daily'); assert.equal(await frequency().isDisabled(), true); assert.equal(writes.length, 7)
    await adoptCurrent()
    observations.push('Lost-after-save, malformed-after-save and actual aborted PATCH preserve original intent without retry; current matching/different is a read fact, never a saved receipt; uncertain leave stays protected')

    source.actor.systemRoles = ['ReadOnly']; source.actor.capabilities.readOnly = true; mode = 'success'
    await page.goto(base + '/preferences'); await form().waitFor(); await frequency().selectOption('Daily')
    expectedBody = { revision: 6, noticeDigestFrequency: 'Daily' }
    await save().click(); await saved().waitFor(); assert.equal(source.current().revision, 7)
    assert.equal(writes.length, 8); assert.equal(applied.length, 6)

    // A delayed mutation receipt cannot roll a newer successful GET back to its earlier revision.
    await hold().click()
    const heldOlder = await holdSave({ revision: 7, holdImmediateEmailWhenAway: false })
    assert.equal(heldOlder.result.revision, 8)
    source.change({ revision: 9, noticeDigestFrequency: 'Weekly', timeReminderWeekday: 4, timeReminderTimeLocal: '11:20' })
    await backgroundRead(200)
    assert.equal(reads.at(-1).revision, 9)
    await releaseSave(heldOlder)
    assert.equal(await saved().count(), 0); assert.equal(await save().count(), 0)
    assert.equal(await frequency().inputValue(), 'Daily'); assert.equal(await time().inputValue(), '09:45'); assert.equal(await hold().isChecked(), false)
    assert.equal(await frequency().isDisabled(), true); assert.equal(writes.length, 9)
    await check().click(); await form().getByRole('status').filter({ hasText: 'Current settings differ from your draft.' }).waitFor()
    await adoptCurrent(); assert.equal(await frequency().inputValue(), 'Weekly'); assert.equal(await day().inputValue(), '4'); assert.equal(await time().inputValue(), '11:20')
    assert.equal(writes.length, 9)

    // Genuine current GET failures arrive while an already-applied PATCH reply is held in flight.
    for (const status of [403, 404, 503]) {
      const original = source.current(), requested = original.noticeDigestFrequency === 'Weekly' ? 'Daily' : 'Weekly'
      await frequency().selectOption(requested)
      const beforeWrites = writes.length, stableUrl = page.url(), heldForm = await form().elementHandle(), heldFrequency = await frequency().elementHandle()
      assert.ok(heldForm && heldFrequency)
      const held = await holdSave({ revision: original.revision, noticeDigestFrequency: requested })
      assert.equal(held.result.revision, original.revision + 1)
      await backgroundRead(status); await error().getByRole('alert').waitFor()
      assert.equal(await heldForm.evaluate(node => node.isConnected && node.hidden && node.inert), true)
      await releaseSave(held)
      await error().getByRole('alert').waitFor()
      assert.equal(await heldForm.evaluate(node => node.isConnected && node.hidden && node.inert && node.getAttribute('aria-hidden') === 'true'), true)
      assert.equal(await heldFrequency.evaluate((node, requested) => node.isConnected && node.value === requested, requested), true)
      assert.equal(await form().count(), 0); assert.equal(await page.getByText('Preferences saved.', { exact: true }).count(), 0)
      assert.equal(page.url(), stableUrl); assert.equal(writes.length, beforeWrites + 1)
      await goHome(); await leave.waitFor()
      await leave.getByRole('button', { name: 'Keep editing', exact: true }).click(); await leave.waitFor({ state: 'hidden' })
      assert.equal(await heldForm.evaluate(node => node.isConnected && node.hidden && node.inert), true)
      failedRead = 0
      await error().getByRole('button', { name: 'Check current settings', exact: true }).click(); await form().waitFor()
      await form().getByRole('status').filter({ hasText: 'Current settings match your draft.' }).waitFor()
      assert.equal(await frequency().inputValue(), requested); assert.equal(await frequency().isDisabled(), true)
      assert.equal(await saved().count(), 0); assert.equal(await save().count(), 0); assert.equal(writes.length, beforeWrites + 1)
      await adoptCurrent()
    }
    observations.push('Held valid PATCH receipts never replace intervening newer GET9 or erase genuine current GET403/404/503; original draft/leave guard survive hidden, only explicit successful current GET recovers, no automatic resend/rebase')

    // Revision+1 and the requested hold field are insufficient when omitted frequency/day/time contradict the original snapshot.
    const beforeContradiction = source.current(), originalFrequency = beforeContradiction.noticeDigestFrequency, originalDay = beforeContradiction.timeReminderWeekday, originalTime = beforeContradiction.timeReminderTimeLocal
    await hold().click(); mode = 'contradictory'; expectedBody = { revision: beforeContradiction.revision, holdImmediateEmailWhenAway: !beforeContradiction.holdImmediateEmailWhenAway }
    await save().click(); await uncertainty().waitFor()
    assert.equal(writes.length, 13); assert.equal(applied.length, 11)
    assert.equal(await saved().count(), 0); assert.equal(await save().count(), 0)
    assert.equal(await frequency().inputValue(), originalFrequency); assert.equal(await day().inputValue(), String(originalDay)); assert.equal(await time().inputValue(), originalTime)
    assert.equal(await frequency().isDisabled(), true)
    await check().click(); await form().getByRole('status').filter({ hasText: 'Current settings match your draft.' }).waitFor()
    assert.equal(writes.length, 13); assert.equal(await saved().count(), 0); await adoptCurrent()
    observations.push('Schema-valid contradictory revision+1 reply that matches changed hold but alters omitted frequency/day/time is uncertain; original whole draft remains, honest GET checks all choices and never automatically PATCHes')

    await page.setViewportSize({ width: 375, height: 800 }); await form().scrollIntoViewIfNeeded(); await capture('notice-preferences-phone')
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    assert.equal(await day().inputValue(), '4'); assert.equal(await time().inputValue(), '11:20')
    source.flags.people_screens = false; source.flags.work_screens = true; const beforeOff = reads.length
    await page.reload(); await page.locator('#digest').waitFor(); assert.equal(await page.locator('#notice-preferences').count(), 0); assert.equal(reads.length, beforeOff)
    assert.equal(await digestHold().isChecked(), true)
    source.flags.people_screens = true; source.flags.work_screens = false; source.flags.capacity_screens = false
    await page.reload(); await form().waitFor(); assert.ok(reads.length > beforeOff)
    malformedRead = true; await page.reload(); await page.locator('#notice-preferences').getByRole('alert').waitFor()
    assert.equal(await form().count(), 0); assert.equal(writes.length, 13)
    malformedRead = false; await page.locator('#notice-preferences').getByRole('button', { name: 'Check current settings', exact: true }).click(); await form().waitFor()
    assert.equal(await frequency().inputValue(), 'Daily'); assert.equal(await save().isDisabled(), true)
    assert.equal(JSON.stringify(source.legacy), legacy); assert.equal(writes.length, 13); assert.equal(expectedBody, null); assert.equal(acceptHeld, null)
    assert.deepEqual(errors, []); assert.deepEqual(unexpected, []); assert.deepEqual(ui.mock.unknownGets, [])
    observations.push('Read Only self preference remains editable, 375px fits, people gate alone controls new reads/UI, independent work/capacity flags and legacy digest remain intact, malformed initial read shows no invented settings and explicitly recovers')
    console.log(JSON.stringify({ status: 'PASS', suite: 'notice-preferences', evidence: 'synthetic actual-SPA after future full-policy mount; no mail/dispatch proof', observations, reads, writes, applied }))
  } catch (error) { console.error(JSON.stringify({ status: 'FAIL', suite: 'notice-preferences', url: page.url(), observations, reads, writes, unexpected, errors })); throw error }
  finally { await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
