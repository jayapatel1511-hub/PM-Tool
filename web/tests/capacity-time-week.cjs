// S32 rendered interaction checks use synthetic responses. CapacityTimeWeekTests owns real command/calendar/permission evidence.
const assert = require('node:assert/strict')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { timeRow, timeView, entrySaved } = require('./time.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname), 'Use the local built preview')
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const first = '2026-11-09', last = '2026-11-15', stamp = '2026-11-01T13:00:00Z'
const addDays = (date, days) => { const value = new Date(`${date}T00:00:00Z`); value.setUTCDate(value.getUTCDate() + days); return value.toISOString().slice(0, 10) }
const tasks = [1, 2, 3, 4].map(n => ({ taskId: id(900 + n), taskKey: `TW-${n}`, taskName: `Synthetic time task ${n}`, projectId: project.id,
  projectNumber: project.projectNumber, projectName: project.name, canCreate: true, deleted: false, assigned: n <= 3, previousWeek: n === 1 || n === 4 }))

;(async () => {
  const ui = await newMockPage(base, { events: false }), { page, mock } = ui
  const errors = [], unknownWrites = [], observedWrites = [], creates = [], patches = [], weekReads = [], entries = [], receipts = new Map()
  let mode = 'Complete', loseResponse = false, staleNext = false, nextId = 1000, holdWeekRefresh = false, failWeekRefresh = false, notifyHeldRead = null
  const releaseReads = []
  const current = (url) => entries.filter(entry => entry.workDate >= url.searchParams.get('from') && entry.workDate <= url.searchParams.get('to')
    && (!url.searchParams.get('taskId') || entry.taskId === url.searchParams.get('taskId'))
    && (!url.searchParams.get('projectId') || entry.projectId === url.searchParams.get('projectId'))
    && (!url.searchParams.get('userId') || entry.userId === url.searchParams.get('userId')))
  function ownWeek(url) {
    const from = url.searchParams.get('from'), to = url.searchParams.get('to'), visible = current(url)
    return { from, to, today: first, personId: me.id, capacityHours: ['Provisional', 'Unavailable'].includes(mode) ? null : mode === 'Zero' ? 0 : 32, asOf: stamp,
      days: Array.from({ length: 7 }, (_, n) => ({ date: addDays(from, n), normalHours: mode === 'Unavailable' && n === 1 ? null : mode === 'Zero' ? 0 : n < 5 ? 8 : 0,
        availableHours: mode === 'Unavailable' && n === 1 ? null : mode === 'Zero' ? 0 : n === 2 || n >= 5 ? 0 : mode === 'Provisional' && n === 1 ? 4 : 8,
        timeAwayHours: mode === 'Unavailable' && n === 1 ? null : mode === 'Zero' ? 0 : n === 2 ? 8 : mode === 'Provisional' && n === 1 ? 4 : 0,
        holidayHours: mode === 'Unavailable' && n === 1 ? null : 0, additionalHours: mode === 'Unavailable' && n === 1 ? null : 0,
        provisional: mode === 'Provisional' && n === 1, unavailable: mode === 'Unavailable' && n === 1 })),
      tasks: tasks.filter(task => (!url.searchParams.get('taskId') || task.taskId === url.searchParams.get('taskId')) && (!url.searchParams.get('projectId') || task.projectId === url.searchParams.get('projectId')))
        .map(task => ({ ...task, canCreate: !me.capabilities.readOnly, entries: visible.filter(entry => entry.taskId === task.taskId).map(entry => ({ ...entry, canEdit: !me.capabilities.readOnly })) })),
    }
  }
  try {
    page.setDefaultTimeout(10000); page.on('pageerror', error => errors.push(error.message))
    me.flags = { ...me.flags, live_events: false, capacity_screens: false, visual_system: false }
    page.on('request', request => {
      if (new URL(request.url()).pathname.startsWith('/api/') && !['GET', 'HEAD'].includes(request.method())) observedWrites.push(`${request.method()} ${new URL(request.url()).pathname}`)
    })
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      if (path === 'time/week') {
        weekReads.push(Object.fromEntries(url.searchParams))
        if (holdWeekRefresh) { notifyHeldRead?.(); await new Promise(resolve => releaseReads.push(resolve)) }
        if (failWeekRefresh) return route.fulfill({ status: 503, json: { detail: 'Synthetic own-week refresh unavailable' } })
        return route.fulfill({ json: ownWeek(url) })
      }
      if (path === 'time' && request.method() === 'GET') return route.fulfill({ json: timeView(url.searchParams.get('from'), url.searchParams.get('to'), current(url), true) })
      if (path === 'time' && request.method() === 'POST') {
        const body = request.postDataJSON(), key = request.headers()['idempotency-key']; creates.push({ body, key })
        if (!receipts.has(key)) {
          const task = tasks.find(task => task.taskId === body.taskId)
          const entry = timeRow({ id: id(nextId++), projectId: project.id, taskId: body.taskId, userId: me.id,
            projectNumber: task.projectNumber, projectName: task.projectName, taskKey: task.taskKey, taskName: task.taskName, person: me.displayName,
            workDate: body.workDate, hours: body.hours, note: body.note })
          entries.push(entry); receipts.set(key, entrySaved(entry.id, entry.rowVersion))
        }
        if (loseResponse) { loseResponse = false; return route.abort('failed') }
        return route.fulfill({ status: 201, json: receipts.get(key) })
      }
      if (path.startsWith('time/') && request.method() === 'PATCH') {
        const body = request.postDataJSON(), entry = entries.find(entry => entry.id === path.split('/')[1]), version = request.headers()['if-match']
        patches.push({ id: entry.id, body, version })
        if (staleNext) { staleNext = false; entry.hours = 3; entry.rowVersion++; return route.fulfill({ status: 409, json: { code: 'concurrency_conflict', detail: 'Synthetic entry changed. Reload it.' } }) }
        assert.equal(version, `"${entry.rowVersion}"`)
        entry.hours = body.hours; if ('note' in body) entry.note = body.note; if ('workDate' in body) entry.workDate = body.workDate
        entry.rowVersion++; return route.fulfill({ json: entrySaved(entry.id, entry.rowVersion) })
      }
      if (!['GET', 'HEAD'].includes(request.method()) && path !== 'me/sign-in') unknownWrites.push(`${request.method()} ${path}`)
      return route.fallback()
    })
    const url = `${base}/time?from=${first}&to=${last}`
    const region = page.getByRole('region', { name: 'Your task hours by day', exact: true })
    const row = task => region.locator(`[data-time-week-task="${task.taskId}"]`)
    const cell = (task, date) => row(task).locator(`[data-time-cell="${date}"]`)
    const input = (task, date) => cell(task, date).getByRole('spinbutton')
    const total = value => page.waitForFunction(value => document.querySelector('[data-time-week-total]')?.textContent === value, value)
    await page.goto(url); await page.getByRole('heading', { name: 'Time', exact: true }).waitFor()
    await page.getByRole('list', { name: 'Hours per day', exact: true }).waitFor()
    assert.equal(await region.count(), 0); assert.equal(weekReads.length, 0)
    me.flags.capacity_screens = true
    await page.reload(); await region.waitFor()
    await page.getByText('Capacity 32 h this week', { exact: true }).waitFor()
    assert.equal(await region.locator('[data-time-week-task]').count(), 4)
    const desktopGrid = await region.evaluate(element => ({ width: element.clientWidth, scroll: element.scrollWidth, firstTaskY: element.querySelector('[data-time-week-task]').getBoundingClientRect().top }))
    assert.ok(desktopGrid.scroll <= desktopGrid.width + 1, `all seven days and totals fit the1440px desktop grid: ${JSON.stringify(desktopGrid)}`)
    assert.ok(desktopGrid.firstTaskY <= 405, `actual first Time task row is early: ${JSON.stringify(desktopGrid)}`)
    assert.equal(await region.getByRole('spinbutton').count(), 28)
    assert.equal(await region.getByRole('columnheader').filter({ has: page.getByText('Time away 8 h', { exact: true }) }).count(), 1)
    const wednesday = region.locator('[data-time-day="2026-11-11"]')
    assert.equal(await wednesday.getAttribute('data-time-away'), 'true')
    assert.notEqual(await wednesday.evaluate(element => getComputedStyle(element).backgroundImage), 'none')
    assert.equal(await input(tasks[0], '2026-11-11').isEnabled(), true, 'fully away days remain enterable')
    // Visual-only off/on/off: the same recorded-away data and capacity feature
    // remain present. Styles/labels change without creating an actual-time entry.
    const settleHatch = async visual => {
      await region.waitFor()
      await page.waitForFunction(visual => (document.documentElement.dataset.visual === 'v2') === visual, visual)
      await page.evaluate(async () => { await document.fonts.ready; await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))) })
    }
    const hatch = () => region.evaluate(element => {
      const header = element.querySelector('[data-time-day="2026-11-11"]'), root = getComputedStyle(document.documentElement)
      const rgba = value => { const context = document.createElement('canvas').getContext('2d'); context.fillStyle = value; context.fillRect(0, 0, 1, 1); return [...context.getImageData(0, 0, 1, 1).data].slice(0, 3) }
      const luminance = colour => colour.map(value => { const n = value / 255; return n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4 }).reduce((sum, value, index) => sum + value * [.2126, .7152, .0722][index], 0)
      const stops = ['--hatch-strong', '--hatch-light'].map(name => { const value = root.getPropertyValue(name).trim(); return value ? rgba(value) : null })
      const namedRules = []
      const inspect = (rules, href) => {
        for (const rule of rules) {
          if (rule.type === CSSRule.MEDIA_RULE && !matchMedia(rule.conditionText).matches) continue
          if (rule.selectorText && rule.style?.getPropertyValue('background-image').trim() === 'var(--time-away-hatch)' && header.matches(rule.selectorText)) namedRules.push({ selector: rule.selectorText, href })
          if (rule.cssRules) inspect(rule.cssRules, href)
        }
      }
      for (const sheet of document.styleSheets) { try { if (!sheet.disabled) inspect(sheet.cssRules, sheet.href) } catch { /* Cross-origin styles cannot attest this local named rule. */ } }
      const details = node => ({ date: node.dataset.timeDay || node.dataset.timeCell, away: node.getAttribute('data-time-away'), image: getComputedStyle(node).backgroundImage })
      return { token: root.getPropertyValue('--time-away-hatch').trim(), namedRules,
        days: [...element.querySelectorAll('[data-time-day]')].map(details), cells: [...element.querySelectorAll('[data-time-cell]')].map(details),
        labels: [...header.children].map(node => { const fg = luminance(rgba(getComputedStyle(node).color)); return { text: node.textContent.trim(), colour: getComputedStyle(node).color,
          stripeRatios: stops.map(stop => { if (!stop) return null; const bg = luminance(stop); return (Math.max(fg, bg) + .05) / (Math.min(fg, bg) + .05) }) } }) }
    })
    const recordedBoundaries = snapshot => {
      assert.deepEqual(snapshot.days.filter(day => day.away === 'true').map(day => day.date), ['2026-11-11'])
      assert.equal(snapshot.cells.filter(day => day.away === 'true').length, tasks.length)
      for (const day of [...snapshot.days, ...snapshot.cells]) if (day.date !== '2026-11-11') { assert.equal(day.away, null); assert.equal(day.image, 'none', `ordinary/weekend/unknown ${day.date} is not recorded time away`) }
    }
    const namedHatch = snapshot => {
      recordedBoundaries(snapshot); assert.ok(snapshot.namedRules.length > 0, 'actual away header matches a rule consuming the named hatch token')
      assert.match(snapshot.token, /repeating-linear-gradient\(135deg/)
      for (const day of [...snapshot.days, ...snapshot.cells].filter(day => day.away === 'true')) {
        assert.match(day.image, /repeating-linear-gradient\(135deg/)
        assert.match(day.image, /rgb\(148,\s*160,\s*180\)/); assert.match(day.image, /rgb\(213,\s*218,\s*227\)/)
        assert.deepEqual((day.image.match(/\b\d+(?:\.\d+)?px\b/g) || []).map(parseFloat), [0, 3, 3, 6], 'actual repeating stripes have the required 3px/3px stops')
      }
      assert.ok(snapshot.labels.every(label => label.stripeRatios.every(ratio => ratio != null && ratio >= 4.5)), `all small away-header labels remain readable on both actual opaque hatch stops: ${JSON.stringify(snapshot.labels)}`)
    }
    await settleHatch(false)
    const legacyHatch = await hatch(), writesBeforeVisual = observedWrites.length, visualHatchObservations = []
    recordedBoundaries(legacyHatch)
    for (const visual of [false, true, false]) {
      me.flags.visual_system = visual; assert.equal(me.flags.capacity_screens, true)
      await page.reload(); await settleHatch(visual)
      await wednesday.getByText('Time away 8 h', { exact: true }).waitFor()
      assert.equal(await input(tasks[0], '2026-11-11').isEnabled(), true)
      const snapshot = await hatch()
      const observation = { visual, capacityScreens: me.flags.capacity_screens, snapshot }
      if (visual) {
        namedHatch(snapshot)
        await page.setViewportSize({ width: 375, height: 812 }); await settleHatch(true); observation.phone = await hatch(); namedHatch(observation.phone)
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'visual away day stays inside the phone time-grid scroll region')
        await page.setViewportSize({ width: 1440, height: 1000 })
        mode = 'Unavailable'; await page.reload(); await settleHatch(true)
        await region.locator('[data-time-day="2026-11-10"]').getByText('Availability unavailable', { exact: true }).waitFor()
        observation.unavailable = await hatch(); namedHatch(observation.unavailable) // Null away hours stay unhatched; known Wednesday still carries the token.
        mode = 'Complete'; await page.reload(); await settleHatch(true)
      } else {
        recordedBoundaries(snapshot)
        assert.deepEqual(snapshot.days, legacyHatch.days); assert.deepEqual(snapshot.cells, legacyHatch.cells, 'off restores the exact old computed away styling')
      }
      assert.equal(observedWrites.length, writesBeforeVisual, 'visual-only toggles and unknown availability do not issue any API command')
      assert.deepEqual(creates, []); assert.deepEqual(patches, []); assert.equal(entries.length, 0)
      visualHatchObservations.push(observation)
    }
    console.log(JSON.stringify({ visualHatchObservations, attemptedWritesDuringVisualChanges: observedWrites.length - writesBeforeVisual }))
    await input(tasks[0], '2026-11-10').focus(); await input(tasks[0], '2026-11-10').blur()
    assert.deepEqual(creates, []); assert.deepEqual(patches, []); assert.equal(entries.length, 0, 'blank suggestions/focus persist nothing')
    holdWeekRefresh = true
    const heldRead = new Promise(resolve => { notifyHeldRead = resolve })
    await input(tasks[0], '2026-11-10').fill('2'); await input(tasks[0], '2026-11-10').press('Enter')
    await cell(tasks[0], '2026-11-10').getByText('Saved. Waiting for the recorded entry to refresh; use Reload if it does not appear.', { exact: true }).waitFor()
    await heldRead
    assert.equal(await input(tasks[0], '2026-11-10').isDisabled(), true, 'successful create stays protected while the authoritative entry read is delayed')
    await page.keyboard.type('9'); await page.keyboard.press('Enter')
    assert.equal(creates.length, 1); assert.equal(entries.length, 1, 'a fast second edit cannot re-enter the blank POST path')
    holdWeekRefresh = false; releaseReads.splice(0).forEach(release => release())
    await total('2 h')
    assert.equal(creates.length, 1, 'Enter plus blur is one logical create'); assert.equal(entries.length, 1)
    assert.deepEqual(creates[0].body, { taskId: tasks[0].taskId, workDate: '2026-11-10', hours: 2, note: null }); assert.ok(creates[0].key)
    await input(tasks[0], '2026-11-10').fill('2.25'); await input(tasks[0], '2026-11-10').press('Enter'); await total('2.25 h')
    assert.equal(patches.length, 1); assert.deepEqual(patches[0], { id: entries[0].id, body: { hours: 2.25 }, version: '"1"' })
    const priorWrites = creates.length + patches.length
    await input(tasks[0], '2026-11-10').fill('0'); await input(tasks[0], '2026-11-10').blur()
    await cell(tasks[0], '2026-11-10').getByRole('alert').waitFor()
    await input(tasks[0], '2026-11-10').fill(''); await input(tasks[0], '2026-11-10').blur()
    assert.equal(creates.length + patches.length, priorWrites); assert.equal(entries[0].hours, 2.25, 'zero/clear never delete or overwrite')
    await input(tasks[0], '2026-11-10').fill('2.25'); await input(tasks[0], '2026-11-10').blur()
    staleNext = true
    await input(tasks[0], '2026-11-10').fill('2.5'); await input(tasks[0], '2026-11-10').press('Enter')
    await cell(tasks[0], '2026-11-10').getByText('Synthetic entry changed. Reload it.', { exact: true }).waitFor()
    assert.equal(await input(tasks[0], '2026-11-10').isDisabled(), true)
    await cell(tasks[0], '2026-11-10').getByRole('button', { name: 'Reload', exact: true }).click(); await total('3 h')
    assert.equal(await input(tasks[0], '2026-11-10').inputValue(), '3')
    loseResponse = true
    await input(tasks[1], '2026-11-11').fill('1'); await input(tasks[1], '2026-11-11').press('Enter')
    await cell(tasks[1], '2026-11-11').getByText('The save result is unknown. Retry these same hours to check the original request safely.', { exact: true }).waitFor()
    assert.equal(await input(tasks[1], '2026-11-11').isDisabled(), true)
    await cell(tasks[1], '2026-11-11').getByRole('button', { name: 'Retry', exact: true }).click(); await total('4 h')
    assert.equal(entries.length, 2); assert.deepEqual(creates[1], creates[2], 'lost-response retry preserves exact body and idempotency key')

    // A distinct legitimate entry appears from a later read. The UI must choose an ID, never patch an aggregate.
    entries[0].note = 'Preserved note A'
    entries.push(timeRow({ ...entries[0], id: id(nextId++), hours: 1.5, note: 'Independent note B', rowVersion: 1 }))
    await page.reload(); await region.waitFor(); await total('5.5 h')
    const multiple = cell(tasks[0], '2026-11-10')
    assert.equal(await multiple.getByRole('spinbutton').count(), 0)
    await multiple.getByRole('button', { name: /4\.5 h across 2 entries/ }).click()
    const chooser = page.getByRole('dialog', { name: 'TW-1 · entries on 2026-11-10', exact: true })
    await chooser.getByText('Preserved note A', { exact: true }).waitFor(); await chooser.getByText('Independent note B', { exact: true }).waitFor()
    await chooser.getByRole('listitem').filter({ hasText: 'Independent note B' }).getByRole('button', { name: 'Edit the entry on TW-1', exact: true }).click()
    const edit = page.getByRole('dialog', { name: 'Correct time entry', exact: true })
    await edit.waitFor(); assert.equal(await edit.getByLabel(/^Note(?:\s*\(optional\))?$/).inputValue(), 'Independent note B')
    await edit.getByLabel('Hours', { exact: true }).fill('1.75'); await edit.getByRole('button', { name: 'Save', exact: true }).click()
    await edit.waitFor({ state: 'hidden' }); await total('5.75 h')
    assert.equal(entries[0].hours, 3); assert.equal(entries[0].note, 'Preserved note A'); assert.equal(entries[2].hours, 1.75); assert.equal(entries[2].note, 'Independent note B')
    assert.equal(patches.at(-1).id, entries[2].id); assert.equal(patches.at(-1).body.note, 'Independent note B')
    assert.ok(patches.every(patch => ![4.5, 4.75].includes(patch.body.hours)), 'cell aggregates are never writes')
    failWeekRefresh = true
    const createsBeforeFailure = creates.length
    await input(tasks[2], '2026-11-13').fill('0.5'); await input(tasks[2], '2026-11-13').press('Enter')
    await cell(tasks[2], '2026-11-13').getByText('Saved. Waiting for the recorded entry to refresh; use Reload if it does not appear.', { exact: true }).waitFor()
    assert.equal(await input(tasks[2], '2026-11-13').isDisabled(), true)
    await page.getByRole('alert').filter({ hasText: 'Synthetic own-week refresh unavailable' }).waitFor()
    await cell(tasks[2], '2026-11-13').getByRole('button', { name: 'Reload', exact: true }).waitFor()
    assert.equal(creates.length, createsBeforeFailure + 1)
    assert.equal(await input(tasks[2], '2026-11-13').isDisabled(), true, 'known saved plus failed refetch never enables another POST')
    failWeekRefresh = false
    await cell(tasks[2], '2026-11-13').getByRole('button', { name: 'Reload', exact: true }).click(); await total('6.25 h')
    assert.equal(await input(tasks[2], '2026-11-13').inputValue(), '0.5'); assert.equal(creates.length, createsBeforeFailure + 1)
    mode = 'Provisional'; await page.reload(); await region.waitFor()
    await page.getByText('Capacity unavailable this week', { exact: true }).waitFor(); await region.getByText('Provisional available 4 h', { exact: true }).waitFor()
    mode = 'Zero'; await page.reload(); await region.waitFor(); await page.getByText('Capacity 0 h this week', { exact: true }).waitFor()
    assert.equal(await region.locator('[data-time-away="true"]').count(), 0, 'known zero never invents absence or nonworking')
    me.capabilities.readOnly = true; await page.reload(); await region.waitFor()
    assert.equal(await region.getByRole('spinbutton').count(), 0); assert.equal(await page.getByRole('button', { name: 'Add time', exact: true }).count(), 0)
    me.capabilities.readOnly = false; mode = 'Complete'
    await page.goto(`${url}&taskId=${tasks[1].taskId}`); await region.waitFor()
    assert.equal(await region.locator('[data-time-week-task]').count(), 1); assert.equal(weekReads.at(-1).taskId, tasks[1].taskId)
    await page.goto(`${base}/time?from=2026-12-28&to=2027-01-03`); await region.waitFor()
    await page.getByRole('button', { name: 'Next', exact: true }).click(); await region.locator('[data-time-day="2027-01-04"]').waitFor()
    assert.equal(new URL(page.url()).searchParams.get('to'), '2027-01-10')
    const callsBeforeFallback = weekReads.length
    await page.goto(`${url}&scope=all`); await page.getByRole('list', { name: 'Hours per day', exact: true }).waitFor()
    assert.equal(await region.count(), 0); assert.equal(weekReads.length, callsBeforeFallback)
    await page.goto(`${base}/time?from=2026-11-10&to=2026-11-22`); await page.getByRole('list', { name: 'Hours per day', exact: true }).waitFor()
    assert.equal(await region.count(), 0); assert.equal(weekReads.length, callsBeforeFallback)
    await page.goto(`${url}&userId=${id(9999)}`); await page.getByRole('list', { name: 'Hours per day', exact: true }).waitFor()
    assert.equal(await region.count(), 0); assert.equal(weekReads.length, callsBeforeFallback)
    await page.setViewportSize({ width: 375, height: 812 }); await page.goto(url); await region.waitFor()
    assert.equal(await region.getAttribute('tabindex'), '0'); await region.focus(); assert.equal(await region.evaluate(element => document.activeElement === element), true)
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), '375px keeps the day table inside its named keyboard scroll region')
    await region.getByText('Time away 8 h', { exact: true }).waitFor()
    await page.setViewportSize({ width: 1440, height: 1000 }); me.flags.capacity_screens = false
    const callsBeforeOff = weekReads.length; await page.reload(); await page.getByRole('list', { name: 'Hours per day', exact: true }).waitFor()
    assert.equal(await region.count(), 0); assert.equal(weekReads.length, callsBeforeOff)
    assert.deepEqual(errors, []); assert.deepEqual(unknownWrites, []); assert.deepEqual(mock.unknownGets, []); assert.deepEqual(mock.apiErrors, [])
    console.log('PASS: synthetic S32 capacity/away/unknown/zero, blank suggestions, exact-once Enter+blur, ID/version patches, lost-response receipt retry, multi-entry selector, canonical scopes, navigation, keyboard phone and dark controls')
  } catch (error) { console.error('time-week-diagnostics', { url: page.url(), creates, patches, weekReads, entries, errors, unknownWrites, unknown: mock.unknownGets }); console.error(await page.locator('body').innerText()); throw error }
  finally { releaseReads.splice(0).forEach(release => release()); await ui.browser.close() }
})().catch(error => { console.error(error); process.exitCode = 1 })
