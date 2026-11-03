// AC-SCR-61: outcomes, one explanation, refusal/retry, history and package-sheet UI.
// AC-SCR-84: register_screens off preserves the old form/body and hides every new outcome/export control.
// Synthetic browser proof only. Actual receipt, access, storage and CSV/XLSX format authority belongs to C's API tests.
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const { newMockPage, me, project } = require('./platform-fixtures.cjs')
const { reviewResponseFixture } = require('./review-response.fixture.cjs')
const base = process.env.TUESDAY_UI_BASE_URL ?? 'http://127.0.0.1:8853'
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(base).hostname))
const f = reviewResponseFixture(project, me.id)
const start = `${base}/projects/${project.projectNumber}/reviews?q=Retained&status=In+Review&page=1&findingSeverity=Advisory`

;(async () => {
  const saved = { flags: { ...me.flags }, capabilities: { ...me.capabilities }, settings: { ...me.settings } }
  const { browser, page, mock } = await newMockPage(base, { events: false })
  const pageErrors = [], unexpectedGets = [], unexpectedWrites = [], commands = [], exports = [], cases = []
  let data = f.fresh(), armed = null, seenCommand = null, exportRefusal = false, responseOpener = null, stage = 'setup'
  const receipts = new Map()
  page.setDefaultTimeout(15000); page.on('pageerror', error => pageErrors.push(error.message))
  const sheet = () => '\ufeffAll rounds; one row per stored finding, latest retained response.\r\nBlank outcome: not recorded\r\n' +
    'Round,Comment,Originator,Discipline,Severity,Status,Response,Outcome\r\n' + data.findings.map(row => [row.roundId === f.id(9210) ? 1 : 2,
      row.text, 'Review originator A', 'Civil', row.severity, row.status, row.response ?? '', f.outcomes.includes(row.responseOutcome) ? row.responseOutcome : '']
      .map(value => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\r\n') + '\r\n'
  try {
    me.flags = Object.fromEntries(Object.keys(saved.flags).map(key => [key, false]))
    Object.assign(me.flags, { register_screens: true, data_entry_safety: true })
    me.capabilities = { ...saved.capabilities, readOnly: false }; me.settings.today = '2026-11-02'
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname.replace('/api/v1/', '')
      const action = new RegExp(`^projects/${project.id}/reviews/${f.package.id}/findings/([^/]+)/action$`).exec(path)
      if (request.method() !== 'GET') {
        if (path === 'me/sign-in') return route.fallback()
        const raw = request.postData(), body = raw ? JSON.parse(raw) : null
        if (request.method() !== 'POST' || !action || !armed) {
          unexpectedWrites.push({ method: request.method(), path, body })
          return route.fulfill({ status: 409, json: { code: 'unexpected_review_response_write', detail: 'No command armed for this action' } })
        }
        const mode = armed; armed = null
        commands.push({ path, raw, body })
        assert.equal(body.action, 'Responded'); assert.equal(body.rowVersion, mode.rowVersion ?? 7)
        assert.match(body.requestId, /^[0-9a-f]{8}-[0-9a-f-]{27}$/i)
        let reply = receipts.get(body.requestId)
        if (reply) assert.equal(reply.raw, raw, 'A simulated receipt accepts only the exact original body')
        if (mode.commit || mode.kind === 'success') {
          if (!reply) { reply = { raw, result: f.apply(data, action[1], body) }; receipts.set(body.requestId, reply) }
        }
        try {
          if (mode.kind === 'abort') return await route.abort('failed')
          if (mode.kind === 'malformed') return await route.fulfill({ status: 200, contentType: 'application/json', body: '{' })
          if (mode.kind === 'malformed-shape') return await route.fulfill({ status: 200, json: mode.reply })
          if (mode.kind === 'refusal') return await route.fulfill({ status: mode.status, contentType: 'application/problem+json', json: {
            status: mode.status, code: mode.status === 409 ? 'concurrency_conflict' : 'response_refused',
            detail: `Synthetic ${mode.status} response refusal`, currentRowVersion: 999,
            errors: { reason: ['Synthetic named response blocker'], evidenceUrl: ['Synthetic named evidence blocker'] },
          } })
          return await route.fulfill({ json: reply.result })
        } finally { seenCommand?.(); seenCommand = null }
      }
      if (path === `projects/${project.id}/review-findings`) {
        if (!me.flags.register_screens) { unexpectedGets.push(path); return route.fulfill({ status: 404, json: { code: 'not_found' } }) }
        return route.fulfill({ json: f.register(data, url.searchParams) })
      }
      if (path.startsWith(`projects/${project.id}/review-findings/`)) {
        const finding = data.findings.find(row => row.id === path.split('/').at(-1))
        if (!finding || (!me.flags.register_screens && !me.flags.screen_patterns)) return route.fulfill({ status: 404, json: { code: 'not_found' } })
        return route.fulfill({ json: f.locator(finding, !!me.flags.screen_patterns) })
      }
      if (path === `projects/${project.id}/reviews`) return route.fulfill({ json: f.packages(1) })
      if (path === `projects/${project.id}/reviews/discipline-summary`) {
        if (!me.flags.register_screens) { unexpectedGets.push(path); return route.fulfill({ status: 404, json: { code: 'not_found' } }) }
        assert.deepEqual([...url.searchParams.keys()], [], 'Discipline summary takes no finding or package-list filters')
        return route.fulfill({ json: f.summary })
      }
      if (path === `projects/${project.id}/changes/options` || path === `projects/${project.id}/reviews/options`) return route.fulfill({ json: f.options })
      if (path === `projects/${project.id}/reviews/${f.package.id}`) return route.fulfill({ json: data })
      if (path === `projects/${project.id}/reviews/${f.package.id}/response-sheet`) {
        exports.push({ path, query: Object.fromEntries(url.searchParams) })
        assert.deepEqual([...url.searchParams.keys()], ['format'], 'The package sheet cannot inherit round, finding or package-list filters')
        if (!me.flags.register_screens || exportRefusal) return route.fulfill({ status: 403, json: { code: 'forbidden', detail: 'Synthetic response sheet refused' } })
        assert.equal(url.searchParams.get('format'), 'csv', 'This mock proves CSV transport only; real XLSX bytes are API-tested')
        return route.fulfill({ contentType: 'text/csv; charset=utf-8', headers: { 'content-disposition': 'attachment; filename="synthetic-all-round-responses.csv"' }, body: sheet() })
      }
      if (['config', 'me', 'reference', 'workspaces', 'views', 'projects', 'me/notifications/pulse', 'me/notifications/unread-count',
        `projects/${project.projectNumber}`, `projects/${project.id}`].includes(path)) return route.fallback()
      unexpectedGets.push(path); return route.fulfill({ status: 501, json: { code: 'unexpected_review_response_get', detail: path } })
    })
    const form = () => page.locator('[data-review-response-form]')
    const inspector = () => page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: `${f.package.key} · ${f.package.title}`, exact: true }) })
    const article = finding => inspector().locator('article').filter({ has: page.getByText(finding.text, { exact: true }) })
    const frames = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const preserved = () => {
      const url = new URL(page.url())
      assert.equal(url.searchParams.get('q'), 'Retained'); assert.equal(url.searchParams.get('status'), 'In Review')
      assert.equal(url.searchParams.get('page'), '1'); assert.equal(url.searchParams.get('findingSeverity'), 'Advisory')
    }
    const go = async (finding = data.findings[0], packageMode = false) => {
      await page.goto(start + `&panel=${packageMode ? `ReviewPackage:${f.package.id}` : `ReviewFinding:${finding.id}`}&round=${finding.roundId}`)
      await inspector().getByText(finding.text, { exact: true }).waitFor(); preserved()
    }
    const open = async (finding = data.findings[0]) => {
      await go(finding)
      const trigger = article(finding).getByRole('button', { name: 'Record response', exact: true })
      responseOpener = await trigger.elementHandle()
      await trigger.click(); await form().waitFor()
      return trigger
    }
    const fill = async (outcome = 'Not incorporated', reason = 'Synthetic response with supporting evidence.') => {
      await form().getByLabel('Response outcome', { exact: true }).selectOption(outcome)
      await form().getByRole('textbox', { name: outcome === 'Not incorporated' ? 'Why it was not incorporated' : 'Response', exact: true }).fill(reason)
      await form().getByRole('textbox', { name: 'Evidence link', exact: true }).fill('https://example.test/response-evidence')
    }
    const send = async mode => {
      assert.equal(armed, null); armed = mode
      const observed = new Promise(resolve => { seenCommand = resolve })
      await form().getByRole('button', { name: /^(Confirm|Retry response)$/ }).click(); await observed
      if (mode.kind === 'success') await form().waitFor({ state: 'hidden' })
      else await form().getByRole('alert').waitFor()
      return commands.at(-1)
    }
    const cancel = async (dirty = true) => {
      const count = commands.length
      await form().getByRole('button', { name: 'Cancel', exact: true }).click()
      if (dirty) {
        // Name the actual question, including its closing portal; .last() would retarget the parent inspector.
        const question = page.getByRole('dialog', { includeHidden: true }).filter({ has: page.getByRole('heading', { name: 'Keep editing or discard?', exact: true, includeHidden: true }) })
        const discard = question.getByRole('button', { name: 'Discard', exact: true, includeHidden: true })
        await discard.click(); await discard.waitFor({ state: 'hidden' })
        await question.waitFor({ state: 'hidden' }); await question.waitFor({ state: 'detached' })
      }
      await form().waitFor({ state: 'hidden' }); await form().waitFor({ state: 'detached' })
      assert.ok(responseOpener, 'Capture the real Record response opener before opening the form')
      await page.waitForFunction(node => node?.isConnected && document.activeElement === node, responseOpener)
      assert.equal(commands.length, count)
    }
    const reset = () => { data = f.fresh(); receipts.clear() }

    // AC-SCR-61: no default; four exact values; a single textarea survives the conditional label change.
    const opener = await open(), initialCount = commands.length
    assert.equal(await form().getByLabel('Response outcome', { exact: true }).inputValue(), '')
    assert.deepEqual(await form().locator('select option').evaluateAll(nodes => nodes.map(node => node.value)), ['', ...f.outcomes])
    const textareaId = await form().locator('textarea').getAttribute('id')
    await form().getByRole('button', { name: 'Confirm', exact: true }).click()
    assert.equal(await form().locator('select').evaluate(node => node.validity.valueMissing), true)
    await form().getByLabel('Response outcome', { exact: true }).selectOption('Not incorporated')
    assert.equal(await form().getByLabel('Why it was not incorporated', { exact: true }).getAttribute('id'), textareaId)
    assert.equal(await form().locator('textarea').count(), 1)
    await form().getByRole('button', { name: 'Confirm', exact: true }).click()
    assert.equal(await form().locator('textarea').evaluate(node => node.validity.valueMissing), true)
    await fill('Not incorporated', ' abc ')
    await form().getByRole('button', { name: 'Confirm', exact: true }).click()
    assert.equal(await form().locator('textarea').evaluate(node => node.validity.valid), false)
    await fill(); await form().getByLabel('Evidence link', { exact: true }).fill('')
    await form().getByRole('button', { name: 'Confirm', exact: true }).click()
    assert.equal(await form().getByLabel('Evidence link', { exact: true }).evaluate(node => node.validity.valueMissing), true)
    await form().getByLabel('Evidence link', { exact: true }).fill('ftp://example.test/evidence')
    await form().getByRole('button', { name: 'Confirm', exact: true }).click()
    assert.equal(await form().getByLabel('Evidence link', { exact: true }).evaluate(node => node.validity.valid), false)
    assert.equal(commands.length, initialCount, 'Missing outcome/reason, trimmed-short reason and non-HTTP(S) evidence make no POST')
    await form().getByRole('button', { name: 'Cancel', exact: true }).click()
    await page.getByRole('dialog').last().getByRole('button', { name: 'Keep editing', exact: true }).click()
    assert.equal(await form().getByLabel('Why it was not incorporated', { exact: true }).inputValue(), 'Synthetic response with supporting evidence.')
    await cancel(); await page.waitForFunction(node => document.activeElement === node, await opener.elementHandle())
    await open(); await cancel(false)
    cases.push('AC-SCR-61: explicit outcome; one explanation; required trimmed reason and HTTP(S) evidence; clean/dirty Cancel zero POST and natural opener focus')

    // Check.Reason requires a trimmed minimum only; this form must not invent an explanation length cap.
    reset(); await open()
    const longReason = 'Synthetic technical explanation for the retained revision; no added cap.\n'.repeat(75)
    assert.ok(longReason.length > 4000)
    assert.equal(await form().getByLabel('Response', { exact: true }).getAttribute('maxlength'), null)
    await fill('Clarified', longReason)
    assert.equal(await form().getByLabel('Response', { exact: true }).inputValue(), longReason)
    const beforeLongResponse = commands.length, longResponse = await send({ kind: 'success' })
    assert.equal(commands.length, beforeLongResponse + 1, 'The long explanation sends one explicitly armed normal command')
    assert.equal(longResponse.body.reason, longReason, 'The complete explanation reaches the command unchanged')
    assert.equal(data.findings[0].response, longReason)
    assert.equal(data.events.filter(event => event.findingId === data.findings[0].id).length, 1)
    cases.push('AC-SCR-61: explanation over 4000 characters has no invented maxlength and is submitted unchanged in one command')

    // Every definite refusal retains the original inputs/version. Correction gets a new ID only before any uncertainty.
    for (const status of [400, 403, 404, 409]) {
      reset(); await open(); await fill('Clarified', `Retained explanation for ${status}.`)
      const refused = await send({ kind: 'refusal', status })
      assert.equal(await form().getByLabel('Response', { exact: true }).inputValue(), `Retained explanation for ${status}.`)
      assert.equal(await form().getByLabel('Response outcome', { exact: true }).isEnabled(), true)
      const reasons = form().getByRole('list', { name: 'Reasons this action was refused' })
      await reasons.getByText('Synthetic named response blocker', { exact: true }).waitFor()
      await reasons.getByText('Synthetic named evidence blocker', { exact: true }).waitFor()
      if (status === 409) await form().getByText(/^This record changed\./).waitFor()
      assert.equal(data.findings[0].status, 'Open'); assert.equal(data.events.length, 1)
      await form().getByLabel('Response', { exact: true }).fill(`Corrected explanation for ${status}.`)
      const corrected = await send({ kind: 'success' })
      assert.notEqual(corrected.body.requestId, refused.body.requestId); assert.equal(corrected.body.rowVersion, 7)
      assert.equal(data.findings[0].status, 'Responded')
    }
    cases.push('AC-SCR-61: 400/403/404/409 retain input and named blockers; correction never adopts server currentRowVersion')

    // Lost response, 5xx, timeout and malformed successful JSON can all hide a committed command.
    for (const first of [{ kind: 'abort' }, { kind: 'refusal', status: 503 }, { kind: 'refusal', status: 408 }, { kind: 'malformed' },
      ...[{}, { id: f.id(99999), rowVersion: 8 }, { id: f.id(15101) }, { id: f.id(15101), rowVersion: '8' },
        { id: f.id(15101), rowVersion: 8.5 }, { id: f.id(15101), rowVersion: -1 }].map(reply => ({ kind: 'malformed-shape', reply }))]) {
      reset(); await open(); await fill('Not incorporated', 'Not incorporated because the recorded clearance is required.')
      const original = await send({ ...first, commit: true })
      await form().getByText(/^The response may have been saved\./).waitFor()
      assert.equal(await form().getByLabel('Why it was not incorporated', { exact: true }).inputValue(), original.body.reason)
      assert.equal(await form().getByLabel('Evidence link', { exact: true }).inputValue(), original.body.evidenceUrl)
      for (const status of [403, 404, 409]) {
        for (const input of [form().getByLabel('Response outcome', { exact: true }), form().getByLabel('Why it was not incorporated', { exact: true }), form().getByLabel('Evidence link', { exact: true })])
          assert.equal(await input.isDisabled(), true)
        const retried = await send({ kind: 'refusal', status })
        assert.equal(retried.raw, original.raw, 'Later refusal cannot replace an uncertain body, ID, outcome or original version')
        assert.equal(await form().getByLabel('Response outcome', { exact: true }).isDisabled(), true)
      }
      const answered = await send({ kind: 'success' })
      assert.equal(answered.raw, original.raw); assert.equal(data.events.filter(event => event.findingId === data.findings[0].id).length, 1)
      assert.equal(data.findings[0].rowVersion, 8)
    }
    cases.push('AC-SCR-61: uncertain command remains immutable through later 403/404/409; exact retry resolves one simulated receipt without a second event')
    reset(); await open(); await fill('Noted')
    await send({ kind: 'abort', commit: true }); await cancel()
    cases.push('AC-SCR-61: explicitly discarding an uncertain form does not send or manufacture another command')

    // All outcomes remain Responded, while immutable event history and old rounds retain their own outcome facts.
    reset()
    for (let index = 0; index < f.outcomes.length; index++) {
      const finding = data.findings[index], outcome = f.outcomes[index]
      await open(finding); await fill(outcome, index === 0 ? '=Synthetic formula-looking response, "quoted"\nUnicode café.' : `Recorded ${outcome} explanation.`)
      const result = await send({ kind: 'success' })
      assert.equal(result.body.responseOutcome, outcome); assert.equal(data.findings[index].status, 'Responded')
      await article(finding).getByText(outcome, { exact: true }).waitFor()
      assert.equal(await article(finding).getByRole('button', { name: 'Verify closure', exact: true }).count(), 0, 'Resolver is not the named independent verifier')
      const history = article(finding).locator('details')
      await history.locator('summary').click(); await history.getByText(`Response outcome: ${outcome}`, { exact: true }).waitFor()
    }
    const retainedEvent = data.events.find(event => event.findingId === data.findings[0].id), retainedBytes = JSON.stringify(retainedEvent)
    // Simulate the canonical verifier returning the finding; the last response remains a historical fact.
    data.findings[0].status = 'Open'
    await open(data.findings[0])
    assert.equal(await form().getByLabel('Response outcome', { exact: true }).inputValue(), '', 'A retained earlier outcome is never a default for the next response')
    await fill('Noted', '=Second synthetic response, "quoted"\nUnicode café retained.')
    await send({ kind: 'success', rowVersion: 8 })
    assert.equal(JSON.stringify(retainedEvent), retainedBytes, 'A later response does not rewrite the earlier event')
    await article(data.findings[0]).getByText('Noted', { exact: true }).waitFor()
    const repeatedHistory = article(data.findings[0]).locator('details')
    await repeatedHistory.locator('summary').click()
    await repeatedHistory.getByText('Response outcome: Incorporated', { exact: true }).waitFor()
    await repeatedHistory.getByText('Response outcome: Noted', { exact: true }).waitFor()
    await repeatedHistory.getByText('=Synthetic formula-looking response, "quoted"\nUnicode café.', { exact: true }).waitFor()
    assert.equal(data.events.filter(event => event.findingId === data.findings[0].id).length, 2)
    const unknown = data.findings[5]
    await go(unknown); await article(unknown).getByText('Not recorded', { exact: true }).waitFor()
    assert.equal(await inspector().getByText('Unknown legacy outcome', { exact: true }).count(), 0)
    const legacy = data.findings[4]
    await go(legacy); await article(legacy).getByText('Not recorded', { exact: true }).waitFor()
    await article(legacy).locator('summary').click()
    await article(legacy).getByText('Response outcome: Not recorded', { exact: true }).waitFor()
    assert.equal(await article(legacy).getByRole('button', { name: 'Record response', exact: true }).count(), 0)
    cases.push('AC-SCR-61: four current/event outcomes; Responded never implies closure; historical and unknown outcomes remain Not recorded')

    // The CSV fixture includes every stored finding in both rounds, even while the inspector is on an old exact finding.
    const beforeExport = commands.length, expectedCsv = sheet()
    await inspector().getByRole('button', { name: 'Download response sheet', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Excel (.xlsx)', exact: true }).waitFor()
    const downloading = page.waitForEvent('download')
    await page.getByRole('menuitem', { name: 'CSV (.csv)', exact: true }).click()
    const download = await downloading
    assert.equal(download.suggestedFilename(), 'synthetic-all-round-responses.csv')
    assert.equal(await fs.readFile(await download.path(), 'utf8'), expectedCsv)
    assert.ok(expectedCsv.includes('Earlier round comment retained after carry'))
    assert.ok(expectedCsv.includes('Unicode café retained.'), 'The sheet contains the latest retained response')
    assert.equal(expectedCsv.includes('Unicode café.'), false, 'The replaced response remains in event history, not the latest-response sheet')
    exportRefusal = true
    await inspector().getByRole('button', { name: 'Download response sheet', exact: true }).click()
    const denied = page.waitForResponse(response => response.url().includes('/response-sheet?format=xlsx') && response.status() === 403)
    await page.getByRole('menuitem', { name: 'Excel (.xlsx)', exact: true }).click(); await denied
    await page.getByText('Synthetic response sheet refused', { exact: true }).waitFor()
    assert.equal(commands.length, beforeExport); preserved(); exportRefusal = false
    cases.push('AC-SCR-61: actual CSV browser download bytes match all-round synthetic dataset; Excel menu sends format=xlsx and presents refusal without a command')

    // Exact list source, round, filters, scroll and natural focus survive inspection and form Cancel.
    stage = 'finding-source-return'
    await page.setViewportSize({ width: 1440, height: 500 }); reset(); await page.goto(start)
    const source = page.locator(`[data-review-finding-open="${data.findings[3].id}"]:visible`)
    await source.waitFor(); await source.scrollIntoViewIfNeeded(); await frames()
    const sourceId = await source.getAttribute('id')
    // Reach the source through real keyboard navigation; never assign focus or scroll position to make return pass.
    for (let step = 0; step < 100 && await page.evaluate(() => document.activeElement?.id) !== sourceId; step++)
      await page.keyboard.press('Tab')
    assert.equal(await page.evaluate(() => document.activeElement?.id), sourceId)
    await frames()
    const origin = page.url(), scroll = await page.locator('#content').evaluate(node => ({ top: node.scrollTop, left: node.scrollLeft }))
    assert.ok(scroll.top > 0, 'Short viewport produces real nonzero source scroll before inspection')
    await page.keyboard.press('Enter'); await inspector().getByText(data.findings[3].text, { exact: true }).waitFor()
    const exactUrl = page.url(); assert.equal(new URL(exactUrl).searchParams.get('round'), data.findings[3].roundId)
    const sourceResponseTrigger = article(data.findings[3]).getByRole('button', { name: 'Record response', exact: true })
    responseOpener = await sourceResponseTrigger.elementHandle()
    await sourceResponseTrigger.click(); await form().waitFor()
    await fill('Noted'); await cancel(); assert.equal(page.url(), exactUrl)
    await page.keyboard.press('Escape'); await page.waitForURL(origin)
    await page.waitForFunction(id => document.activeElement?.id === id, sourceId); await frames()
    const restored = await page.locator('#content').evaluate(node => ({ top: node.scrollTop, left: node.scrollLeft }))
    assert.ok(Math.abs(restored.top - scroll.top) <= 2); assert.ok(Math.abs(restored.left - scroll.left) <= 2); preserved()
    cases.push('AC-SCR-61: actual finding/round/filter source returns with natural keyboard focus and original scroll')

    stage = 'phone-375'
    await page.setViewportSize({ width: 375, height: 812 }); await open(); await fill('Not incorporated')
    for (const control of [form().getByLabel('Response outcome', { exact: true }), form().getByLabel('Why it was not incorporated', { exact: true }), form().getByLabel('Evidence link', { exact: true })]) {
      const box = await control.boundingBox(); assert.ok(box && box.x >= 0 && box.x + box.width <= 376)
    }
    assert.equal(await form().evaluate(node => node.scrollWidth <= node.clientWidth + 1), true)
    await form().getByRole('button', { name: 'Cancel', exact: true }).scrollIntoViewIfNeeded(); await cancel()
    cases.push('AC-SCR-61: 375px outcome/explanation/evidence and Cancel remain reachable without horizontal overflow')

    // AC-SCR-84: legacy response remains available without new fields; no register read or sheet request is hidden in rendering.
    stage = 'register-flag-off'
    reset(); me.flags.register_screens = false; await page.setViewportSize({ width: 1440, height: 1000 })
    await go(data.findings[0], true)
    assert.equal(await page.locator('[data-review-open-findings]').count(), 0)
    assert.equal(await inspector().getByText('Response outcome', { exact: true }).count(), 0)
    assert.equal(await inspector().getByRole('button', { name: 'Download response sheet', exact: true }).count(), 0)
    assert.equal(await inspector().getByText('Not recorded', { exact: true }).count(), 0)
    await article(data.findings[0]).getByRole('button', { name: 'Record response', exact: true }).click()
    const legacyForm = page.getByRole('dialog').filter({ has: page.getByRole('heading', { name: 'Respond to finding', exact: true }) })
    await legacyForm.getByRole('heading', { name: 'Respond to finding', exact: true }).waitFor()
    assert.equal(await form().count(), 0); assert.equal(await legacyForm.getByLabel('Response outcome', { exact: true }).count(), 0)
    await legacyForm.getByLabel('Reason or response', { exact: true }).fill('Legacy response remains usable without the new outcome.')
    await legacyForm.getByLabel('Evidence link', { exact: true }).fill('http://example.test/legacy-response')
    const beforeLegacy = commands.length; armed = { kind: 'success' }
    const observed = new Promise(resolve => { seenCommand = resolve })
    await legacyForm.getByRole('button', { name: 'Confirm', exact: true }).click(); await observed
    await legacyForm.waitFor({ state: 'hidden' })
    assert.equal(commands.length, beforeLegacy + 1); assert.equal('responseOutcome' in commands.at(-1).body, false)
    assert.equal(commands.at(-1).body.reason, 'Legacy response remains usable without the new outcome.')
    cases.push('AC-SCR-84: flag-off legacy form/command remain usable and omit new outcome; new list/outcome/history/export presentation absent')

    assert.equal(armed, null); assert.deepEqual(unexpectedWrites, []); assert.deepEqual(unexpectedGets, [])
    assert.deepEqual(mock.unknownGets, []); assert.deepEqual(pageErrors, [])
    console.log(JSON.stringify({ status: 'PASS', scope: 'synthetic R2 response UI regression only; API/export/storage and external intake have separate verification', cases,
      commandCount: commands.length, exportRequests: exports }))
  } catch (error) {
    let observed
    try {
      observed = await page.evaluate(() => {
        const active = document.activeElement, content = document.querySelector('#content')
        const dialogs = [...document.querySelectorAll('[data-slot="dialog-content"], [role="dialog"]')].slice(0, 12).map(node => {
          const style = getComputedStyle(node), box = node.getBoundingClientRect(), body = node.querySelector('[data-slot="dialog-body"]')
          return { id: node.id, state: node.getAttribute('data-state'), ariaHidden: node.getAttribute('aria-hidden'),
            visible: box.width > 0 && box.height > 0 && style.display !== 'none' && style.visibility !== 'hidden', display: style.display, visibility: style.visibility,
            headings: [...node.querySelectorAll('[role="heading"],h1,h2,h3')].slice(0, 4).map(heading => heading.textContent?.slice(0, 180)),
            scrollTop: body?.scrollTop ?? node.scrollTop, scrollLeft: body?.scrollLeft ?? node.scrollLeft }
        })
        return { url: location.href, dialogs, active: { tag: active?.tagName, id: active?.id, text: active?.textContent?.slice(0, 180) },
          scroll: content ? { top: content.scrollTop, left: content.scrollLeft, height: content.clientHeight, totalHeight: content.scrollHeight } : null }
      })
    } catch (diagnosticError) { observed = { diagnosticError: String(diagnosticError) } }
    console.error(JSON.stringify({ stage, ...observed, completedCases: cases, counters: { commands: commands.length, exports: exports.length,
      unexpectedGets: unexpectedGets.length, unexpectedWrites: unexpectedWrites.length, pageErrors: pageErrors.length, baseUnknownGets: mock.unknownGets.length },
      lastCommands: commands.slice(-3).map(command => ({ path: command.path, requestId: command.body.requestId, rowVersion: command.body.rowVersion })),
      armed: armed?.kind ?? null }))
    throw error
  } finally {
    me.flags = saved.flags; me.capabilities = saved.capabilities; me.settings = saved.settings
    await browser.close()
  }
})().catch(error => { console.error(error); process.exitCode = 1 })
